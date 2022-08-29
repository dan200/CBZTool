using Dan200.CBZLib;
using Dan200.CBZTool;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dan200.CBZTool
{
    internal static class InfoCommand
    {
        private class AuthorInfo
        {
            public string Name;
            public ComicRole Role;
            public int PagesAuthored;
        }

        private static void AddAuthorInfo(List<AuthorInfo> io_authorInfo, string name, ComicRole role, int numPages)
        {
            foreach(var authorInfo in io_authorInfo)
            {
                if(authorInfo.Name == name && authorInfo.Role == role)
                {
                    authorInfo.PagesAuthored += numPages;
                    return;
                }
            }

            var newInfo = new AuthorInfo();
            newInfo.Name = name;
            newInfo.Role = role;
            newInfo.PagesAuthored = numPages;
            io_authorInfo.Add(newInfo);
        }

        private static List<AuthorInfo> GetAuthorInfo(ComicArchive comic)
        {
            var results = new List<AuthorInfo>();
            if (comic.Metadata != null)
            {
                // Measure the contribution of all authors
                foreach (var author in comic.Metadata.Authors)
                {
                    AddAuthorInfo(results, author.FullName, author.Role, comic.PageCount);
                }
                foreach(var content in comic.Metadata.Contents)
                {
                    int pageCount = Math.Min(content.Pages.Last, comic.PageCount) - Math.Min(content.Pages.First, comic.PageCount) + 1;
                    foreach(var author in content.Authors)
                    {
                        AddAuthorInfo(results, author.FullName, author.Role, pageCount);
                    }
                }

                // Sort by pages, then role, then alpabetically
                results.Sort((AuthorInfo a, AuthorInfo b) => {
                    int compareResult = b.PagesAuthored.CompareTo(a.PagesAuthored);
                    if(compareResult == 0)
                    {
                        compareResult = b.Role.CompareTo(a.Role);
                        if(compareResult == 0)
                        {
                            compareResult = a.Name.CompareTo(b.Name);
                        }
                    }
                    return compareResult;
                });
            }
            return results;
        }

        public static string GetOrdinalDateSuffix(int num)
        {
            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return "th";
            }
            switch (num % 10)
            {
                case 1:
                    return "st";
                case 2:
                    return "nd";
                case 3:
                    return "rd";
                default:
                    return "th";
            }
        }

        private static bool PrintInfo_Comic(string inputPath)
        {
            using (var inputComic = new ComicArchive(inputPath, ComicArchiveMode.Read))
            {
                Console.WriteLine("Info for {0}:", inputPath);
                if (inputComic.PageCount == 1)
                {
                    Console.WriteLine("This comic has 1 page.", inputComic.PageCount);
                }
                else
                {
                    Console.WriteLine("This comic has {0} pages.", inputComic.PageCount);
                }
                if (inputComic.Metadata != null)
                {
                    if (inputComic.Metadata.IssueTitle != null)
                    {
                        Console.WriteLine("Issue Title: {0}", inputComic.Metadata.IssueTitle);
                    }
                    if (inputComic.Metadata.SeriesTitle != null)
                    {
                        Console.WriteLine("Series Title: {0}", inputComic.Metadata.SeriesTitle);
                    }
                    if (inputComic.Metadata.VolumeNumber.HasValue)
                    {
                        Console.WriteLine("Volume Number: {0}", inputComic.Metadata.VolumeNumber.Value);
                    }
                    if (inputComic.Metadata.IssueNumber != null)
                    {
                        Console.WriteLine("Issue Number: {0}", inputComic.Metadata.IssueNumber);
                    }
                    if (inputComic.Metadata.ReleaseYear.HasValue || inputComic.Metadata.ReleaseMonth.HasValue || inputComic.Metadata.ReleaseDay.HasValue)
                    {
                        var dateBuilder = new StringBuilder();
                        if (inputComic.Metadata.ReleaseMonth.HasValue)
                        {
                            int month = inputComic.Metadata.ReleaseMonth.Value;
                            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
                            if (dateBuilder.Length > 0) { dateBuilder.Append(" "); }
                            dateBuilder.Append(monthName);
                        }
                        if (inputComic.Metadata.ReleaseDay.HasValue)
                        {
                            int day = inputComic.Metadata.ReleaseDay.Value;
                            if (dateBuilder.Length > 0) { dateBuilder.Append(" "); }
                            dateBuilder.Append(day.ToString() + GetOrdinalDateSuffix(day));
                        }
                        if (inputComic.Metadata.ReleaseYear.HasValue)
                        {
                            int year = inputComic.Metadata.ReleaseYear.Value;
                            if (dateBuilder.Length > 0) { dateBuilder.Append(" "); }
                            dateBuilder.Append(year.ToString());
                        }
                        Console.WriteLine("Release Date: {0}", dateBuilder.ToString());
                    }
                    if (inputComic.Metadata.Publisher != null)
                    {
                        Console.WriteLine("Publisher: {0}", inputComic.Metadata.Publisher);
                    }
                    if (inputComic.Metadata.Imprint != null)
                    {
                        Console.WriteLine("Imprint: {0}", inputComic.Metadata.Imprint);
                    }

                    var authors = GetAuthorInfo(inputComic);
                    if (authors.Count > 0)
                    {
                        Console.WriteLine("Authors:");
                        foreach (var author in authors)
                        {
                            if (author.PagesAuthored == inputComic.PageCount)
                            {
                                Console.WriteLine("{0} ({1})", author.Name, author.Role);
                            }
                            else if(author.PagesAuthored == 1)
                            {
                                Console.WriteLine("{0} ({1}, 1 page)", author.Name, author.Role);
                            }
                            else
                            {
                                Console.WriteLine("{0} ({1}, {2} pages)", author.Name, author.Role, author.PagesAuthored);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("This comic has no metadata", inputComic.PageCount);
                }
            }
            return true;
        }

        public static bool PrintInfo(string inputPath)
        {
            if (File.Exists(inputPath))
            {
                var inputExtension = Path.GetExtension(inputPath);
                if (inputExtension.Equals(".cbr", StringComparison.InvariantCultureIgnoreCase) || inputExtension.Equals(".cbz", StringComparison.InvariantCultureIgnoreCase))
                {
                    return PrintInfo_Comic(inputPath);
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
