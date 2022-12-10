using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Dan200.CBZTool;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SevenZipExtractor;

namespace Dan200.CBZLib
{
    public enum ComicArchiveFormat
    {
        CBR,
        CBZ,
    }

    public enum ComicImageFormat
    {
        PNG,
        JPEG
    }

    public enum ComicArchiveMode
    {
        Read,
        Create,
        Modify,
    }

    public class ComicArchive : IDisposable
    {
        private string m_filePath;
        private ComicArchiveMode m_openMode;
        private ArchiveFile m_sevenZipArchive;
        private ZipArchive m_zipArchive;
        private List<string> m_pageIndex;
        private ComicMetadata m_metadata;
        private bool m_metadataLoaded;
        private bool m_metadataEverSaved;

        public string FilePath
        {
            get { return m_filePath; }
        }

        public ComicArchiveMode OpenMode
        {
            get
            {
                return m_openMode;
            }
        }

        public ComicMetadata Metadata
        {
            get
            {
                return LoadMetadata();
            }
            set
            {
                m_metadata = value;
                m_metadataLoaded = true;
            }
        }

        public int PageCount
        {
            get
            {
                return GetPageIndex().Count;
            }
        }

        private static ComicArchiveFormat GuessArchiveFormatFromExtension(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".cbz":
                    return ComicArchiveFormat.CBZ;
                case ".cbr":
                    return ComicArchiveFormat.CBR;
                default:
                    throw new NotSupportedException("Unknown archive format: " + extension);
            }
        }

