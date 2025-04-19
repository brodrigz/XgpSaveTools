using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers
{
	// Handler interface
	public interface ISaveHandler
	{
		bool CanHandle(string handlerName);
		IEnumerable<SaveFile> GetSaveEntries(
			List<ContainerMetaFile> containers,
			HandlerArgs? handlerArgs);
	}
}
