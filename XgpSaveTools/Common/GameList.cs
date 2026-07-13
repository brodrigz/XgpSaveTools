using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using XgpSaveTools.Records;
using XgpSaveTools.SaveSources;
using static XgpSaveTools.Extensions.IoExtensions;

namespace XgpSaveTools.Common
{
    public static class GameList
    {
        private static JsonSerializerSettings SerializerSettings => new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        public static List<GameInfo> ReadGameList()
        {
            if (!File.Exists(GameListPath)) throw new FileNotFoundException(GameListPath);

            string raw = File.ReadAllText(GameListPath);
            var wrapper = JsonConvert.DeserializeObject<GameInfoJson>(raw, SerializerSettings);
            return wrapper?.Games ?? throw new Exception($"Failed to read {GameListPath}");
        }

        public static IEnumerable<GameInfo> DiscoverUserGames(
            IEnumerable<GameInfo>? supportedGameList = null,
            GameSaveSourceResolver? sources = null)
        {
            var games = (supportedGameList ?? ReadGameList()).ToList();
            sources ??= GameSaveSourceRegistry.Default;
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var packageName in sources.Wgs.EnumeratePackageNames())
            {
                //supported
                var supported = games.FirstOrDefault(x =>
                    x.Package == packageName &&
                    (string.IsNullOrWhiteSpace(x.Source) || x.Source.Equals("wgs", StringComparison.OrdinalIgnoreCase)));
                if (supported != null)
                {
                    if (yielded.Add($"wgs:{supported.Package}")) yield return supported;
                    continue;
                }

                // A PGS package can retain an empty compatibility wgs directory.
                if (games.Any(x => x.Package == packageName)) continue;

                //Unregistered
                if (yielded.Add($"wgs:{packageName}"))
                    yield return new UnregisteredGameInfo(packageName, packageName, "generic", null);
            }

            foreach (var pgsRoot in sources.Pgs.EnumerateUserRoots())
            {
                var supported = games.FirstOrDefault(x =>
                    string.Equals(x.Source, "pgs", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.SourceArgs?.GameId, pgsRoot.GameId, StringComparison.OrdinalIgnoreCase));
                if (supported != null)
                {
                    if (yielded.Add($"pgs:{pgsRoot.GameId}")) yield return supported;
                    continue;
                }

                if (yielded.Add($"pgs:{pgsRoot.GameId}"))
                    yield return new UnregisteredGameInfo(
                        $"PGS game {pgsRoot.GameId}",
                        $"pgs:{pgsRoot.GameId}",
                        "pgs-files",
                        null,
                        "pgs",
                        new SourceArgs(pgsRoot.GameId));
            }
        }
    }
}
