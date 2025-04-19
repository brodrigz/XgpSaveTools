using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XgpSaveTools.Records;
using static XgpSaveTools.Extensions.IoExtensions;

namespace XgpSaveTools.SaveHandlers.Impl
{
	public class StarfieldHandler : ISaveHandler
	{
		public bool CanHandle(string handlerName) => handlerName == "starfield";
		public IEnumerable<SaveFile> GetSaveEntries(List<ContainerMetaFile> containers, HandlerArgs? args)
		{
			var outDir = Path.Combine(CreateTempFolder().FullName, "Starfield");
			Directory.CreateDirectory(outDir);
			var padStr = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("padding\0", 2)));

			foreach (var c in containers)
			{
				var path = c.Name.Replace("\\", "/");
				if (!path.StartsWith("Saves/")) continue;
				var saveName = Path.GetFileName(path);
				var parts = new SortedDictionary<int, string>();
				bool isNew = c.Files.Any(f => f.Name == "toc");
				foreach (var f in c.Files)
				{
					if (f.Name == "toc") continue;
					int idx = isNew
						? int.Parse(f.Name.Replace("BlobData", ""))
						: (f.Name == "BETHESDAPFH" ? 0 : int.Parse(f.Name.TrimStart('P')) + 1);
					parts[idx] = f.Path;
				}
				var outFile = Path.Combine(outDir, saveName);
				using var outFs = File.OpenWrite(outFile);
				foreach (var kv in parts)
				{
					var data = File.ReadAllBytes(kv.Value);
					outFs.Write(data, 0, data.Length);
					var pad = 16 - (data.Length % 16);
					if (pad < 16) outFs.Write(padStr, 0, pad);
				}
				yield return new(saveName, outFile);
			}
		}
	}
}
