using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
	public class Cricket24Handler : ISaveHandler
	{
		public bool CanHandle(string handlerName) => handlerName == "cricket-24";
		public IEnumerable<SaveFile> GetSaveEntries(
			List<ContainerMetaFile> containers,
			HandlerArgs? handlerArgs)
		{
			foreach (var c in containers)
			{
				foreach (var fe in c.Files)
				{
					var fn = fe.Name;
					if (fn.EndsWith(".CHUNK0", StringComparison.OrdinalIgnoreCase))
						fn = fn[..^7];
					else if (fn.Contains("CHUNK", StringComparison.OrdinalIgnoreCase))
						throw new InvalidOperationException($"Unexpected chunk in {fe.Name}");
					fn += ".SAV";

					var entryName = $"{c.Name}/{fn}";
					yield return new SaveFile(entryName, fe);
				}
			}
		}
	}

}
