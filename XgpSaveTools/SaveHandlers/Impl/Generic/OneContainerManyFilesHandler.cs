using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class OneContainerManyFilesHandler : ISaveHandler
    {
        public bool CanHandle(string handlerName) => handlerName == "1cnf";

        public IEnumerable<SaveFile> GetSaveEntries(List<ContainerMetaFile> containers, HandlerArgs? args)
        {
            var c = containers.First();
            foreach (var file in c.Files)
            {
                var name = file.Name + (args?.Suffix ?? string.Empty);
                yield return new(name, file);
            }
        }
    }
}