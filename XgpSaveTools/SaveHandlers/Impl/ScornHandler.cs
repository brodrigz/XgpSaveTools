using XgpSaveTools.Extensions;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class ScornHandler : OneContainerOneFileHandler
    {
        private readonly string[] suffixes = new[] { "dat", "sav", "info" };

        public override bool CanHandle(string handlerName) => handlerName == "scorn";

        private SaveFile FixSuffix(SaveFile file)
        {
            if (!file.OutputName.EndsWithAny(suffixes)) return file;

            string fixedName = StringExtensions.FixMissingDotOnExtension(file.OutputName, suffixes);
            return new SaveFile(fixedName, file.ContainerEntry);
        }

        public override IEnumerable<SaveFile> GetSaveEntries(List<ContainerMetaFile> containers, HandlerArgs? args)
        {
            foreach (var baseResult in base.GetSaveEntries(containers, args)) yield return FixSuffix(baseResult);
        }
    }
}