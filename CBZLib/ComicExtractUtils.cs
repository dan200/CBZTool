using Dan200.CBZTool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dan200.CBZLib
{
    public static class ComicExtractUtils
    {
        public static bool IsImageFilePath(string path)
        {
            var extension = Path.GetExtension(path);
            return 
                extension.Equals(".png", StringComparison.InvariantCultureIgnoreCase) ||
                extension.Equals(".jpg", StringComparison.InvariantCultureIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.InvariantCultureIgnoreCase);
        }

        public static List<string> GetImagesInDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                var results = Directory.GetFiles(path).Where(filePath => IsImageFilePath(filePath)).ToList();
                results.Sort(AlphaNumericComparator.Instance);
                return results;
            }
            else
            {
                return new List<string>();
            }
        }

        public static void EnsureDirectoryExists(string path)
        {
            if(!string.IsNullOrEmpty(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static void DeleteAllImagesInDirectory(string path)
        {
            foreach(var filePath in GetImagesInDirectory(path))
            {
                File.Delete(filePath);
            }
        }

        public static string GenerateNewImagePath(List<string> existingPaths, int newPageNumber, string fileExtension, string defaultDirectory = null)
        {
            return GenerateNewImagePaths(existingPaths, newPageNumber, 1, fileExtension, defaultDirectory)[0];
        }

        public static List<string> GenerateNewImagePaths(List<string> existingPaths, int firstNewPageNumber, int count, string fileExtension, string defaultDirectory = null)
        {
            // Check arguments
            if (firstNewPageNumber < 1 || firstNewPageNumber > existingPaths.Count + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(firstNewPageNumber));
            }

            // Generate the page paths
            var previousPageNumber = (firstNewPageNumber - 1);
            var previousPagePath = (previousPageNumber > 0) ? existingPaths[previousPageNumber - 1] : null;
            return GenerateNewImagePaths(previousPagePath, count, fileExtension, defaultDirectory);
        }

        public static string GenerateNewImagePath(string previousImagePath, string fileExtension, string defaultDirectory = null)
        {
            return GenerateNewImagePaths(previousImagePath, 1, fileExtension, defaultDirectory)[0];
        }

        public static List<string> GenerateNewImagePaths(string previousImagePath, int count, string fileExtension, string defaultDirectory = null)
        {
            // Determine a numbering scheme from the pages already in the file
            string suffix = string.IsNullOrEmpty(defaultDirectory) ? "" : (defaultDirectory + Path.DirectorySeparatorChar);
            int firstPageNumber = 1;
            if (previousImagePath != null)
            {
                var slashIdx = Math.Max(previousImagePath.LastIndexOf('/'), previousImagePath.LastIndexOf('\\'));
                var previousPageDir = previousImagePath.Substring(0, slashIdx + 1);
                var previousPageName = Path.GetFileNameWithoutExtension(previousImagePath.Substring(slashIdx + 1));
                for (int i = 0; i <= previousPageName.Length; ++i)
                {
                    int lastPageNumber;
                    if (i == previousPageName.Length)
                    {
                        // This filename doesn't have any numbers in it: append some
                        suffix = previousPageDir + previousPageName;
                        firstPageNumber = 2;
                        break;
                    }
                    else if (int.TryParse(previousPageName.Substring(i), out lastPageNumber))
                    {
                        // This filename contains a number, increment it
                        suffix = previousPageDir + previousPageName.Substring(0, i);
                        firstPageNumber = lastPageNumber + 1;
                        break;
                    }
                }
            }

            // Generate some page names using the existing naming scheme
            var results = new List<string>();
            results.Capacity = count;
            for (int i = 0; i < count; ++i)
            {
                int pageNumber = firstPageNumber + i;
                results.Add(suffix + pageNumber + fileExtension);
            }
            return results;
        }
    }
}
