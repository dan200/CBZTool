using Dan200.CBZLib;
using Dan200.CBZTool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBZTool
{
    internal static class CompressCommand
    {
        private static bool Compress_ImageFileToComic(string inputPath, string outputPath, bool append)
        {
            using(var outputComic = new ComicArchive(outputPath, append ? ComicArchiveMode.Modify : ComicArchiveMode.Create))
            {
                outputComic.AddPageFromFile(inputPath);
            }
            return true;
        }

        private static bool Compress_DirectoryToComic(string inputPath, PageList pages, string outputPath, bool append, bool includeMetadata)
        {
            using (var outputComic = new ComicArchive(outputPath, append ? ComicArchiveMode.Modify : ComicArchiveMode.Create))
            {
                var oldPageCount = outputComic.PageCount;
                outputComic.AddPagesFromDirectory(inputPath, pages);

                if (includeMetadata)
                {
                    var metadataPath = Path.Combine(inputPath, "ComicInfo.xml");
                    if(File.Exists(metadataPath))
                    {
                        var inputMetadata = ComicMetadata.FromComicInfoFile(metadataPath);
                        if(pages != PageList.All)
                        {
                            inputMetadata = inputMetadata.Trim(pages, ComicExtractUtils.GetImagesInDirectory(inputPath).Count);
                        }
                        if (append && outputComic.Metadata != null)
                        {
                            outputComic.Metadata.Append(inputMetadata, oldPageCount);
                        }
                        else
                        {
                            inputMetadata.MovePagesBy(oldPageCount);
                            outputComic.Metadata = inputMetadata;
                        }
                        outputComic.StoreMetadataChanges();
                    }
                }
            }
            return true;
        }

        public static bool Compress(string inputPath, PageList pages, string outputPath, bool append, bool includeMetadata)
        {
            var outputExtension = Path.GetExtension(outputPath);
            if (outputExtension.Equals(".cbz", StringComparison.InvariantCultureIgnoreCase))
            {
                // Create a comic archive...
                if (Directory.Exists(inputPath))
                {
                    // ...from a directory
                    Console.WriteLine("Compressing pages {0} from {1} to {2}", pages, inputPath, outputPath);
                    return Compress_DirectoryToComic(inputPath, pages, outputPath, append, includeMetadata);
                }
                else if (File.Exists(inputPath))
                {
                    // ...from a single image file
                    var inputExtension = Path.GetExtension(inputPath);
                    if (inputExtension.Equals(".png", StringComparison.InvariantCultureIgnoreCase) || inputExtension.Equals(".jpg", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Console.WriteLine("Compressing {0} to {1}", inputPath, outputPath);
                        return Compress_ImageFileToComic(inputPath, outputPath, append);
                    }
                    else
                    {
                        Console.WriteLine("Unsupported input file type: {0}", inputPath);
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("No such path: {0}", inputPath);
                    return false;
                }
            }
            else
            {
                Console.WriteLine("Unsupported output file type: {0}", outputPath);
                return false;
            }
        }
    }
}
