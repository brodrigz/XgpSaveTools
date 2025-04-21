using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl.Generic
{
	public class GenericHandler : ISaveHandler
	{
		public bool CanHandle(string handlerName) => true;
		public IEnumerable<SaveFile> GetSaveEntries(List<ContainerMetaFile> containers, HandlerArgs? args)
		{
			foreach (var container in containers)
			{
				foreach (var file in container.Files)
				{
					var name = container.Name + (args?.Suffix ?? string.Empty);
					yield return new(name, file);
				}
			}
		}
	}
}
