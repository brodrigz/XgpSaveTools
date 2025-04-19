using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
	public class OneContainerOneFileHandler : ISaveHandler
	{
		public bool CanHandle(string handlerName) => handlerName == "1c1f";
		public IEnumerable<SaveFile> GetSaveEntries(List<ContainerMetaFile> containers, HandlerArgs? args)
		{
			foreach (var c in containers)
			{
				var name = c.Name + (args?.Suffix ?? string.Empty);
				yield return new(name, c.Files[0]);
			}
		}
	}

}
