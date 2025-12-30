using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers
{
    // Handler interface
    public interface ISaveHandler
    {
        bool CanHandle(string handlerName);

        IEnumerable<SaveFile> GetSaveEntries(List<ContainerMetaFile> containers, HandlerArgs? handlerArgs);
    }
}