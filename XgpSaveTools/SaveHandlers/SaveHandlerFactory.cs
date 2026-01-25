using XgpSaveTools.SaveHandlers.Impl;
using XgpSaveTools.SaveHandlers.Impl.Generic;

namespace XgpSaveTools.SaveHandlers
{
    // DI would be overkill?
    public static class SaveHandlerFactory
    {
        // ADD HANDLERS FROM MOST GENERIC TO MOST SPECIALIZED
        private static readonly List<ISaveHandler> Handlers = new()
        {
            new GenericHandler(),
            new OneContainerOneFileHandler(),
            new OneContainerManyFilesHandler(),
            new OneContainerManyFilesFolderHandler(),
            new ControlHandler(),
            new StarfieldHandler(),
            new Fallout4Handler(),
            new ScornHandler(),
            new Persona3ReloadHandler(),
            new ArcadeParadiseHandler(),
            new CoralIslandHandler(),
            new Cricket24Handler(),
            new ForzaHandler(),
            new LiesOfPHandler(),
            new LikeADragonHandler(),
            new PalworldHandler(),
            new RailwayEmpire2Handler(),
            new StateOfDecay2Handler()
        };

        public static ISaveHandler Get(string name)
        {
            var handler = Handlers.LastOrDefault(h => h.CanHandle(name)) ?? throw new NotSupportedException($"Handler '{name}' not supported.");
            Console.WriteLine($"Using {handler.GetType().Name}");
            return handler;
        }
    }
}