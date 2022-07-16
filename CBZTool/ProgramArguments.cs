using System.Text;
using System.Collections.Generic;
using System;
using System.Globalization;

namespace Dan200.CBZTool
{
    internal class ProgramArguments
    {
		public static ProgramArguments Empty = new ProgramArguments(new string[0]);

        private string m_representation;
		private List<string> m_arguments;
		private Dictionary<string, string> m_options;

		public int Count
		{
			get
			{
				return m_arguments.Count;
			}
		}

		public int OptionCount
		{
			get
			{
				return m_options.Count;
			}
		}

		private string AddQuotes(string arg)
        {
			if (arg.Contains(" ") || arg.Contains("\t"))
            {
				return "\"" + arg + "\"";
            }
			else
            {
				return arg;
            }
        }

        public ProgramArguments(string[] args)
        {
            var representation = new StringBuilder();
			var arguments = new List<string>();
			var options = new Dictionary<string, string>();
            string lastOption = null;
            foreach (string arg in args)
            {
				if (arg.StartsWith("-", StringComparison.InvariantCulture))
                {
                    if (lastOption != null)
                    {
                        representation.Append("-" + lastOption + " ");
						options[lastOption] = "true";
                    }
                    lastOption = arg.Substring(1);
                }
                else if (lastOption != null)
                {
                    representation.Append("-" + lastOption + " " + AddQuotes(arg) + " ");
					options[lastOption] = arg;
                    lastOption = null;
                }
				else
				{
					representation.Append(AddQuotes(arg) + " ");
					arguments.Add(arg);
				}
            }
            if (lastOption != null)
            {
                representation.Append("-" + lastOption + " ");
				options[lastOption] = "true";
            }
            m_representation = representation.ToString().TrimEnd();
			m_options = options;
			m_arguments = arguments;
        }

		public string Get(int index, string _default=null)
		{
			if(index >= 0 && index < m_arguments.Count)
			{
				return m_arguments[index];
			}
			return _default;
		}

		public string GetStringOption(string key, string _default=null)
		{
			string arg;
			if (m_options.TryGetValue(key, out arg))
			{
				return arg;
			}
			return _default;
		}

		public float GetFloatOption(string key, float _default = 0.0f)
		{
			string arg;
			float result;
			if (m_options.TryGetValue(key, out arg) &&
			    float.TryParse(arg, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
			{
				return result;
			}
			return _default;
		}

		public int GetIntegerOption(string key, int _default = 0)
		{
			string arg;
			int result;
			if (m_options.TryGetValue(key, out arg) &&
			    int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
			{
				return result;
			}
			return _default;
		}

		public bool GetBoolOption(string key, bool _default = false)
		{
			string arg;
			if (m_options.TryGetValue(key, out arg))
			{
				return arg == "true" || arg == "1";
			}
			return _default;
		}

		public override string ToString()
		{
			return m_representation;
		}
	}
}
