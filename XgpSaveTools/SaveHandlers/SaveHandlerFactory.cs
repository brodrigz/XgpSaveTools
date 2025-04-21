using XgpSaveTools.SaveHandlers.Impl;
using XgpSaveTools.SaveHandlers.Impl.Generic;

namespace XgpSaveTools.SaveHandlers
{
	// DI would be overkill?
	public static class SaveHandlerFactory
	{
		private static readonly List<ISaveHandler> Handlers = new()
		{
			new GenericHandler(),
			new OneContainerOneFileHandler(),
			new OneContainerManyFilesHandler(),
			new OneContainerManyFilesFolderHandler(),
			new ControlHandler(),
			new StarfieldHandler(),
		};

		public static ISaveHandler Get(string name)
		{
			return Handlers.FirstOrDefault(h => h.CanHandle(name))
				?? throw new NotSupportedException($"Handler '{name}' not supported.");
		}
	}
}
