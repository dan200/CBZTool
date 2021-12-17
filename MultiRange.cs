using System;
using System.Collections.Generic;
using System.Linq;

namespace Dan200.CBZTool
{
    internal class MultiRange
    {
        public static readonly MultiRange All = new MultiRange(Range.All);

        public static MultiRange Parse(string s)
        {
            MultiRange result;
            if (TryParse(s, out result))
            {
                return result;
            }
            else
            {
                throw new Exception("Failed to parse range: " + s);
            }
        }

        public static bool TryParse(string s, out MultiRange o_range)
        {
            var result = new MultiRange();
            foreach (var part in s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Range range;
                if (Range.TryParse(part.Trim(), out range))
                {
                    result.Append(range);
                }
                else
                {
                    o_range = new MultiRange();
                    return false;
                }
            }

            o_range = result;
            return true;
        }

        private readonly List<Range> m_subRanges;

        public IReadOnlyList<Range> SubRanges
        {
            get
            {
                return m_subRanges;
            }
        }

        public int TotalLength
        {
            get
            {
                int total = 0;
                foreach (var subRange in m_subRanges)
                {
                    total += subRange.Length;
                }
                return total;
            }
        }

        public MultiRange()
        {
            m_subRanges = new List<Range>();
        }

        public MultiRange(params Range[] ranges)
        {
            m_subRanges = new List<Range>();
            foreach(Range range in ranges)
            {
                Append(range);
            }
        }

        public void Append(MultiRange r)
        {
            foreach(Range range in r.SubRanges)
            {
                Append(range);
            }
        }

        public void Append(Range r)
        {
            if(m_subRanges.Count > 0 && m_subRanges[SubRanges.Count - 1].Last == (r.First - 1))
            {
                m_subRanges[SubRanges.Count - 1].Last = r.Last;
            }
            else
            {
                m_subRanges.Add(r);
            }
        }

        public bool Equals(MultiRange o)
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
