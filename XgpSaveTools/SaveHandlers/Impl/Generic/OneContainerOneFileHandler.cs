using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class OneContainerOneFileHandler : ISaveHandler
    {
        public virtual bool CanHandle(string handlerName) => handlerName == "1c1f";

        public virtual IEnumerable<SaveFile> GetSaveEntries(List<ContainerMetaFile> containers, HandlerArgs? args)
        {
            foreach (var c in containers)
            {
                var name = c.Name + (args?.Suffix ?? string.Empty);
                yield return new(name, c.Files[0]);
            }
        }
    }
}