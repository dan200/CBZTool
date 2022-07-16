using System;
using System.Collections.Generic;
using System.Linq;

namespace Dan200.CBZLib
{
    public class PageList
    {
        public static readonly PageList All = new PageList(PageRange.All);

        public static PageList Parse(string s)
        {
            PageList result;
            if (TryParse(s, out result))
            {
                return result;
            }
            else
            {
                throw new Exception("Failed to parse range: " + s);
            }
        }

        public static bool TryParse(string s, out PageList o_list)
        {
            var result = new PageList();
            foreach (var part in s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                PageRange range;
                if (PageRange.TryParse(part.Trim(), out range))
                {
                    result.Append(range);
                }
                else
                {
                    o_list = new PageList();
                    return false;
                }
            }

            o_list = result;
            return true;
        }

        private readonly List<PageRange> m_subRanges;

        public IReadOnlyList<PageRange> SubRanges
        {
            get
            {
                return m_subRanges;
            }
        }

        public PageList(int firstAndlast) : this(new PageRange(firstAndlast))
        {
        }

        public PageList(int first, int last) : this(new PageRange(first, last))
        {
        }

        public PageList(params PageRange[] ranges)
        {
            m_subRanges = new List<PageRange>();
            foreach(PageRange range in ranges)
            {
                Append(range);
            }
        }

        public void Append(PageList r)
        {
            foreach(PageRange range in r.SubRanges)
            {
                Append(range);
            }
        }

        public void Append(PageRange r)
        {
            if(m_subRanges.Count > 0 && m_subRanges[SubRanges.Count - 1].Last == (r.First - 1))
            {
                var lastRange = m_subRanges[SubRanges.Count - 1];
                m_subRanges[SubRanges.Count - 1] = new PageRange(m_subRanges[SubRanges.Count - 1].First, r.Last);
            }
            else
            {
                m_subRanges.Add(r);
            }
        }

        public bool Equals(PageList o)
        {
            if (m_subRanges.Count == o.m_subRanges.Count)
            {
                for (int i = 0; i < m_subRanges.Count; ++i)
                {
                    if (m_subRanges[i] != o.m_subRanges[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return string.Join(",", SubRanges.Select(r => r.ToString()).ToArray());
        }
    }
}
