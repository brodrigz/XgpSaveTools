using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class LiesOfPHandler : ISaveHandler
    {
        public bool CanHandle(string handlerName) => handlerName == "lies-of-p";

        public IEnumerable<SaveFile> GetSaveEntries(
            List<ContainerMetaFile> containers,
            HandlerArgs? handlerArgs)
        {
            foreach (var c in containers)
            {
                string name = c.Name;
                int i = 0;
                while (i < name.Length && char.IsDigit(name[i])) i++;
                name = name[i..] + ".sav";

                // always exactly one file in each container
                yield return new SaveFile(name, c.Files[0]);
            }
        }
    }
}