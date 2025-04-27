using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XgpSaveTools.Common
{
	public static class StringExtensions
	{
		public static string RemoveSuffix(this string str, string suffix)
		{
			if (str != null && suffix != null && str.EndsWith(suffix))
			{
				return str.Substring(0, str.Length - suffix.Length);
			}
			return str;
		}


		public static string FixMissingDotOnExtension(string fileName, IEnumerable<string> suffixes)
		{
			foreach (var ext in suffixes)
			{
				if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
				{
					return fileName.Substring(0, fileName.Length - ext.Length) + "." + ext;
				}
			}
			return fileName;
		}

		public static bool EndsWithAny(this string src, params string[] suffixes)
		{
			foreach (var suffix in suffixes)
			{
				if (src.EndsWith(suffix)) return true;
			}
			return false;
		}
	}
}
