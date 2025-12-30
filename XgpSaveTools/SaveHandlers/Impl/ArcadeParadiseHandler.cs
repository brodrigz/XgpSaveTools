using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class ArcadeParadiseHandler : ISaveHandler
    {
        public bool CanHandle(string handlerName) => handlerName == "arcade-paradise";

        public IEnumerable<SaveFile> GetSaveEntries(
            List<ContainerMetaFile> containers,
            HandlerArgs? handlerArgs)
        {
            var fe = containers[0].Files[0];
            yield return new SaveFile("RATSaveData.dat", fe);
        }
    }
}