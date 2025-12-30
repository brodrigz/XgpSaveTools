using XgpSaveTools.Extensions;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl
{
    public class Persona3ReloadHandler : ISaveHandler
    {
        private const string Key = "ae5zeitaix1joowooNgie3fahP5Ohph";

        public bool CanHandle(string handlerName) => handlerName == "persona-3-reload";

        public IEnumerable<SaveFile> GetSaveEntries(
            List<ContainerMetaFile> containers,
            HandlerArgs? handlerArgs)
        {
            // create a temp subfolder for the encrypted files
            var tmp = IoExtensions.CreateTempFolder();
            var p3rDir = Path.Combine(tmp.FullName, "P3R");
            Directory.CreateDirectory(p3rDir);

            int keyLen = Key.Length;
            foreach (var c in containers)
            {
                // original filename + .sav
                string fileName = c.Name + ".sav";
                string sourcePath = c.Files[0].Path;
                string destPath = Path.Combine(p3rDir, fileName);

                // read, transform, write
                byte[] data = File.ReadAllBytes(sourcePath);
                byte[] output = new byte[data.Length];

                for (int i = 0; i < data.Length; i++)
                {
                    byte b = data[i];
                    byte transformed = (byte)(
                        ((b >> 4) & 0x03) | // upper 2 bits
                        ((b & 0x03) << 4) | // lower 2 bits
                        (b & 0xCC)            // middle bits
                    );
                    byte keyByte = (byte)Key[i % keyLen];
                    output[i] = (byte)(transformed ^ keyByte);
                }

                File.WriteAllBytes(destPath, output);
                yield return new SaveFile(fileName, destPath);
            }
        }
    }
}