using Newtonsoft.Json.Linq;
using System;
using System.IO.Compression;
using System.Linq;
using System.Text;
using XgpSaveTools.Extensions;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class OneLonelyOutpostHandler : ISaveHandler
    {
        public bool CanHandle(string handlerName) => handlerName == "one-lonely-outpost";

        public IEnumerable<SaveFile> GetSaveEntries(
            List<ContainerMetaFile> containers,
            HandlerArgs? handlerArgs)
        {
            if (containers == null || containers.Count == 0)
                yield break;

            var container = containers[0];
            if (container.Files == null || container.Files.Count == 0)
                yield break;

            // tmp subfolder for extraction
            var tempRoot = IoExtensions.CreateTempFolder();
            var outDir = Path.Combine(tempRoot.FullName, "OneLonelyOutpost");
            Directory.CreateDirectory(outDir);

            // decompress gz json
            string jsonText;
            var blobPath = container.Files[0].Path;
            using (var fs = File.OpenRead(blobPath))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            using (var ms = new MemoryStream())
            {
                gz.CopyTo(ms);
                jsonText = Encoding.UTF8.GetString(ms.ToArray());
            }

            JObject root;
            try
            {
                root = JObject.Parse(jsonText);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Failed to parse decompressed JSON for One Lonely Outpost", ex);
            }

            var filesArray = root.SelectToken("files.$values") as JArray;
            if (filesArray == null)
                yield break;

            foreach (var file in filesArray)
            {
                var rawName = file["name"]?.ToString();
                if (string.IsNullOrEmpty(rawName)) continue;

                const string prefix = "ConsoleSaves/";
                var fname = rawName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? rawName[prefix.Length..]
                    : rawName;

                var destPath = Path.Combine(outDir, fname);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                var dataToken = file.SelectToken("datas.$values[0]");
                if (dataToken == null)
                    continue;

                File.WriteAllText(destPath, dataToken.ToString());
                yield return new SaveFile(fname, destPath);
            }
        }
    }
}
