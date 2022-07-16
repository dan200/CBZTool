using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dan200.CBZLib
{
    public partial class ComicMetadata
    {
        private static Dictionary<ComicRole, string[]> s_creditSynonyms = new Dictionary<ComicRole, string[]>
        {
            { ComicRole.Writer, new string[]{ "Script", "Game Guru", "Reviewer", "Interviewer", "News Hound", "News", "Newshound", "Reporter", "Problem Solver", "Script & Art" } },
            { ComicRole.Artist, new string[]{ "Art", "Artist", "Script & Art", "Photographer" } },
            { ComicRole.Colorist, new string[]{ "Colour", "Color", "Colouring" } },
            { ComicRole.Letterer, new string[]{ "Lettering" } },
            { ComicRole.Editor, new string[]{ "Editor", "Asst Editor", "Managing Editor", "Assistant Editor", "Asst. Editor", "Co-Editor", "Editorial Assistant", "Editorial Assistance", "Features Editor", "Review Zone Editor" } },
            { ComicRole.Designer, new string[]{ "Design", "Designer", "Cover Designer", "Cover Design" } },
            { ComicRole.Producer, new string[]{ "Production" } },
        };
        private static void ParseCredits(string key, string value, List<ComicAuthor> o_credits)
        {
            foreach (var pair in s_creditSynonyms)
            {
                var role = pair.Key;
                foreach (var synonym in pair.Value)
                {
                    if (key.Equals(synonym, StringComparison.InvariantCultureIgnoreCase))
                    {
                        foreach (var name in value.Split(new char[] { '/', '&', ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            o_credits.Add(new ComicAuthor(role, name.Trim()));
                        }
                    }
                }
            }
        }

        public static ComicMetadata FromTagFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return FromTagFile(stream);
            }
        }

        public static ComicMetadata FromTagFile(Stream stream)
        {
            var reader = new StreamReader(stream, Encoding.UTF8);
            var metadata = new ComicMetadata();

            // Parse header
            string line = null;
            bool contentsStarted = false;
            ComicContent currentContent = null;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                int colonPos = line.IndexOf(':');
                if (colonPos >= 0)
                {
                    string key = line.Substring(0, colonPos).Trim().ToLowerInvariant();
                    string value = line.Substring(colonPos + 1).Trim();
                    if (key.Length > 0)
                    {
                        if (key == "title")
                        {
                            // Title
                            if (!contentsStarted && value.Length > 0)
                            {
                                int issueNum;
                                var hashIndex = value.IndexOf('#');
                                if (hashIndex >= 0 && int.TryParse(value.Substring(hashIndex + 1).Trim(), out issueNum))
                                {
                                    // An issue in a series
                                    metadata.SeriesTitle = value.Substring(0, hashIndex).Trim();
                                    metadata.IssueNumber = issueNum;
                                }
                                else
                                {
                                    // One-shot
                                    metadata.IssueTitle = value;
                                }
                            }
                        }
                        else if (key == "date")
                        {
                            // Date
                            if (!contentsStarted && value.Length > 0)
                            {
                                var spaceIdx = value.IndexOf(' ');
                                if (spaceIdx >= 0)
                                {
                                    foreach (var suffix in new string[] { "st", "nd", "rd", "th" })
                                    {
                                        int suffixIdx = value.IndexOf(suffix, 0, spaceIdx);
                                        if (suffixIdx >= 0)
                                        {
                                            value = value.Substring(0, suffixIdx) + value.Substring(suffixIdx + suffix.Length);
                                        }
                                    }
                                    DateTime date;
                                    if (DateTime.TryParseExact(value, "d MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite, out date) ||
                                        DateTime.TryParseExact(value, "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite, out date))
                                    {
                                        metadata.ReleaseYear = date.Year;
                                        metadata.ReleaseMonth = date.Month;
                                        metadata.ReleaseDay = date.Day;
                                    }
                                }
                            }
                        }
                        else if (key == "publisher" || key == "published by")
                        {
                            // Publisher
                            if (!contentsStarted && metadata.Publisher == null && value.Length > 0)
                            {
                                metadata.Publisher = value;
                            }
                        }
                        else if (key == "scanned by")
                        {
                            // Scan information
                            if (!contentsStarted && value.Length > 0)
                            {
                                metadata.ScanInformation = "Scanned by " + value;
                            }
                        }
                        else if (key == "contents")
                        {
                            // Start of contents
                            contentsStarted = true;
                        }
                        else
                        {
                            // Credits
                            if (value.Length > 0)
                            {
                                if (contentsStarted && currentContent != null)
                                {
                                    ParseCredits(key, value, currentContent.Authors);
                                }
                                else if (!contentsStarted)
                                {
                                    ParseCredits(key, value, metadata.Authors);
                                }
                            }
                        }
                    }
                }
                else if (line.StartsWith("http://"))
                {
                    // Website
                    if (!contentsStarted)
                    {
                        metadata.Website = line;
                    }
                }
                else if (line.ToLowerInvariant().StartsWith("page "))
                {
                    // Start of a content section
                    PageRange pageRange;
                    if (PageRange.TryParse(line.Substring("page ".Length), out pageRange))
                    {
                        string titleLine = reader.ReadLine();
                        if (titleLine != null)
                        {
                            titleLine = titleLine.Trim();
                            contentsStarted = true;
                            currentContent = new ComicContent();
                            metadata.Content.Add(currentContent);

                            // Page range
                            currentContent.Pages = pageRange;

                            // Part number
                            int dashIndex = titleLine.IndexOf(" - ");
                            int partIndex = titleLine.IndexOf("(part", (dashIndex >= 0) ? (dashIndex + 3) : 0, StringComparison.InvariantCultureIgnoreCase);
                            int partEndIndex = (partIndex >= 0) ? titleLine.IndexOf(")", partIndex) : -1;
                            if (partIndex >= 0 && partEndIndex >= 0)
                            {
                                int partNum;
                                string partStr = titleLine.Substring(partIndex + "(part".Length, partEndIndex - (partIndex + "(part".Length)).Trim();
                                if (int.TryParse(partStr, out partNum))
                                {
                                    currentContent.PartNumber = partNum;
                                    titleLine = titleLine.Substring(0, partIndex);
                                }
                            }

                            // Series and story title
                            if (dashIndex >= 0)
                            {
                                currentContent.Title = titleLine.Substring(0, dashIndex).Trim();
                                currentContent.StoryTitle = titleLine.Substring(dashIndex + 3).Trim();
                                if (currentContent.StoryTitle.Length == 0)
                                {
                                    currentContent.StoryTitle = currentContent.Title;
                                }
                            }
                            else
                            {
                                currentContent.Title = titleLine.Trim();
                            }
                        }
                    }
                }
            }

            return metadata;
        }
    }
}
