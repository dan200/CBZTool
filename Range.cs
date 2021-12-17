using System;

namespace Dan200.CBZTool
{
    internal class Range : IEquatable<Range>
    {
        public static readonly Range All = new Range(1, int.MaxValue);

        public static Range Parse(string s)
        {
            Range result;
            if (TryParse(s, out result))
            {
                return result;
            }
            else
            {
                throw new Exception("Failed to parse range: " + s);
            }
        }

        public static bool TryParse(string s, out Range o_range)
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
                            o_range = new Range(first, int.MaxValue);
                            return true;
                        }
                    }
                    else if (int.TryParse(s.Substring(dashIndex + 1), out last))
                    {
                        if (first >= 1 && last >= first)
                        {
                            o_range = new Range(first, last);
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
                    o_range = Range.All;
                    return true;
                }
                else if (int.TryParse(s, out first))
                {
                    if (first >= 1)
                    {
                        o_range = new Range(first);
                        return true;
                    }
                }
            }
            o_range = null;
            return false;
        }

        private int m_first;
        private int m_last;

        public int First
        {
            get
            {
                return m_first;
            }
            set
            {
                if (value <= 0 || value > m_last)
                {
                    throw new Exception("Value out of range");
                }
                m_first = value;
            }
        }

        public int Last
        {
            get
            {
                return m_last;
            }
            set
            {
                if (value <= 0 || value < m_first)
                {
                    throw new Exception("Value out of range");
                }
                m_last = value;
            }
        }

        public int Length 
        {
            get 
            {
                return m_last - m_first + 1;
            }
        }

        public Range(int firstAndLast) : this(firstAndLast, firstAndLast)
        {
        }

        public Range(int first, int last)
        {
            if (first <= 0 || last < first)
            {
                throw new Exception("Value out of range");
            }
            m_first = first;
            m_last = last;
        }

        public override bool Equals(object o)
        {
            if(o is Range)
            {
                return Equals((Range)o);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return m_first ^ m_last;
        }

        public bool Equals(Range o)
        {
            return
                o != null &&
                m_first == o.m_first &&
                m_last == o.m_last;
        }

        public static bool operator ==(Range a, Range b)
        {
            return (a != null) ? a.Equals(b) : (b == null);
        }

        public static bool operator !=(Range a, Range b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            if (m_first == m_last)
            {
                return m_first.ToString();
            }
            else if(m_last == int.MaxValue)
            {
                if (m_first == 1)
                {
                    return "*";
                }
                else
                {
                    return m_first.ToString() + "-*";
                }
            }
            else
            {
                return m_first.ToString() + "-" + m_last.ToString();
            }
        }
    }
}
