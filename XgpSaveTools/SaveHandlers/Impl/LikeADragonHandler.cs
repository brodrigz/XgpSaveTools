using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class LikeADragonHandler : ISaveHandler
    {
        public bool CanHandle(string handlerName) => handlerName == "like-a-dragon";

        public IEnumerable<SaveFile> GetSaveEntries(
            List<ContainerMetaFile> containers,
            HandlerArgs? handlerArgs)
        {
            var iconFmt = handlerArgs?.IconFormat;
            foreach (var c in containers)
            {
                var path = System.IO.Path.GetDirectoryName(c.Name) ?? "";
                var leaf = System.IO.Path.GetFileName(c.Name);
                string baseName = leaf switch
                {
                    "datasav" => System.IO.Path.Combine(path, "data.sav"),
                    "datasys" => System.IO.Path.Combine(path, "data.sys"),
                    _ => c.Name
                };

                foreach (var fe in c.Files)
                {
                    if (fe.Name.Equals("data", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return new SaveFile(baseName, fe);
                    }
                    else if (fe.Name.Equals("icon", StringComparison.OrdinalIgnoreCase)
                             && iconFmt is not null)
                    {
                        var parent = System.IO.Path.GetFileName(path);
                        var iconName = System.IO.Path.ChangeExtension(
                            System.IO.Path.Combine(path, parent + "_icon"),
                            iconFmt);
                        yield return new SaveFile(iconName, fe);
                    }
                }
            }
        }
    }
}