using System;

namespace Dan200.CBZLib
{
    public class PageRange : IEquatable<PageRange>
    {
        public static readonly PageRange All = new PageRange(1, int.MaxValue);

        public static PageRange Parse(string s)
        {
            PageRange result;
            if (TryParse(s, out result))
            {
                return result;
            }
            else
            {
                throw new Exception("Failed to parse range: " + s);
            }
        }

        public static bool TryParse(string s, out PageRange o_range)
        {
            int dashIndex = s.IndexOf('-');
            if (dashIndex >= 0)
            {
                int first, last;
                if (int.TryParse(s.Substring(0, dashIndex), out first))
                {
                    string secondPart = s.Substring(dashIndex + 1);
                    if (secondPart == "*")
                    {
                        if (first >= 1)
                        {
                            o_range = new PageRange(first, int.MaxValue);
                            return true;
                        }
                    }
                    else if (int.TryParse(s.Substring(dashIndex + 1), out last))
                    {
                        if (first >= 1 && last >= first)
                        {
                            o_range = new PageRange(first, last);
                            return true;
                        }
                    }
                }
            }
            else
            {
                int first;
                if(s == "*")
                {
                    o_range = PageRange.All;
                    return true;
                }
                else if (int.TryParse(s, out first))
                {
                    if (first >= 1)
                    {
                        o_range = new PageRange(first);
                        return true;
                    }
                }
            }
            o_range = null;
            return false;
        }

        public readonly int First;
        public readonly int Last;

        public PageRange(int firstAndLast) : this(firstAndLast, firstAndLast)
        {
        }

        public PageRange(int first, int last)
        {
            if (first < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(first));
            }
            if(last < first)
            {
                throw new ArgumentOutOfRangeException(nameof(last));
            }
            First = first;
            Last = last;
        }

        public override bool Equals(object o)
        {
            if(o is PageRange)
            {
                return Equals((PageRange)o);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return First ^ Last;
        }

        public bool Equals(PageRange o)
        {
            return
                o != null &&
                First == o.First &&
                Last == o.Last;
        }

        public override string ToString()
        {
            if (First == Last)
            {
                return First.ToString();
            }
            else if(Last == int.MaxValue)
            {
                if (First == 1)
                {
                    return "*";
                }
                else
                {
                    return First.ToString() + "-*";
                }
            }
            else
            {
                return First.ToString() + "-" + Last.ToString();
            }
        }
    }
}
