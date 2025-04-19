using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
	public class CoralIslandHandler : ISaveHandler
	{
		public bool CanHandle(string handlerName) => handlerName == "coral-island";
		public IEnumerable<SaveFile> GetSaveEntries(
			List<ContainerMetaFile> containers,
			HandlerArgs? handlerArgs)
		{
			foreach (var c in containers)
			{
				var name = c.Name + ".sav";
				if (name.StartsWith("Backup", StringComparison.OrdinalIgnoreCase))
				{
					name = $"Backup/{name["Backup".Length..]}";
				}
				yield return new SaveFile(name, c.Files[0]);
			}
		}
	}
}
