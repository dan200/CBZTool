using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Dan200.CBZTool
{
    public class Program
    {
        static string[] METADATA_FILENAMES = { "tag.txt", "ComicInfo.xml" };

        static void PrintUsage()
        {
            Console.WriteLine(
                "Usage:" + Environment.NewLine +
                Environment.NewLine +
                "CBZTool extract PATH... [options]" + Environment.NewLine +
                "  -o [directory]    Specify the directory to extract to (defaults to the input path minus the extension)" + Environment.NewLine +
                "  -p [range]        Specify the range of pages to extract (ex: 1-10 2,4,6 7-*) (default=*)" + Environment.NewLine +
                "  -a                Appends the extracted pages to the end of the directory, instead of replacing them (default=0)" + Environment.NewLine +
                "  -denoise          Runs a noise reduction algorithm on the images when extracting (default=0)" + Environment.NewLine +
                "  -whitebalance     Runs a white balancing algorithm on the images when extracting (default=0)" + Environment.NewLine +
                "  -metadata         Specify that metadata files (tag.txt, ComicInfo.xml) should also be extracted (default=0)" + Environment.NewLine +
                Environment.NewLine +
                "CBZTool compress PATH... [options]" + Environment.NewLine +
                "  -o [directory]    Specify the file to compress to (defaults to the input path with the .cbz extension appended)" + Environment.NewLine +
                "  -p [range]        Specify the range of pages to compress (ex: 1-10 2,4,6 7-*) (default=*)" + Environment.NewLine +
                "  -a                Appends the extracted pages to the end of the archive, instead of replacing it (default=0)" + Environment.NewLine +
                "  -metadata         Specify that metadata files (tag.txt, ComicInfo.xml) should also be added (default=0)"
            );
        }

        static void ExtractOnePage(Stream imageStream, IList<IImageFilter> filters, string outputFile)
        {
            if (filters.Count > 0)
            {
                // Decode the file as an image
                using (var image = (Bitmap)Image.FromStream(imageStream, false))
                {
                    // Apply filters to the image
                    var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
                    try
                    {
                        foreach (IImageFilter filter in filters)
                        {
                            filter.Filter(bits);
                        }
                    }
                    finally
                    {
                        image.UnlockBits(bits);
                    }

                    // Save the image
                    image.Save(outputFile);
                }
            }
            else
            {
                // Write the stream directly to disk
                using (var outputStream = File.OpenWrite(outputFile))
                {
                    imageStream.CopyTo(outputStream);
                }
            }
        }

        static void ExtractOnePage(Entry archiveEntry, IList<IImageFilter> filters, string outputFile)
        {
            if (filters.Count == 0)
            {
                // Extract the file directly to disk
                archiveEntry.Extract(outputFile);
            }
            else
            {
                // Extract the file to memory
                var memoryStream = new MemoryStream();
                archiveEntry.Extract(memoryStream);
                memoryStream.Position = 0;

                // Extract from the stream
                ExtractOnePage(memoryStream, filters, outputFile);
            }
        }

        static bool Extract(string inputFile, MultiRange pages, IList<IImageFilter> filters, string outputDir, bool append, bool includeMetadata)
        {
            // Check the input file exists
            if (!File.Exists(inputFile))
            {
                Console.WriteLine("File not found: {0}", inputFile);
                return false;
            }
            Console.WriteLine("Extracting pages {0} from {1} to {2}", pages, inputFile, outputDir);

            // Create a directory to extract to
            int nextPageIndex = 1;
            if (Directory.Exists(outputDir))
            {
                if(append)
                {
                    while (File.Exists(Path.Combine(outputDir, nextPageIndex + ".png")) || File.Exists(Path.Combine(outputDir, nextPageIndex + ".jpg")))
                    {
                        nextPageIndex++;
                    }
                }
                else
                {
                    Directory.Delete(outputDir, true);
                }
            }
            Directory.CreateDirectory(outputDir);

            if (inputFile.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase) || inputFile.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase))
            {
                // "Extracting" from an image file
                foreach (Range subRange in pages.SubRanges)
                {
                    if (subRange.Last > 1 && subRange.Last != int.MaxValue)
                    {
                        Console.WriteLine("ERROR: Pages {0} out of range for file {1}. Skipping", subRange, inputFile);
                        continue;
                    }

                    for (int pageNum = subRange.First; pageNum <= Math.Min(subRange.Last, 1); ++pageNum)
                    {
                        string outputExtension = (filters.Count > 0) ? ".png" : Path.GetExtension(inputFile);
                        string outputPath = Path.Combine(outputDir, string.Format("{0}{1}", nextPageIndex, outputExtension));
                        using (var inputStream = File.OpenRead(inputFile))
                        {
                            ExtractOnePage(inputStream, filters, outputPath);
                        }

                        nextPageIndex++;
                    }
                }
            }
            else
            {
                // Extracting from an archive
                using (var archive = new ArchiveFile(inputFile))
                {
                    // Find all the pages in the archive
                    var allInputPages = archive.Entries.Select(entry => entry.FileName).Where(name => name.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    allInputPages.Sort(AlphaNumericComparator.Instance);

                    // Extract each page
                    foreach (Range subRange in pages.SubRanges)
                    {
                        if (subRange.Last > allInputPages.Count && subRange.Last != int.MaxValue)
                        {
                            Console.WriteLine("ERROR: Pages {0} out of range for file {1}. Skipping", subRange, inputFile);
                            continue;
                        }

                        for (int pageNum = subRange.First; pageNum <= Math.Min(subRange.Last, allInputPages.Count); ++pageNum)
                        {
                            string inputPath = allInputPages[pageNum - 1];
                            Entry inputEntry = archive.Entries.Where(entry => entry.FileName.Equals(inputPath)).First();

                            string outputExtension = (filters.Count > 0) ? ".png" : Path.GetExtension(inputPath);
                            string outputPath = Path.Combine(outputDir, string.Format("{0}{1}", nextPageIndex, outputExtension));
                            ExtractOnePage(inputEntry, filters, outputPath);

                            nextPageIndex++;
                        }
                    }

                    if (includeMetadata)
                    {
                        // Extract any metadata
                        foreach (string metadataFileName in METADATA_FILENAMES)
                        {
                            Entry inputEntry = archive.Entries.Where(entry => Path.GetFileName(entry.FileName).Equals(metadataFileName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                            if (inputEntry != null)
                            {
                                string outputPath = Path.Combine(outputDir, metadataFileName);
                                inputEntry.Extract(outputPath);
                            }
                        }
                    }
                }
            }

            return true;
        }
        
        static bool Compress(string inputDir, MultiRange pages, string outputFile, bool append, bool includeMetadata)
        {
            // Check the input directory exists
            if (!Directory.Exists(inputDir))
            {
                Console.WriteLine("Directory not found: {0}", inputDir);
                return false;
            }
            Console.WriteLine("Compressing pages {0} from {1} to {2}", pages, inputDir, outputFile);

            // Create a directory to extract to put the zip file in
            if (File.Exists(outputFile))
            {
                if (!append)
                {
                    File.Delete(outputFile);
                }
            }
            else
            {
                var outputDir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
            }

            // Find all the pages in the directory
            var allInputPages = Directory.GetFiles(inputDir).Where(name => name.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase)).ToList();
            allInputPages.Sort(AlphaNumericComparator.Instance);

            using (var fileStream = new FileStream(outputFile, append ? FileMode.OpenOrCreate : FileMode.Create))
            {
                using (var archive = new ZipArchive(fileStream, append ? ZipArchiveMode.Update : ZipArchiveMode.Create))
                {
                    // Put each file into the archive
                    int nextPageIndex = 1;
                    if(append)
                    {
                        while (archive.GetEntry(nextPageIndex + ".png") != null || archive.GetEntry(nextPageIndex + ".jpg") != null)
                        {
                            nextPageIndex++;
                        }
                    }

                    foreach (Range subRange in pages.SubRanges)
                    {
                        if (subRange.Last > allInputPages.Count && subRange.Last != int.MaxValue)
                        {
                            Console.WriteLine("ERROR: Pages {0} out of range for directory {1}. Skipping", subRange, inputDir);
                            continue;
                        }

                        for (int pageNum = subRange.First; pageNum <= Math.Min(subRange.Last, allInputPages.Count); ++pageNum)
                        {
                            string inputPath = allInputPages[pageNum - 1];
                            string outputExtension = Path.GetExtension(inputPath);
                            string outputPath = string.Format("{0}{1}", nextPageIndex, outputExtension);
                            using (var outputStream = archive.CreateEntry(outputPath).Open())
                            {
                                using (var inputStream = File.OpenRead(inputPath))
                                {
                                    inputStream.CopyTo(outputStream);
                                }
                            }
                            nextPageIndex++;
                        }
                    }

                    if (includeMetadata)
                    {
                        // Include any metadata files
                        foreach (string metadataFileName in METADATA_FILENAMES)
                        {
                            var inputPath = Path.Combine(inputDir, metadataFileName);
                            if (File.Exists(inputPath))
                            {
                                string outputPath = metadataFileName;
                                if (append)
                                {
                                    var existingEntry = archive.GetEntry(outputPath);
                                    if (existingEntry != null)
                                    {
                                        existingEntry.Delete();
                                    }
                                }
                                using (var outputStream = archive.CreateEntry(outputPath).Open())
                                {
                                    using (var inputStream = File.OpenRead(inputPath))
                                    {
                                        inputStream.CopyTo(outputStream);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        static string[] ExpandWildcardPath(string path, bool includeFiles, bool includeDirs)
        {
            if(!path.Contains("*"))
            {
                return new string[] { path };
            }

            var dirPart = Path.GetDirectoryName(path);
            if (dirPart == null || dirPart.Length == 0)
            {
                dirPart = ".";
            }

            var filePart = Path.GetFileName(path);
            var paths = new List<string>();
            if (Directory.Exists(dirPart))
            {
                if (includeDirs)
                {
                    foreach (var filepath in Directory.GetDirectories(dirPart, filePart))
                    {
                        paths.Add(filepath);
                    }
                }
                if (includeFiles)
                {
                    foreach (var filepath in Directory.GetFiles(dirPart, filePart))
                    {
                        paths.Add(filepath);
                    }
                }
                paths.Sort(AlphaNumericComparator.Instance);
            }
            return paths.ToArray();
        }

        static void Main(string[] args)
        {
            var arguments = new ProgramArguments(args);
            if(arguments.Count >= 2 && arguments.Get(0) == "extract")
            {
                // Extract one or more files:

                // Parse command line arguments
                var commonOutputDir = arguments.GetStringOption("o", null);

                var pages = MultiRange.All;
                var pagesString = arguments.GetStringOption("p", null);
                if(pagesString != null && !MultiRange.TryParse(pagesString, out pages))
                {
                    Console.WriteLine("Failed to parse page range {0}", pagesString);
                    return;
                }

                var append = arguments.GetBoolOption("a");
                var includeMetadata = arguments.GetBoolOption("metadata");

                var filters = new List<IImageFilter>();
                if(arguments.GetBoolOption("denoise"))
                {
                    filters.Add(new DenoiseFilter());
                }
                if (arguments.GetBoolOption("whitebalance"))
                {
                    filters.Add(new WhiteBalanceFilter());
                }

                var inputFiles = new List<string>();
                for (int i = 1; i < arguments.Count; ++i)
                {
                    foreach(var str in ExpandWildcardPath(arguments.Get(i), true, false))
                    {
                        inputFiles.Add(str);
                    }
                }

                // Extract the files
                foreach (var inputFile in inputFiles)
                {
                    if (commonOutputDir != null)
                    {
                        if (Extract(inputFile, pages, filters, commonOutputDir, append, includeMetadata))
                        {
                            append = true; // If all files are being extracted to the same place, we don't want them to overwrite each other
                            includeMetadata = false; // We don't want more than one set of metadata to be added to the same file
                        }
                    }
                    else
                    {
                        var extension = Path.GetExtension(inputFile);
                        var outputDir = inputFile.Substring(0, inputFile.Length - extension.Length);
                        Extract(inputFile, pages, filters, outputDir, append, includeMetadata);
                    }
                }
            }
            else if(arguments.Count >= 2 && arguments.Get(0) == "compress")
            {
                // Compress one or more directories:

                // Parse command line arguments
                var commonOutputFile = arguments.GetStringOption("o", null);

                var pages = MultiRange.All;
                var pagesString = arguments.GetStringOption("p", null);
                if (pagesString != null && !MultiRange.TryParse(pagesString, out pages))
                {
                    Console.WriteLine("Failed to parse page range {0}", pagesString);
                    return;
                }

                var append = arguments.GetBoolOption("a");
                var includeMetadata = arguments.GetBoolOption("metadata");

                var inputDirs = new List<string>();
                for (int i = 1; i < arguments.Count; ++i)
                {
                    foreach (var str in ExpandWildcardPath(arguments.Get(i), false, true))
                    {
                        inputDirs.Add(str);
                    }
                }
                
                // Compress the directories
                foreach(var inputDir in inputDirs)
                {
                    if (commonOutputFile != null)
                    {
                        if (Compress(inputDir, pages, commonOutputFile, append, includeMetadata))
                        {
                            append = true; // If all directories are being compressed to the same file, we don't want their contents to overwrite each other
                            includeMetadata = false; // We don't want more than one set of metadata to be added to the same directory
                        }
                    }
                    else
                    {
                        var outputFile = inputDir + ".cbz";
                        Compress(inputDir, pages, outputFile, append, includeMetadata);
                    }
                }
            }
            else
            {
                // No/unsupported arguments
                PrintUsage();
            }
        }
    }
}
