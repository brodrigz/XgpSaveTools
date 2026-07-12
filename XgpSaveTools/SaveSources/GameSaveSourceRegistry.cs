using XgpSaveTools.Records;

namespace XgpSaveTools.SaveSources;

public static class GameSaveSourceRegistry
{
	private static readonly IReadOnlyDictionary<string, IGameSaveSource> Sources =
		new Dictionary<string, IGameSaveSource>(StringComparer.OrdinalIgnoreCase)
		{
			["wgs"] = new WgsGameSaveSource(),
			["pgs"] = new PgsGameSaveSource()
		};

	public static IGameSaveSource Resolve(GameInfo game)
	{
		var id = string.IsNullOrWhiteSpace(game.Source) ? "wgs" : game.Source;
		return Sources.TryGetValue(id, out var source)
			? source
			: throw new InvalidDataException($"Unknown save source '{id}' for {game.Name}.");
	}

	public static PgsGameSaveSource Pgs => (PgsGameSaveSource)Sources["pgs"];
	public static WgsGameSaveSource Wgs => (WgsGameSaveSource)Sources["wgs"];
}
