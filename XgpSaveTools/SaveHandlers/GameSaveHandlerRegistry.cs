using XgpSaveTools.Operations;
using XgpSaveTools.Records;
using XgpSaveTools.SaveHandlers.Impl;
using XgpSaveTools.SaveHandlers.Impl.DoomDarkAges;

namespace XgpSaveTools.SaveHandlers;

public static class GameSaveHandlerRegistry
{
	private static readonly IGameSaveHandler Generic =
		new StandardGameSaveHandler("generic", StandardGameMappings.Generic);

	private static readonly IReadOnlyDictionary<string, IGameSaveHandler> Handlers =
		new Dictionary<string, IGameSaveHandler>(StringComparer.OrdinalIgnoreCase)
		{
			["generic"] = Generic,
			["1c1f"] = new StandardGameSaveHandler("1c1f", StandardGameMappings.OneFilePerContainer),
			["1cnf"] = new StandardGameSaveHandler("1cnf", StandardGameMappings.FirstContainer),
			["1cnf-folder"] = new StandardGameSaveHandler("1cnf-folder", StandardGameMappings.ContainerFolders),
			["arcade-paradise"] = new StandardGameSaveHandler("arcade-paradise", StandardGameMappings.ArcadeParadise),
			["balatro"] = new StandardGameSaveHandler("balatro", StandardGameMappings.Balatro),
			["coral-island"] = new StandardGameSaveHandler("coral-island", StandardGameMappings.CoralIsland),
			["cricket-24"] = new StandardGameSaveHandler("cricket-24", StandardGameMappings.Cricket24),
			["forza"] = new StandardGameSaveHandler("forza", StandardGameMappings.Forza),
			["galacticare"] = new StandardGameSaveHandler("galacticare", StandardGameMappings.Galacticare),
			["lies-of-p"] = new StandardGameSaveHandler("lies-of-p", StandardGameMappings.LiesOfP),
			["like-a-dragon"] = new StandardGameSaveHandler("like-a-dragon", StandardGameMappings.LikeADragon),
			["metaphor-refantazio"] = new StandardGameSaveHandler("metaphor-refantazio", StandardGameMappings.MetaphorRefantazio),
			["palworld"] = new StandardGameSaveHandler("palworld", StandardGameMappings.Palworld),
			["railway-empire-2"] = new StandardGameSaveHandler("railway-empire-2", StandardGameMappings.RailwayEmpire2),
			["scorn"] = new StandardGameSaveHandler("scorn", StandardGameMappings.Scorn),
			["silksong"] = new StandardGameSaveHandler("silksong", StandardGameMappings.Silksong),
			["state-of-decay-2"] = new StandardGameSaveHandler("state-of-decay-2", StandardGameMappings.StateOfDecay2),
			["control"] = new ControlHandler(),
			["fallout-4"] = new Fallout4Handler(),
			["one-lonely-outpost"] = new OneLonelyOutpostHandler(),
			["persona-3-reload"] = new Persona3ReloadHandler(),
			["pgs-files"] = new PgsGameSaveHandler(),
			[PgsForzaHandler.HandlerId] = new PgsForzaHandler(),
			["starfield"] = new StarfieldHandler(),
			[DoomDarkAgesHandler.HandlerId] = new DoomDarkAgesHandler(),
			[DoomEternalHandler.HandlerId] = new DoomEternalHandler()
		};

	public static IGameSaveHandler Resolve(GameInfo game) =>
		Handlers.TryGetValue(game.Handler, out var handler) ? handler : Generic;
}
