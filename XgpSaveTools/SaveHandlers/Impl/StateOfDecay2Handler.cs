using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
	public class StateOfDecay2Handler : ISaveHandler
	{
		public bool CanHandle(string handlerName) => handlerName == "state-of-decay-2";
		public IEnumerable<SaveFile> GetSaveEntries(
			List<ContainerMetaFile> containers,
			HandlerArgs? handlerArgs)
		{
			foreach (var fe in containers[0].Files)
			{
				var leaf = System.IO.Path.GetFileName(fe.Name) + ".sav";
				yield return new SaveFile(leaf, fe);
			}
		}
	}
}
