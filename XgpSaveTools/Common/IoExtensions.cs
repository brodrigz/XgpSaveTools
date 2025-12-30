namespace XgpSaveTools.Extensions
{
    public static class IoExtensions
    {
        // Directories
        public static readonly string PackagesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");

        public static readonly string GameListPath = Path.Combine(AppContext.BaseDirectory, "games.json");
        public static readonly string BackupOutput = Path.Combine(AppContext.BaseDirectory, "Backups");

        private static readonly object _lock = new();
        private static readonly List<DirectoryInfo> TempFolders = new();

        public static DirectoryInfo CreateTempFolder()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var dir = Directory.CreateDirectory(temp);
            lock (_lock)
            {
                TempFolders.Add(dir);
            }
            return dir;
        }

        public static void ClearTempFolders()
        {
            foreach (var folder in TempFolders)
            {
                try
                {
                    folder.Delete(true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to delete temp folder '{folder.FullName}': {ex.Message}");
                }
            }
            TempFolders.Clear();
        }

        // Recursively copy a directory
        public static string CopyDirectory(string sourceDir, string targetDir)
        {
            var diSource = new DirectoryInfo(sourceDir);
            var diTarget = new DirectoryInfo(targetDir);

            if (!diSource.Exists)
                throw new DirectoryNotFoundException(sourceDir);

            if (diTarget.Exists) diTarget.Delete(true);
            Directory.CreateDirectory(diTarget.FullName);

            // Copy files
            foreach (var file in diSource.GetFiles())
            {
                string targetFilePath = Path.Combine(diTarget.FullName, file.Name);
                file.CopyTo(targetFilePath, overwrite: true);
            }

            // Copy subdirectories
            foreach (var subDir in diSource.GetDirectories())
            {
                string newTargetDir = Path.Combine(diTarget.FullName, subDir.Name);
                CopyDirectory(subDir.FullName, newTargetDir);
            }

            return targetDir;
        }

        public static string GetReadableFileSize(long byteCount)
        {
            string[] sizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int order = 0;
            double len = byteCount;

            while (len >= 1024 && order < sizeSuffixes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizeSuffixes[order]}";
        }

        public static string Unquote(this string input)
        {
            return input.Length >= 2 &&
                   ((input.StartsWith("\"") && input.EndsWith("\"")) ||
                    (input.StartsWith("'") && input.EndsWith("'")))
                ? input.Substring(1, input.Length - 2)
                : input;
        }
    }
}