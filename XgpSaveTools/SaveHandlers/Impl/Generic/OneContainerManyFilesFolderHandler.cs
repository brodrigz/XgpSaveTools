using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class OneContainerManyFilesFolderHandler : ISaveHandler
    {
        public bool CanHandle(string handlerName) => handlerName == "1cnf-folder";

        public IEnumerable<SaveFile> GetSaveEntries(List<ContainerMetaFile> containers, HandlerArgs? args)
        {
            foreach (var c in containers)
            {
                var folder = c.Name;
                foreach (var f in c.Files)
                {
                    var entry = Path.Combine(folder, f.Name).Replace('\\', '/');
                    yield return new(entry, f);
                }
            }
        }
    }
}