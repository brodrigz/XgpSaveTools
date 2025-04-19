using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XgpSaveTools.Extensions;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
	public class ControlHandler : ISaveHandler
	{
		public bool CanHandle(string handlerName) => handlerName == "control";
		public IEnumerable<SaveFile> GetSaveEntries(List<ContainerMetaFile> containers, HandlerArgs? args)
		{
			foreach (var container in containers)
			{
				var folder = container.Name;
				// synthetic container name file
				var dispPath = Path.Combine(IoExtensions.CreateTempFolder().FullName, container.Name + "_--containerDisplayName.chunk");
				File.WriteAllText(dispPath, container.Name);
				yield return new(Path.Combine(folder, "--containerDisplayName.chunk").Replace('\\', '/'), dispPath);

				foreach (var file in container.Files)
				{
					var entry = Path.Combine(folder, file.Name + ".chunk").Replace('\\', '/');
					yield return new(entry, file);
				}
			}
		}
	}
}
