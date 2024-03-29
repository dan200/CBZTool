﻿using Dan200.CBZLib;
using Dan200.CBZTool;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dan200.CBZTool
{
    internal static class ExtractCommand
    {
        private static bool Extract_ComicToDirectory(string inputPath, PageList pages, IList<IImageFilter> filters, string outputPath, bool append, bool includeMetadata, bool splitWidePages)
        {
            using (var inputComic = new ComicArchive(inputPath, ComicArchiveMode.Read))
            {
                // Delete existing files
                List<string> existingPages;
                if (append)
                {
                    existingPages = ComicExtractUtils.GetImagesInDirectory(outputPath);
                }
                else
                {
                    ComicExtractUtils.DeleteAllImagesInDirectory(outputPath);
                    existingPages = new List<string>();
                }

                // Extract the pages
                if(filters.Count == 0)
                {
                    inputComic.ExtractPagesToDirectory(outputPath, pages, splitWidePages);
                }
                else
                {
                    ComicExtractUtils.EnsureDirectoryExists(outputPath);

                    var pagesToExtract = new List<int>();
                    foreach (var subRange in pages.SubRanges)
                    {
                        for (int pageNum = subRange.First; pageNum <= Math.Min(subRange.Last, inputComic.PageCount); ++pageNum)
                        {
                            pagesToExtract.Add(pageNum);
                        }
                    }

                    string previousImagePath = existingPages.LastOrDefault();
                    var numPathsToGenerate = (splitWidePages ? 2 : 1) * pagesToExtract.Count; // make sure we generate as many paths as we might need in our worst case
                    var outputPaths = ComicExtractUtils.GenerateNewImagePaths(previousImagePath, numPathsToGenerate, ".png", outputPath);
                    var outputPathIndex = 0;
                    for(int i=0; i<pagesToExtract.Count; ++i)
                    {
                        int pageNumber = pagesToExtract[i];
                        string newImagePath = outputPaths[outputPathIndex++];
                        using (var bitmap = inputComic.ExtractPageAsBitmap(pageNumber))
                        {
                            foreach(var filter in filters)
                            {
                                filter.ApplyTo(bitmap);
                            }

                            Bitmap leftPage, rightPage;
                            if (splitWidePages && ComicExtractUtils.SplitWideImage(bitmap, out leftPage, out rightPage))
                            {
                                try
                                {
                                    string leftImagePath = newImagePath;
                                    string rightImagePath = outputPaths[outputPathIndex++];
                                    leftPage.Save(leftImagePath);
                                    rightPage.Save(rightImagePath);
                                }
                                finally
                                {
                                    leftPage.Dispose();
                                    rightPage.Dispose();
                                }
                            }
                            else
                            {
                                bitmap.Save(newImagePath);
                            }
                        }
                    }
                }

                // Extract metadata
                if(includeMetadata && inputComic.Metadata != null)
                {
                    var metadata = inputComic.Metadata;
                    if(pages != PageList.All)
                    {
                        metadata = metadata.Trim(pages, inputComic.PageCount);
                    }

                    var metadataPath = Path.Combine(outputPath, "ComicInfo.xml");
                    if (append && File.Exists(metadataPath))
                    {
                        var existingMetadata = ComicMetadata.FromComicInfoFile(metadataPath);
                        existingMetadata.Append(metadata, existingPages.Count);
                        existingMetadata.SaveAsComicInfoFile(metadataPath);
                    }
                    else
                    {
                        metadata.MovePagesBy(existingPages.Count);
                        metadata.SaveAsComicInfoFile(metadataPath);
                    }
                }
            }
            return true;
        }

        private static bool Extract_ComicToComic(string inputPath, PageList pages, IList<IImageFilter> filters, string outputPath, bool append, bool includeMetadata)
        {
            using (var inputComic = new ComicArchive(inputPath, ComicArchiveMode.Read))
            {
                using (var outputComic = new ComicArchive(outputPath, append ? ComicArchiveMode.Modify : ComicArchiveMode.Create))
                {
                    // Copy pages
                    var oldPageCount = outputComic.PageCount;
                    if (filters.Count == 0)
                    {
                        outputComic.AddPagesFromComic(inputComic, pages);
                    }
                    else
                    {
                        foreach (var subRange in pages.SubRanges)
                        {
                            for (int pageNum = subRange.First; pageNum <= Math.Min(subRange.Last, inputComic.PageCount); ++pageNum)
                            {
                                using (var bitmap = inputComic.ExtractPageAsBitmap(pageNum))
                                {
                                    foreach (var filter in filters)
                                    {
                                        filter.ApplyTo(bitmap);
                                    }
                                    outputComic.AddPageFromBitmap(bitmap, ComicImageFormat.PNG); // TODO
                                }
                            }
                        }
                    }

                    // Copy metadata
                    if(includeMetadata && inputComic.Metadata != null)
                    {
                        var inputMetadata = inputComic.Metadata;
                        if(pages != PageList.All)
                        {
                            inputMetadata = inputMetadata.Trim(pages, inputComic.PageCount);
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

        private static bool Extract_ComicToPDF(string inputPath, PageList pages, string outputPath, bool append, bool includeMetadata, ComicArchive.PDFExportOptions pdfOptions)
        {
            using (var inputComic = new ComicArchive(inputPath, ComicArchiveMode.Read))
            {
                // Copy pages
                var pdfOptionsCopy = new ComicArchive.PDFExportOptions(pdfOptions);
                pdfOptionsCopy.AppendToExistingFile |= append;
                pdfOptionsCopy.GenerateContentsList |= includeMetadata;
                inputComic.ExtractPagesToPDF(outputPath, pages, pdfOptionsCopy);
            }
            return true;
        }

        private static bool Extract_ImageFileToDirectory(string inputPath, IList<IImageFilter> filters, string outputPath, bool append, bool splitWidePages)
        {
            // IMPORTANT NOTE:
            // This feature is quite inelegant, and doesn't match the meaning of "extract" and "compress" that exists in the rest of the program.
            // However, we need it right now for STGNC.
            // TODO: Remove this entirely once we have a proper templating system we can use instead.

            // Handle existing images in the directory 
            ComicExtractUtils.EnsureDirectoryExists(outputPath);
            if (!append)
            {
                ComicExtractUtils.DeleteAllImagesInDirectory(outputPath);
            }
            var lastImageInDirectory = ComicExtractUtils.GetImagesInDirectory(outputPath).LastOrDefault();

            // Perform the extraction
            var outputImageExtension = (filters.Count > 0) ? ".png" : Path.GetExtension(inputPath);
            var outputImagePaths = ComicExtractUtils.GenerateNewImagePaths(lastImageInDirectory, splitWidePages ? 2 : 1, outputImageExtension, outputPath);
            if (filters.Count > 0 || splitWidePages)
            {
                using (var bitmap = (Bitmap)Image.FromFile(inputPath))
                {
                    foreach (var filter in filters)
                    {
                        filter.ApplyTo(bitmap);
                    }

                    Bitmap leftPage, rightPage;
                    if (splitWidePages && ComicExtractUtils.SplitWideImage(bitmap, out leftPage, out rightPage))
                    {
                        try
                        {
                            leftPage.Save(outputImagePaths[0]);
                            rightPage.Save(outputImagePaths[1]);
                        }
                        finally
                        {
                            leftPage.Dispose();
                            rightPage.Dispose();
                        }
                    }
                    else
                    {
                        bitmap.Save(outputImagePaths[0]);
                    }
                }
            }
            else
            {
                File.Copy(inputPath, outputImagePaths[0]);
            }

            return true;
        }

        public static bool Extract(string inputPath, PageList pages, IList<IImageFilter> filters, string outputPath, bool append, bool includeMetadata, ComicArchive.PDFExportOptions pdfOptions, bool splitWidePages)
        {
            if (File.Exists(inputPath))
            {
                var inputExtension = Path.GetExtension(inputPath);
                if (inputExtension.Equals(".cbr", StringComparison.InvariantCultureIgnoreCase) || inputExtension.Equals(".cbz", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Extract a comic archive...
                    var outputExtension = Path.GetExtension(outputPath);
                    if (outputExtension.Equals("", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // ...to a directory
                        Console.WriteLine("Extracting pages {0} from {1} to {2}", pages, inputPath, outputPath);
                        return Extract_ComicToDirectory(inputPath, pages, filters, outputPath, append, includeMetadata, splitWidePages);
                    }
                    else if (outputExtension.Equals(".cbz", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // ...to another comic file
                        if (splitWidePages)
                        {
                            Console.WriteLine("-splitWidePages is not not supported when extracting to CBZ");
                            return false;
                        }
                        else
                        {
                            Console.WriteLine("Extracting pages {0} from {1} to {2}", pages, inputPath, outputPath);
                            return Extract_ComicToComic(inputPath, pages, filters, outputPath, append, includeMetadata);
                        }
                    }
                    else if (outputExtension.Equals(".pdf", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // ...to a PDF file
                        if (splitWidePages)
                        {
                            Console.WriteLine("-splitWidePages is not not supported when extracting to PDF");
                            return false;
                        }
                        else if (filters.Count > 0)
                        {
                            Console.WriteLine("Image filters are not supported when extracting to PDF");
                            return false;
                        }
                        else
                        {
                            Console.WriteLine("Extracting pages {0} from {1} to {2}", pages, inputPath, outputPath);
                            return Extract_ComicToPDF(inputPath, pages, outputPath, append, includeMetadata, pdfOptions);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unsupported output file type: {0}", outputPath);
                        return false;
                    }
                }
                else if (inputExtension.Equals(".jpg", StringComparison.InvariantCultureIgnoreCase) || inputExtension.Equals(".png", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Extract an image file...
                    var outputExtension = Path.GetExtension(outputPath);
                    if (outputExtension.Equals("", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // ...to a directory
                        Console.WriteLine("Extracting {0} to {1}", inputPath, outputPath);
                        return Extract_ImageFileToDirectory(inputPath, filters, outputPath, append, splitWidePages);
                    }
                    else
                    {
                        Console.WriteLine("Unsupported output file type: {0}", outputPath);
                        return false;
                    }
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
    }
}
