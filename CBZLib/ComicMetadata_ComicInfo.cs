using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Dan200.CBZLib
{
    public partial class ComicMetadata
    {
        private static string GetStringProperty(XElement element, string key, string _default=null)
        {
            var childElement = element.Element(key);
            if (childElement != null)
            {
                return childElement.Value;
            }
            return _default;
        }

        private static string[] GetStringProperties(XElement element, string key)
        {
            var result = new List<string>();
            foreach(var childElement in element.Elements(key))
            {
                result.Add(childElement.Value);
            }
            return result.ToArray();
        }

        private static int? GetIntProperty(XElement element, string key, int? _default = null)
        {
            var str = GetStringProperty(element, key);
            if(str != null)
            {
                int result;
                if(int.TryParse(str, out result))
                {
                    return result;
                }
            }
            return _default;
        }

        private static TEnum? GetEnumProperty<TEnum>(XElement element, string key, TEnum? _default = null) where TEnum : struct
        {
            var str = GetStringProperty(element, key);
            if (str != null)
            {
                TEnum result;
                if (Enum.TryParse<TEnum>(str, out result))
                {
                    return result;
                }
            }
            return _default;
        }

        private static void WriteProperty<T>(XmlWriter writer, string key, T value) where T : class
        {
            if(value != null)
            {
                writer.WriteElementString(key, value.ToString());
            }
        }

        private static void WriteProperty<T>(XmlWriter writer, string key, T? value) where T : struct
        {
            if (value.HasValue)
            {
                writer.WriteElementString(key, value.Value.ToString());
            }
        }

        private static PageRange GetPageRangeProperty(XElement element, string key, PageRange _default = null)
        {
            var str = GetStringProperty(element, key);
            if (str != null)
            {
                PageRange result;
                if (PageRange.TryParse(str, out result))
                {
                    return result;
                }
            }
            return _default;
        }

        public static ComicMetadata FromComicInfoFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return FromComicInfoFile(stream);
            }
        }

        public static ComicMetadata FromComicInfoFile(Stream stream)
        {
            var metadata = new ComicMetadata();

            var document = XDocument.Load(stream);
            var rootElement = document.Root;

            if (rootElement != null)
            {
                // Parse issue info
                metadata.IssueTitle = GetStringProperty(rootElement, "Title");
                metadata.SeriesTitle = GetStringProperty(rootElement, "Series");
                metadata.IssueNumber = GetIntProperty(rootElement, "Number");
                metadata.VolumeNumber = GetIntProperty(rootElement, "Volume");
                metadata.Summary = GetStringProperty(rootElement, "Summary");
                metadata.Notes = GetStringProperty(rootElement, "Notes");
                metadata.ReleaseYear = GetIntProperty(rootElement, "Year");
                metadata.ReleaseMonth = GetIntProperty(rootElement, "Month");
                metadata.ReleaseDay = GetIntProperty(rootElement, "Day");
                metadata.Publisher = GetStringProperty(rootElement, "Publisher");
                metadata.Imprint = GetStringProperty(rootElement, "Imprint");
                metadata.Website = GetStringProperty(rootElement, "Web");
                metadata.Language = GetStringProperty(rootElement, "LanguageISO");
                metadata.ScanInformation = GetStringProperty(rootElement, "ScanInformation");

                // Parse issue credits
                foreach (ComicRole role in Enum.GetValues(typeof(ComicRole)))
                {
                    var roleStr = role.ToString();
                    foreach (var name in GetStringProperties(rootElement, roleStr))
                    {
                        metadata.Authors.Add(new ComicAuthor(role, name));
                    }
                }

                // Parse contents
                var contentsElement = rootElement.Element("Contents");
                if (contentsElement != null)
                {
                    foreach (var contentElement in contentsElement.Elements("Content"))
                    {
                        var content = new ComicContent();

                        // Parse content info
                        content.ContentType = GetEnumProperty<ComicContentType>(contentElement, "ContentType");
                        content.Pages = GetPageRangeProperty(contentElement, "Pages");
                        content.Title = GetStringProperty(contentElement, "Title");
                        content.StoryTitle = GetStringProperty(contentElement, "Story");
                        content.PartNumber = GetIntProperty(contentElement, "Part");

                        // Parse content credits
                        foreach (ComicRole role in Enum.GetValues(typeof(ComicRole)))
                        {
                            foreach (var name in GetStringProperties(contentElement, role.ToString()))
                            {
                                content.Authors.Add(new ComicAuthor(role, name));
                            }
                        }

                        metadata.Contents.Add(content);
                    }
                }
            }

            return metadata;
        }

        public void SaveAsComicInfoFile(string path)
        {
            var directory = Path.GetDirectoryName(path);
            ComicExtractUtils.EnsureDirectoryExists(directory);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                SaveAsComicInfoFile(stream);
            }
        }

        public void SaveAsComicInfoFile(Stream stream)
        {
            var settings = new XmlWriterSettings();
            settings.Indent = true;

            using (var writer = XmlWriter.Create(stream, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("ComicInfo");

                // Write header
                WriteProperty(writer, "Title", IssueTitle);
                WriteProperty(writer, "Series", SeriesTitle);
                WriteProperty(writer, "Number", IssueNumber);
                WriteProperty(writer, "Volume", VolumeNumber);
                WriteProperty(writer, "Summary", Summary);
                WriteProperty(writer, "Notes", Notes);
                WriteProperty(writer, "Year", ReleaseYear);
                WriteProperty(writer, "Month", ReleaseMonth);
                WriteProperty(writer, "Day", ReleaseDay);
                WriteProperty(writer, "Publisher", Publisher);
                WriteProperty(writer, "Imprint", Imprint);
                WriteProperty(writer, "Genre", Genre);
                WriteProperty(writer, "Web", Website);
                WriteProperty(writer, "LanguageISO", Language);
                WriteProperty(writer, "ScanInformation", ScanInformation);

                // Write authors
                foreach (var author in Authors)
                {
                    WriteProperty(writer, author.Role.ToString(), author.FullName);
                }

                // Write contents
                writer.WriteStartElement("Contents");
                foreach (var content in Contents)
                {
                    writer.WriteStartElement("Content");
                    WriteProperty(writer, "ContentType", content.ContentType);
                    WriteProperty(writer, "Pages", content.Pages);
                    WriteProperty(writer, "Title", content.Title);
                    WriteProperty(writer, "Story", content.StoryTitle);
                    WriteProperty(writer, "Part", content.PartNumber);
                    foreach (var author in content.Authors)
                    {
                        writer.WriteElementString(author.Role.ToString(), author.FullName);
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }
    }
}
