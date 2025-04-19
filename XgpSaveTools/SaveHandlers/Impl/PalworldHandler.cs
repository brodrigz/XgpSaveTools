using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
	namespace XgpSaveTools.SaveHandlers
	{
		public class PalworldHandler : ISaveHandler
		{
			public bool CanHandle(string handlerName) => handlerName == "palworld";
			public IEnumerable<SaveFile> GetSaveEntries(
				List<ContainerMetaFile> containers,
				HandlerArgs? handlerArgs)
			{
				foreach (var c in containers)
				{
					var entryName = c.Name.Replace("-", "/") + ".sav";
					yield return new SaveFile(entryName, c.Files[0]);
				}
			}
		}
	}

}
