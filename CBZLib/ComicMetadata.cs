using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Dan200.CBZLib
{
    public enum ComicRole
    {
        Writer,
        Artist,
        Penciller,
        Inker,
        Colorist,
        Letterer,
        Editor,
        Designer,
        Producer,
    }

    public class ComicAuthor
    {
        private ComicRole m_role;
        public ComicRole Role
        {
            get
            {
                return m_role;
            }
            set
            {
                m_role = value;
            }
        }

        private string m_fullName;
        public string FullName
        {
            get
            {
                return m_fullName;
            }
            set
            {
                if(value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                m_fullName = value;
            }
        }

        public ComicAuthor(ComicRole role, string fullName)
        {
            if(fullName == null)
            {
                throw new ArgumentNullException(nameof(fullName));
            }
            Role = role;
            FullName = fullName;
        }

        public ComicAuthor(ComicAuthor other)
        {
            Role = other.Role;
            FullName = other.FullName;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", Role, FullName);
        }
    }

    public enum ComicContentType
    {
        Cover,
        Story,
        Article,
        ContentsPage,
        LettersPage,
        Advertisement,
    }

    public class ComicContent
    {
        public ComicContentType? ContentType { get; set; }
        public PageRange Pages { get; set; }
        public string Title { get; set; }
        public string StoryTitle { get; set; }
        public int? PartNumber { get; set; }
        public List<ComicAuthor> Authors { get; private set; }

        public ComicContent()
        {
            ContentType = null;
            Pages = null;
            Title = null;
            StoryTitle = null;
            PartNumber = null;
            Authors = new List<ComicAuthor>();
        }

        public ComicContent(ComicContent other)
        {
            ContentType = other.ContentType;
            Pages = other.Pages;
            Title = other.Title;
            StoryTitle = other.StoryTitle;
            PartNumber = other.PartNumber;
            Authors = new List<ComicAuthor>(other.Authors);
        }

        public override string ToString()
        {
            if (StoryTitle != null)
            {
                var title = StoryTitle;
                if (PartNumber.HasValue)
                {
                    title += " (Part " + PartNumber.Value + ")";
                }
                return title;
            }
            else if(Title != null)
            {
                return Title;
            }
            else
            {
                return "Untitled";
            }
        }
    }

    public partial class ComicMetadata
    {
        public string IssueTitle;
        public string SeriesTitle;
        public int? IssueNumber;
        public int? VolumeNumber;
        public string Summary;
        public string Notes;
        public int? ReleaseYear;
        public int? ReleaseMonth;
        public int? ReleaseDay;
        public string Publisher;
        public string Imprint;
        public string Genre;
        public string Website;
        public string Language;
        public string ScanInformation;

        private List<ComicContent> m_contents;
        public List<ComicContent> Contents
        {
            get
            {
                return m_contents;
            }
            set
            {
                if(value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                m_contents = value;
            }
        }

        private List<ComicAuthor> m_authors;
        public List<ComicAuthor> Authors
        {
            get
            {
                return m_authors;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                m_authors = value;
            }
        }

        public ComicMetadata()
        {
            IssueTitle = null;
            SeriesTitle = null;
            IssueNumber = null;
            VolumeNumber = null;
            Summary = null;
            Notes = null;
            ReleaseYear = null;
            ReleaseMonth = null;
            ReleaseDay = null;
            Publisher = null;
            Imprint = null;
            Genre = null;
            Website = null;
            Language = null;
            ScanInformation = null;
            Contents = new List<ComicContent>();
            Authors = new List<ComicAuthor>();
        }

        public ComicMetadata(ComicMetadata other)
        {
            IssueTitle = other.IssueTitle;
            SeriesTitle = other.SeriesTitle;
            IssueNumber = other.IssueNumber;
            VolumeNumber = other.VolumeNumber;
            Summary = other.Summary;
            Notes = other.Notes;
            ReleaseYear = other.ReleaseYear;
            ReleaseMonth = other.ReleaseMonth;
            ReleaseDay = other.ReleaseDay;
            Publisher = other.Publisher;
            Imprint = other.Imprint;
            Genre = other.Genre;
            Website = other.Website;
            Language = other.Language;
            ScanInformation = other.ScanInformation;

            Contents = new List<ComicContent>();
            Contents.Capacity = other.Contents.Capacity;
            foreach(var otherContent in other.Contents)
            {
                Contents.Add(new ComicContent(otherContent));
            }

            Authors = new List<ComicAuthor>();
            Authors.Capacity = other.Authors.Capacity;
            foreach (var otherAuthor in other.Authors)
            {
                Authors.Add(new ComicAuthor(otherAuthor));
            }
        }

        public override string ToString()
        {
            if (SeriesTitle != null)
            {
                var result = SeriesTitle;
                if(VolumeNumber.HasValue)
                {
                    if (VolumeNumber.Value >= 1900)
                    {
                        // Probably a year
                        result += " (" + VolumeNumber.Value + ")";
                    }
                    else
                    {
                        // Probably a volume number
                        result += " (Volume " + VolumeNumber.Value + ")";
                    }
                }
                if (IssueNumber.HasValue)
                {
                    result += " " + IssueNumber.Value;
                }
                if (IssueTitle != null)
                {
                    result += ": " + IssueTitle;
                }
                return result;
            }
            else if(IssueTitle != null)
            {
                return IssueTitle;
            }
            else
            {
                return "Untitled Comic";
            }
        }

        public ComicMetadata Trim(PageList pages, int numPagesInComic)
        {
            var newMetadata = new ComicMetadata(this);
            var newContent = new List<ComicContent>(Contents.Capacity);
            int pagesSoFar = 0;
            foreach (var range in pages.SubRanges)
            {
                foreach (var content in Contents)
                {
                    if ((content.Pages == null) ||
                        (content.Pages.First >= range.First && content.Pages.Last <= range.Last))
                    {
                        if (content.Pages != null)
                        {
                            var contentCopy = new ComicContent(content);
                            contentCopy.Pages = new PageRange(
                                pagesSoFar + (content.Pages.First - range.First + 1),
                                pagesSoFar + (content.Pages.Last - range.First + 1)
                            );
                            newContent.Add(contentCopy);
                        }
                    }
                }
                int numPagesInRange = (Math.Min(range.Last, numPagesInComic) - range.First) + 1;
                pagesSoFar += numPagesInRange;
            }
            newContent.TrimExcess();
            newMetadata.Contents = newContent;
            return newMetadata;
        }

        public void Append(ComicMetadata metadata, int numPagesInComic)
        {
            Contents.Capacity += metadata.Contents.Count;
            foreach (var content in metadata.Contents)
            {
                var contentCopy = new ComicContent(content);
                if (contentCopy.Pages != null)
                {
                    contentCopy.Pages = new PageRange(contentCopy.Pages.First + numPagesInComic, contentCopy.Pages.Last + numPagesInComic);
                }
                Contents.Add(contentCopy);
            }
        }

        public void MovePagesBy(int offset)
        {
            if (offset != 0)
            {
                foreach (var content in Contents)
                {
                    if (content.Pages != null)
                    {
                        content.Pages = new PageRange(content.Pages.First + offset, content.Pages.Last + offset);
                    }
                }
            }
        }
    }
}
