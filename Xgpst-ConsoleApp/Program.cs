using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XgpSaveTools;
using XgpSaveTools.Records;
using static XgpSaveTools.Extensions.IoExtensions;
using static XgpSaveTools.Common.GameList;

namespace Xgpst_ConsoleApp
{
	internal class Program
	{
		static void Main(string[] args)
		{
			Console.InputEncoding = System.Text.Encoding.UTF8;
			Console.OutputEncoding = System.Text.Encoding.UTF8;

			ConsoleApp app = new();
			app.RunMainLoop();
		}
	}
}