using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XgpSaveTools.Records;
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

		public static IEnumerable<GameInfo> DiscoverUserGames(IEnumerable<GameInfo>? supportedGameList = null)
		{
			var games = supportedGameList ?? ReadGameList();

			var root = new DirectoryInfo(PackagesRoot);
			foreach (var wgsDir in root.GetDirectories("wgs", SearchOption.AllDirectories))
			{
				string? packageName = wgsDir?.Parent?.Parent?.Name;
				if (string.IsNullOrEmpty(packageName)) continue;

				//supported
				var supported = games.FirstOrDefault(x => x.Package == packageName);
				if (supported != null)
				{
					yield return supported;
					continue;
				}
				//Unregistered
				yield return new UnregisteredGameInfo(packageName, packageName, "generic", null);
			}
			//return games.Where(x => Directory.Exists(Path.Combine(PackagesRoot, x.Package))); //doesnt look for anything outside supported game list
		}
	}
}
