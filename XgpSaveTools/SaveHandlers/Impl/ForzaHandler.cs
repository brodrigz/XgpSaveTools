using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
	public class ForzaHandler : ISaveHandler
	{
		public bool CanHandle(string handlerName) => handlerName == "forza";
		public IEnumerable<SaveFile> GetSaveEntries(
			List<ContainerMetaFile> containers,
			HandlerArgs? handlerArgs)
		{
			foreach (var c in containers)
			{
				foreach (var fe in c.Files)
				{
					var entryName = $"{c.Name}.{fe.Name}";
					yield return new SaveFile(entryName, fe);
				}
			}
		}
	}

}
