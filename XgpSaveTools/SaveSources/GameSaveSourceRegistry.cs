using XgpSaveTools.Records;

namespace XgpSaveTools.SaveSources;

public sealed class GameSaveSourceResolver
{
	private readonly IReadOnlyDictionary<string, IGameSaveSource> _sources;

	public GameSaveSourceResolver(
		WgsGameSaveSource? wgs = null,
		PgsGameSaveSource? pgs = null)
	{
		Wgs = wgs ?? new WgsGameSaveSource();
		Pgs = pgs ?? new PgsGameSaveSource();
		_sources = new Dictionary<string, IGameSaveSource>(StringComparer.OrdinalIgnoreCase)
		{
			[Wgs.Id] = Wgs,
			[Pgs.Id] = Pgs
		};
	}

	public WgsGameSaveSource Wgs { get; }
	public PgsGameSaveSource Pgs { get; }

	public IGameSaveSource Resolve(GameInfo game)
	{
		var id = string.IsNullOrWhiteSpace(game.Source) ? "wgs" : game.Source;
		return _sources.TryGetValue(id, out var source)
			? source
			: throw new InvalidDataException($"Unknown save source '{id}' for {game.Name}.");
	}
}

public static class GameSaveSourceRegistry
{
	public static GameSaveSourceResolver Default { get; } = new();

	public static IGameSaveSource Resolve(GameInfo game) => Default.Resolve(game);

	public static PgsGameSaveSource Pgs => Default.Pgs;
	public static WgsGameSaveSource Wgs => Default.Wgs;
}
