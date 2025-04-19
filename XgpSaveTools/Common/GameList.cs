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

		public static IEnumerable<GameInfo> DiscoverGames(IEnumerable<GameInfo>? gameList = null)
		{
			var games = gameList ?? ReadGameList();
			return games.Where(x => Directory.Exists(Path.Combine(PackagesRoot, x.Package)));
		}
	}
}
