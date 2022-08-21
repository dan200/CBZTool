using CBZTool;
using Dan200.CBZLib;
using Dan200.CBZLib.ImageFilters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dan200.CBZTool
{
    public class Program
    {
        private static void PrintUsage()
        {
            Console.WriteLine(
                "Usage:" + Environment.NewLine +
                Environment.NewLine +
                "CBZTool extract PATH... [options]" + Environment.NewLine +
                "  -o [path]         Specify the path to extract to (defaults to the input path minus the extension). Can be a directory, another CBZ file or a PDF" + Environment.NewLine +
                "  -p [range]        Specify the range of pages to extract (ex: 1-10 2,4,6 7-*) (default=*)" + Environment.NewLine +
                "  -a                Appends the extracted pages to the end of the directory or file, instead of replacing them (default=0)" + Environment.NewLine +
                "  -denoise          Runs a noise reduction algorithm on the images when extracting (default=0)" + Environment.NewLine +
                "  -whitebalance     Runs a white balancing algorithm on the images when extracting (default=0)" + Environment.NewLine +
                "  -flipX/Y          Flips the images when extracting (default=0)" + Environment.NewLine +
                "  -rot90/180/270    Rotates the images when extracting (default=0)" + Environment.NewLine +
                "  -metadata         Specify that metadata files (ComicInfo.xml) should also be extracted (default=0)" + Environment.NewLine +
                "  -pdf:height       When extracting to PDF, specify the height of each page in millimetres (default=260)" + Environment.NewLine +
                "  -pdf:width        When extracting to PDF, specify the width of each page in millimetres (default=auto)" + Environment.NewLine +
                "  -pdf:bleed        When extracting to PDF, specify the bleed margin of each page in millimetres (default=0)" + Environment.NewLine +
                Environment.NewLine +
                "CBZTool compress PATH... [options]" + Environment.NewLine +
                "  -o [file]         Specify the file to compress to (defaults to the input path with the .cbz extension appended)" + Environment.NewLine +
                "  -p [range]        Specify the range of pages to compress (ex: 1-10 2,4,6 7-*) (default=*)" + Environment.NewLine +
                "  -a                Appends the extracted pages to the end of the archive, instead of replacing it (default=0)" + Environment.NewLine +
                "  -metadata         Specify that metadata files (ComicInfo.xml) should also be added (default=0)"
            );
        }
       
        private static string[] ExpandWildcardPath(string path, bool includeFiles, bool includeDirs)
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

        public static void WaitForDebugger()
        {
            Console.WriteLine("Waiting for debugger to attach");
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(100);
            }
            Console.WriteLine("Debugger attached");
        }

        public static void Main(string[] args)
        {
            var arguments = new ProgramArguments(args);
            if(arguments.Count >= 2 && arguments.Get(0) == "extract")
            {
                // Extract one or more files:

                // Parse command line arguments
                var commonOutputPath = arguments.GetStringOption("o", null);
                if (commonOutputPath != null)
                {
                    commonOutputPath = commonOutputPath.Trim();
                }

                var pages = PageList.All;
                var pagesString = arguments.GetStringOption("p", null);
                if(pagesString != null && !PageList.TryParse(pagesString, out pages))
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
                if (arguments.GetBoolOption("flipX"))
                {
                    filters.Add(new RotateFlipFilter(RotateFlipType.RotateNoneFlipX));
                }
                if (arguments.GetBoolOption("flipY"))
                {
                    filters.Add(new RotateFlipFilter(RotateFlipType.RotateNoneFlipY));
                }
                if (arguments.GetBoolOption("rot90"))
                {
                    filters.Add(new RotateFlipFilter(RotateFlipType.Rotate90FlipNone));
                }
                if (arguments.GetBoolOption("rot180"))
                {
                    filters.Add(new RotateFlipFilter(RotateFlipType.Rotate180FlipNone));
                }
                if (arguments.GetBoolOption("rot270"))
                {
                    filters.Add(new RotateFlipFilter(RotateFlipType.Rotate270FlipNone));
                }

                var pdfExportOptions = new ComicArchive.PDFExportOptions();
                pdfExportOptions.PageHeightInMillimetres = arguments.GetDoubleOption("pdf:height", pdfExportOptions.PageHeightInMillimetres);
                if(arguments.GetDoubleOption("pdf:width", -1.0) > 0.0)
                {
                    pdfExportOptions.PageWidthInMillimetres = arguments.GetDoubleOption("pdf:width");
                }
                pdfExportOptions.BleedMarginInMillimetres = arguments.GetDoubleOption("pdf:bleed", pdfExportOptions.BleedMarginInMillimetres);

                var inputPaths = new List<string>();
                for (int i = 1; i < arguments.Count; ++i)
                {
                    foreach(var str in ExpandWildcardPath(arguments.Get(i).Trim(), true, false))
                    {
                        inputPaths.Add(str);
                    }
                }

                // Extract the files
                foreach (var inputPath in inputPaths)
                {
                    if (commonOutputPath != null)
                    {
                        if (Extraction.Extract(inputPath, pages, filters, commonOutputPath, append, includeMetadata, pdfExportOptions))
                        {
                            append = true; // If all files are being extracted to the same place, we don't want them to overwrite each other
                            includeMetadata = false; // We don't want more than one set of metadata to be added to the same file
                        }
                    }
                    else
                    {
                        var outputPath = Path.ChangeExtension(inputPath, null);
                        Extraction.Extract(inputPath, pages, filters, outputPath, append, includeMetadata, pdfExportOptions);
                    }
                }
            }
            else if(arguments.Count >= 2 && arguments.Get(0) == "compress")
            {
                // Compress one or more directories:

                // Parse command line arguments
                var commonOutputPath = arguments.GetStringOption("o", null);
                if(commonOutputPath != null)
                {
                    commonOutputPath = commonOutputPath.Trim();
                }

                var pages = PageList.All;
                var pagesString = arguments.GetStringOption("p", null);
                if (pagesString != null && !PageList.TryParse(pagesString, out pages))
                {
                    Console.WriteLine("Failed to parse page range {0}", pagesString);
                    return;
                }

                var append = arguments.GetBoolOption("a");
                var includeMetadata = arguments.GetBoolOption("metadata");

                var inputPaths = new List<string>();
                for (int i = 1; i < arguments.Count; ++i)
                {
                    foreach (var str in ExpandWildcardPath(arguments.Get(i).Trim(), true, true))
                    {
                        inputPaths.Add(str);
                    }
                }
                
                // Compress the directories
                foreach(var inputPath in inputPaths)
                {
                    if (commonOutputPath != null)
                    {
                        if (Compression.Compress(inputPath, pages, commonOutputPath, append, includeMetadata))
                        {
                            append = true; // If all directories are being compressed to the same file, we don't want their contents to overwrite each other
                            includeMetadata = false; // We don't want more than one set of metadata to be added to the same file
                        }
                    }
                    else
                    {
                        var outputFile = Path.ChangeExtension( inputPath, ".cbz" );
                        Compression.Compress(inputPath, pages, outputFile, append, includeMetadata);
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