        private static ComicImageFormat GuessImageFormatFromExtension(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".png":
                    return ComicImageFormat.PNG;
                case ".jpg":
                case ".jpeg":
                    return ComicImageFormat.JPEG;
                default:
                    throw new NotSupportedException("Unknown image format: " + extension);
            }
        }

        private static string GetDefaultExtensionForImageFormat(ComicImageFormat format)
        {
            switch(format)
            {
                case ComicImageFormat.PNG:
                    return ".png";
                case ComicImageFormat.JPEG:
                    return ".jpg";
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        public ComicArchive(string path, ComicArchiveMode openMode) : this(path, openMode, GuessArchiveFormatFromExtension(path))
        {
        }

        public ComicArchive(string path, ComicArchiveMode openMode, ComicArchiveFormat format)
        {
            m_filePath = path;
            m_openMode = openMode;
            switch (format)
            {
                case ComicArchiveFormat.CBZ:
                    LoadFromCBZ(path, openMode);
                    break;
                case ComicArchiveFormat.CBR:
                    LoadFromCBR(path, openMode);
                    break;
                default:
                    throw new ArgumentException(nameof(format));
            }
        }

        public override string ToString()
        {
            return Path.GetFileName(FilePath);
        }

        public void Dispose()
        {
            if (m_sevenZipArchive != null)
            {
                m_sevenZipArchive.Dispose();
                m_sevenZipArchive = null;
            }
            if (m_zipArchive != null)
            {
                m_zipArchive.Dispose();
                m_zipArchive = null;
            }
        }

        public ComicImageFormat GetPageImageFormat(int pageNum)
        {
            string entryPath = GetPageEntryPath(pageNum);
            return GuessImageFormatFromExtension(entryPath);
        }

        public string GetPageFileName(int pageNum)
        {
            string entryPath = GetPageEntryPath(pageNum);
            return Path.GetFileName(entryPath);
        }

        public Bitmap ExtractPageAsBitmap(int pageNum)
        {
            // Check arguments
            if(m_openMode == ComicArchiveMode.Create)
            {
                throw new InvalidOperationException("This archive is not readable");
            }

            // Read the image
            string entryPath = GetPageEntryPath(pageNum);
            var inputStream = OpenEntryForRead(entryPath);
            if (!(inputStream is MemoryStream))
            {
                // Image.FromStream requires you to keep the original stream open for the lifetime of the image
                // This is a problem, as we need to close the input stream now so we can keep editing the archive.
                // To get around this: copy the inputStream into a MemoryStream now, then close the original.
                // If OpenEntryForRead already returned a MemoryStream, we can skip this.
                var memoryStream = new MemoryStream();
                try
                {
                    inputStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                }
                finally
                {
                    inputStream.Close();
                }
                inputStream = memoryStream;
            }
            return (Bitmap)Image.FromStream(inputStream, false);
        }

        public void ExtractPageToFile(int pageNum, string path)
        {
            string entryPath = GetPageEntryPath(pageNum);
            ExtractEntryToFile(entryPath, path);
        }

        public void ExtractPagesToDirectory(string path, PageList pages)
        {
            // Check arguments
            if (m_openMode == ComicArchiveMode.Create)
            {
                throw new InvalidOperationException("This archive is not readable");
            }

            // Build a list of entries to export
            var entriesToExport = GetPageEntryPaths(pages);

            // Build a list of filenames to export to
            var directoryIndex = ComicExtractUtils.GetImagesInDirectory(path);
            var outputPaths = ComicExtractUtils.GenerateNewImagePaths(directoryIndex, directoryIndex.Count + 1, entriesToExport.Count, "", path);

            // Start extracting the pages
            int nextPageIndex = 0;
            foreach (string entryPath in entriesToExport)
            {
                string outputExtension = Path.GetExtension(entryPath);
                string outputPath = outputPaths[nextPageIndex] + outputExtension;
                ExtractEntryToFile(entryPath, outputPath);
                nextPageIndex++;
            }
        }

        public class PDFExportOptions
        {
            public double PageHeightInMillimetres;
            public double? PageWidthInMillimetres; // null = automatic
            public double HorizontalBleedInMillimetres;
            public double VerticalBleedInMillimetres;
            public bool Stretch;
            public double XAlign;
            public double YAlign;
            public bool GenerateContentsList;
            public bool AppendToExistingFile;

            public PDFExportOptions()
            {
                PageHeightInMillimetres = 260.0; // Standard US comic height
                PageWidthInMillimetres = null;
                HorizontalBleedInMillimetres = 0.0;
                VerticalBleedInMillimetres = 0.0;
                Stretch = true;
                XAlign = 0.5;
                YAlign = 0.5;
                GenerateContentsList = false;
                AppendToExistingFile = false;
            }

            public PDFExportOptions(PDFExportOptions other)
            {
                PageHeightInMillimetres = other.PageHeightInMillimetres;
                PageWidthInMillimetres = other.PageWidthInMillimetres;
                HorizontalBleedInMillimetres = other.HorizontalBleedInMillimetres;
                VerticalBleedInMillimetres = other.VerticalBleedInMillimetres;
                Stretch = other.Stretch;
                XAlign = other.XAlign;
                YAlign = other.YAlign;
                GenerateContentsList = other.GenerateContentsList;
                AppendToExistingFile = other.AppendToExistingFile;
            }
        }

        public void ExtractPagesToPDF(string path, PageList pages, PDFExportOptions options)
        {
            // Check arguments
            if (m_openMode == ComicArchiveMode.Create)
            {
                throw new InvalidOperationException("This archive is not readable");
            }

            // Build a list of entries to export
            var entriesToExport = GetPageEntryPaths(pages);

            // Create the output directory
            var directory = Path.GetDirectoryName(path);
            ComicExtractUtils.EnsureDirectoryExists(directory);

            // Start the export            
            using (var document = options.AppendToExistingFile ? PdfReader.Open(path, PdfDocumentOpenMode.Modify) : new PdfDocument())
            {
                // Setup some info
                document.PageLayout = PdfPageLayout.TwoColumnRight;

                // Add pages
                var previousPageCount = document.PageCount;
                foreach (string entryPath in entriesToExport)
                {
                    using (var imageStream = OpenEntryForRead(entryPath))
                    {
                        using (var image = XImage.FromStream(imageStream))
                        {
                            var page = document.AddPage();

                            // Set the page dimensions and bleed
                            var imageAspectRatio = (double)image.PixelWidth / (double)image.PixelHeight;
                            page.Height = new XUnit(options.PageHeightInMillimetres, XGraphicsUnit.Millimeter);
                            page.TrimMargins.Left = new XUnit(options.HorizontalBleedInMillimetres, XGraphicsUnit.Millimeter);
                            page.TrimMargins.Right = new XUnit(options.HorizontalBleedInMillimetres, XGraphicsUnit.Millimeter);
                            page.TrimMargins.Top = new XUnit(options.VerticalBleedInMillimetres, XGraphicsUnit.Millimeter);
                            page.TrimMargins.Bottom = new XUnit(options.VerticalBleedInMillimetres, XGraphicsUnit.Millimeter);
                            var totalPageHeight = page.Height + page.TrimMargins.Top + page.TrimMargins.Bottom;
                            if (options.PageWidthInMillimetres.HasValue)
                            {
                                page.Width = new XUnit(options.PageWidthInMillimetres.Value, XGraphicsUnit.Millimeter);
                            }
                            else
                            {
                                page.Width = (totalPageHeight * imageAspectRatio) - page.TrimMargins.Left - page.TrimMargins.Right;
                            }
                            var totalPageWidth = page.Width + page.TrimMargins.Left + page.TrimMargins.Right;

                            // Determine the image size
                            XUnit imageHeight, imageWidth;
                            if (options.Stretch)
                            {
                                imageHeight = totalPageHeight;
                                imageWidth = totalPageWidth;
                            }
                            else
                            {
                                imageHeight = totalPageHeight;
                                imageWidth = imageHeight * imageAspectRatio;
                                if (imageWidth > totalPageWidth)
                                {
                                    imageWidth = totalPageWidth;
                                    imageHeight = imageWidth / imageAspectRatio;
                                }
                            }

                            // Draw the image
                            using (var gfx = XGraphics.FromPdfPage(page))
                            {
                                gfx.DrawImage
                                (
                                    image,
                                    options.XAlign * (totalPageWidth - imageWidth) - page.TrimMargins.Left,
                                    options.YAlign * (totalPageHeight - imageHeight) - page.TrimMargins.Top,
                                    imageWidth,
                                    imageHeight
                                );
                            }
                        }
                    }
                }

                // Get metadata
                var metadata = LoadMetadata();
                if (metadata != null)
                {
                    // Trim metadata
                    if(pages != PageList.All)
                    {
                        metadata = metadata.Trim(pages, PageCount);
                    }

                    // Store document info
                    document.Info.Title = metadata.ToString();
                    if (metadata.Publisher != null)
                    {
                        document.Info.Author = metadata.Publisher;
                    }

                    // Store contents
                    if (metadata.Contents.Count > 0 && document.Pages.Count > 0 && options.GenerateContentsList)
                    {
                        foreach (var content in metadata.Contents)
                        {
                            if (content.Pages != null)
                            {
                                int firstPageNumber = previousPageCount + content.Pages.First;
                                if (firstPageNumber >= 1 && firstPageNumber <= document.Pages.Count)
                                {
                                    var title = content.ToString();
                                    document.Outlines.Add(title, document.Pages[firstPageNumber - 1]);
                                }
                            }
                        }
                    }
                }

                // Save
                document.Save(path);
            }
        }

        public void AddPageFromBitmap(Bitmap bitmap, ComicImageFormat format)
        {
            AddPageFromBitmap(PageCount + 1, bitmap, format);
        }

        public void AddPageFromBitmap(int pageNumber, Bitmap bitmap, ComicImageFormat format)
        {
            // Check arguments
            if (m_openMode == ComicArchiveMode.Read)
            {
                throw new InvalidOperationException("This archive is not writable");
            }
            if (pageNumber < 1 || pageNumber > PageCount + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            // Generate a path for the new page
            var pageIndex = GetPageIndex();
            var entryPath = ComicExtractUtils.GenerateNewImagePaths(pageIndex, pageNumber, 1, GetDefaultExtensionForImageFormat(format)).First();

            // Move other pages out of the way
            if (pageNumber <= pageIndex.Count)
            {
                if(m_openMode == ComicArchiveMode.Create)
                {
                    throw new InvalidOperationException("New archives can only be appended to");
                }
                RenamePagesForInsert(pageNumber, entryPath);
            }

            // Add the page
            using (var outputStream = CreateNewEntry(entryPath))
            {
                switch(format)
                {
                    case ComicImageFormat.PNG:
                        bitmap.Save(outputStream, ImageFormat.Png);
                        break;
                    case ComicImageFormat.JPEG:
                        bitmap.Save(outputStream, ImageFormat.Jpeg);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(format));
                }
            }
            pageIndex.Insert(pageNumber - 1, entryPath);
        }

        public void AddPageFromFile(string path)
        {
            AddPageFromFile(PageCount + 1, path);
        }

        public void AddPageFromFile(int pageNumber, string path)
        {
            // Check arguments
            if (m_openMode == ComicArchiveMode.Read)
            {
                throw new InvalidOperationException("This archive is not writable");
            }
            if (!ComicExtractUtils.IsImageFilePath(path))
            {
                throw new ArgumentException(string.Format("{0} is not a valid image path", path));
            }
            if (pageNumber < 1 || pageNumber > PageCount + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            // Generate a path for the new page
            var pageIndex = GetPageIndex();
            var entryPath = ComicExtractUtils.GenerateNewImagePaths(pageIndex, pageNumber, 1, Path.GetExtension(path)).First();

            // Move other pages out of the way
            if (pageNumber <= pageIndex.Count)
            {
                if (m_openMode == ComicArchiveMode.Create)
                {
                    throw new InvalidOperationException("New pages may only be added to the end of archives opened with ComicArchiveMode.Create");
                }
                RenamePagesForInsert(pageNumber, entryPath);
            }

            // Add the page
            CreateNewEntryFromFile(entryPath, path);
            pageIndex.Insert(pageNumber - 1, entryPath);
        }

        public void AddPagesFromDirectory(string path, PageList pages)
        {
            AddPagesFromDirectory(PageCount + 1, path, pages);
        }

        public void AddPagesFromDirectory(int pageNumber, string path, PageList pages)
        {
            // Check arguments
            if (m_openMode == ComicArchiveMode.Read)
            {
                throw new InvalidOperationException("This archive is not writable");
            }
            if (pageNumber < 1 || pageNumber > PageCount + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            // Build a list of files to add
            var directoryIndex = ComicExtractUtils.GetImagesInDirectory(path);
            var pathsToAdd = GetPagePaths(directoryIndex, pages);
            if(pathsToAdd.Count == 0)
            {
                return;
            }

            // Build a list of entry names for the new files
            var pageIndex = GetPageIndex();
            var entryPaths = ComicExtractUtils.GenerateNewImagePaths(pageIndex, pageNumber, pathsToAdd.Count, "");

            // Move other pages out of the way
            if (pageNumber <= pageIndex.Count)
            {
                if (m_openMode == ComicArchiveMode.Create)
                {
                    throw new InvalidOperationException("New pages may only be added to the end of archives opened with ComicArchiveMode.Create");
                }
                RenamePagesForInsert(pageNumber, entryPaths.Last());
            }

            // Start adding the pages
            int nextPageIndex = 0;
            pageIndex.Capacity += entryPaths.Count;
            foreach (string inputPath in pathsToAdd)
            {
                string inputExtension = Path.GetExtension(inputPath);
                string entryPath = entryPaths[nextPageIndex] + inputExtension;
                CreateNewEntryFromFile(entryPath, inputPath);
                pageIndex.Insert((pageNumber + nextPageIndex) - 1, entryPath);
                nextPageIndex++;
            }
        }

        public void AddPagesFromComic(ComicArchive comic, PageList pages)
        {
            AddPagesFromComic(PageCount + 1, comic, pages);
        }

        public void AddPagesFromComic(int pageNumber, ComicArchive comic, PageList pages)
        {
            // Check arguments
            if (m_openMode == ComicArchiveMode.Read)
            {
                throw new InvalidOperationException("This archive is not writable");
            }
            if (pageNumber < 1 || pageNumber > PageCount + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            // Build a list of pages to copy from the other comic
            var pagesToCopy = comic.GetPageEntryPaths(pages);
            if(pagesToCopy.Count == 0)
            {
                return;
            }

            // Build a list of entry names for the new pages
            var pageIndex = GetPageIndex();
            var entryPaths = ComicExtractUtils.GenerateNewImagePaths(pageIndex, pageNumber, pagesToCopy.Count, "");

            // Move other pages out of the way
            if (pageNumber <= pageIndex.Count)
            {
                if (m_openMode == ComicArchiveMode.Create)
                {
                    throw new InvalidOperationException("New pages may only be added to the end of archives opened with ComicArchiveMode.Create");
                }
                RenamePagesForInsert(pageNumber, entryPaths.Last());
            }

            // Start copying the pages
            int nextPageIndex = 0;
            pageIndex.Capacity += entryPaths.Count;
            foreach (string inputPath in pagesToCopy)
            {
                string inputExtension = Path.GetExtension(inputPath);
                string entryPath = entryPaths[nextPageIndex] + inputExtension;
                using (var inputStream = comic.OpenEntryForRead(inputPath))
                {
                    using (var outputStream = CreateNewEntry(entryPath))
                    {
                        inputStream.CopyTo(outputStream);
                    }
                }
                pageIndex.Insert((pageNumber + nextPageIndex) - 1, entryPath);
                nextPageIndex++;
            }
        }

        public void DeletePage(int pageNumber)
        {
            // Check permissions
            if (m_openMode != ComicArchiveMode.Modify)
            {
                throw new InvalidOperationException("This archive is not writable");
            }
            if (pageNumber < 1 || pageNumber > PageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            // Delete the page
            DeletePages(new PageList(pageNumber));
        }

        public void DeletePages(PageList pages)
        {
            // Check permissions
            if (m_openMode != ComicArchiveMode.Modify)
            {
                throw new InvalidOperationException("This archive is not writable");
            }

            // Delete the pages
            var pageIndex = GetPageIndex();
            var pagePaths = GetPagePaths(pageIndex, pages);
            foreach(var path in pagePaths)
            {
                DeleteEntry(path);
                pageIndex.Remove(path);
            }
        }

        private void LoadFromCBR(string path, ComicArchiveMode openMode)
        {
            switch(openMode)
            {
                case ComicArchiveMode.Read:
                {
                    // Open a file for reading
                    m_sevenZipArchive = new ArchiveFile(path);
                    break;
                }
                case ComicArchiveMode.Create:
                case ComicArchiveMode.Modify:
                {
                    // Not supported
                    throw new NotSupportedException("Writing to CBR files is not supported");
                }
                default:
                {
                    throw new ArgumentException(nameof(openMode));
                }
            }
        }

        private void LoadFromCBZ(string path, ComicArchiveMode openMode)
        {
            switch(openMode)
            {
                case ComicArchiveMode.Read:
                {
                    // Open a file for reading
                    // Note: We are using SevenZip rather than System.IO.Compression reader here
                    // This is because .cbz files have been found in the wild which are mislabelled CBR files, and SevenZip can read from both
                    // (It can't write to anything though, which is why we still need System.IO.Compression for the other use cases)
                    m_sevenZipArchive = new ArchiveFile(path);
                    break;
                }
                case ComicArchiveMode.Create:
                {
                    // Create a new file
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    else
                    {
                        var directory = Path.GetDirectoryName(path);
                        ComicExtractUtils.EnsureDirectoryExists(directory);
                    }
                    m_zipArchive = ZipFile.Open(path, ZipArchiveMode.Create);
                    m_pageIndex = new List<string>();
                    m_metadataLoaded = true;
                    break;
                }
                case ComicArchiveMode.Modify:
                {
                    // Open an existing file, or create a new one if it does not yet exist
                    var directory = Path.GetDirectoryName(path);
                    ComicExtractUtils.EnsureDirectoryExists(directory);
                    m_zipArchive = ZipFile.Open(path, ZipArchiveMode.Update);
                    break;
                }
                default:
                {
                    throw new ArgumentException(nameof(openMode));
                }
            }
        }

        public void StoreMetadataChanges()
        {
            if (m_openMode == ComicArchiveMode.Read)
            {
                throw new InvalidOperationException("This archive is not writable");
            }
            if(m_openMode == ComicArchiveMode.Create && m_metadataEverSaved)
            {
                throw new InvalidOperationException("Metadata may only be saved one time for archives opened with ComicArchiveMode.Create");
            }
            var metadata = LoadMetadata();
            if (metadata != null)
            {
                // Replace metadata
                if (m_openMode == ComicArchiveMode.Modify)
                {
                    DeleteEntry("ComicInfo.xml");
                    DeleteEntry("tag.txt");
                }
                using (var stream = CreateNewEntry("ComicInfo.xml"))
                {
                    metadata.SaveAsComicInfoFile(stream);
                }
            }
            else
            {
                // Delete metadata
                if (m_openMode == ComicArchiveMode.Modify)
                {
                    DeleteEntry("ComicInfo.xml");
                    DeleteEntry("tag.txt");
                }
            }
            m_metadataEverSaved = true;
        }

        public void RevertMetadataChanges()
        {
            m_metadata = null;
            m_metadataLoaded = false;
        }

        private ComicMetadata LoadMetadata()
        {
            if (!m_metadataLoaded)
            {
                if (HasEntry("ComicInfo.xml"))
                {
                    using (var stream = OpenEntryForRead("ComicInfo.xml"))
                    {
                        m_metadata = ComicMetadata.FromComicInfoFile(stream);
                    }
                }
                else if (HasEntry("tag.txt"))
                {
                    using (var stream = OpenEntryForRead("tag.txt"))
                    {
                        m_metadata = ComicMetadata.FromTagFile(stream);
                    }
                }
                m_metadataLoaded = true;
            }
            return m_metadata;
        }

        private List<string> GetPageIndex()
        {
            if (m_pageIndex == null)
            {
                if (m_sevenZipArchive != null)
                {
                    m_pageIndex = m_sevenZipArchive.Entries.Select(entry => entry.FileName).Where(entryPath => ComicExtractUtils.IsImageFilePath(entryPath)).ToList();
                    m_pageIndex.Sort(AlphaNumericComparator.Instance);
                }
                else
                {
                    m_pageIndex = m_zipArchive.Entries.Select(entry => entry.FullName).Where(entryPath => ComicExtractUtils.IsImageFilePath(entryPath)).ToList();
                    m_pageIndex.Sort(AlphaNumericComparator.Instance);
                }
            }
            return m_pageIndex;
        }

        private string GetPageEntryPath(int pageNum)
        {
            var pageIndex = GetPageIndex();
            if (pageNum < 1 || pageNum > pageIndex.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNum));
            }
            return pageIndex[pageNum - 1];
        }

        private List<string> GetPageEntryPaths(PageList pages)
        {
            return GetPagePaths(GetPageIndex(), pages);
        }

        private List<string> GetPagePaths(List<string> pageIndex, PageList pages)
        {
            var results = new List<string>();
            foreach (PageRange subRange in pages.SubRanges)
            {
                int firstPage = subRange.First;
                int lastPage = Math.Min(subRange.Last, pageIndex.Count);
                for (int pageNum = firstPage; pageNum <= lastPage; ++pageNum)
                {
                    results.Add(pageIndex[pageNum - 1]);
                }
            }
            return results;
        }

        private void RenamePagesForInsert(int firstPageToRename, string previousPagePath)
        {
            var pageIndex = GetPageIndex();
            var numEntriesToMove = (pageIndex.Count - firstPageToRename) + 1;
            var newEntryPaths = ComicExtractUtils.GenerateNewImagePaths(previousPagePath, numEntriesToMove, "");
            for (int pageNumber = pageIndex.Count; pageNumber >= firstPageToRename; --pageNumber)
            {
                var oldPath = pageIndex[pageNumber - 1];
                var newPath = newEntryPaths[pageNumber - firstPageToRename] + Path.GetExtension(oldPath);
                MoveEntry(oldPath, newPath);
                pageIndex[pageNumber - 1] = newPath;
            }
        }

        private bool HasEntry(string entryPath)
        {
            if (m_sevenZipArchive != null)
            {
                var entryToExtract = m_sevenZipArchive.Entries.Where(entry => entry.FileName.Equals(entryPath)).FirstOrDefault();
                return entryToExtract != null;
            }
            else
            {
                var entryToExtract = m_zipArchive.Entries.Where(entry => entry.FullName.Equals(entryPath)).FirstOrDefault();
                return entryToExtract != null;
            }
        }

        private Stream OpenEntryForRead(string entryPath)
        {
            if(m_sevenZipArchive != null)
            {
                var entryToExtract = m_sevenZipArchive.Entries.Where(entry => entry.FileName.Equals(entryPath)).FirstOrDefault();
                if (entryToExtract != null)
                {
                    var memoryStream = new MemoryStream();
                    entryToExtract.Extract(memoryStream);
                    memoryStream.Position = 0;
                    return memoryStream;
                }
            }
            else
            {
                var entryToExtract = m_zipArchive.Entries.Where(entry => entry.FullName.Equals(entryPath)).FirstOrDefault();
                if(entryToExtract != null )
                {
                    return entryToExtract.Open();
                }
            }
            return null;
        }

        private void ExtractEntryToFile(string entryPath, string outputPath)
        {
            // Create the output directory
            var directory = Path.GetDirectoryName(outputPath);
            ComicExtractUtils.EnsureDirectoryExists(directory);

            // Extract the file
            if (m_sevenZipArchive != null)
            {
                var entryToExtract = m_sevenZipArchive.Entries.Where(entry => entry.FileName.Equals(entryPath, StringComparison.InvariantCultureIgnoreCase)).First();
                entryToExtract.Extract(outputPath);
            }
            else
            {
                var entryToExtract = m_zipArchive.Entries.Where(entry => entry.FullName.Equals(entryPath, StringComparison.InvariantCultureIgnoreCase)).First();
                entryToExtract.ExtractToFile(outputPath, true);
            }
        }

        private Stream CreateNewEntry(string entryPath)
        {
            if(m_sevenZipArchive != null)
            {
                throw new NotSupportedException("Writing to CBR files is not supported");
            }
            else
            {
                return m_zipArchive.CreateEntry(entryPath).Open();
            }
        }

        private void CreateNewEntryFromFile(string entryPath, string inputPath)
        {
            if (m_sevenZipArchive != null)
            {
                throw new NotSupportedException("Writing to CBR files is not supported");
            }
            else
            {
                m_zipArchive.CreateEntryFromFile(inputPath, entryPath);
            }
        }

        private void MoveEntry(string entryPath, string newEntryPath)
        {
            if (m_sevenZipArchive != null)
            {
                throw new NotSupportedException("Writing to CBR files is not supported");
            }
            else
            {
                var entryToMove = m_zipArchive.Entries.Where(entry => entry.FullName.Equals(entryPath, StringComparison.InvariantCultureIgnoreCase)).First();
                using (var entryStream = entryToMove.Open())
                {
                    using (var newEntryStream = m_zipArchive.CreateEntry(newEntryPath).Open())
                    {
                        entryStream.CopyTo(newEntryStream);
                    }
                }
                entryToMove.Delete();
            }
        }

        private void DeleteEntry(string entryPath)
        {
            if (m_sevenZipArchive != null)
            {
                throw new NotSupportedException("Writing to CBR files is not supported");
            }
            else
            {
                var entryToDelete = m_zipArchive.Entries.Where(entry => entry.FullName.Equals(entryPath, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                if (entryToDelete != null)
                {
                    entryToDelete.Delete();
                }
            }
        }
    }
}
