using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class RailwayEmpire2Handler : ISaveHandler
    {
        public bool CanHandle(string handlerName) => handlerName == "railway-empire-2";

        public IEnumerable<SaveFile> GetSaveEntries(
            List<ContainerMetaFile> containers,
            HandlerArgs? handlerArgs)
        {
            foreach (var c in containers)
            {
                foreach (var fe in c.Files)
                {
                    if (!fe.Name.Equals("savegame", StringComparison.OrdinalIgnoreCase))
                        continue;
                    yield return new SaveFile(c.Name, fe);
                }
            }
        }
    }
}