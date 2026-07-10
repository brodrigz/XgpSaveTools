using XgpSaveTools.Operations;
using XgpSaveTools.Records;
using XgpSaveTools.SaveHandlers.Impl.DoomDarkAges;

namespace XgpSaveTools.SaveHandlers;

public static class GameSaveHandlerFactory
{
	public static IGameSaveHandler Get(GameInfo game)
	{
		if (game.Handler == DoomDarkAgesHandler.HandlerId) return new DoomDarkAgesHandler();
		if (game.Handler == DoomEternalHandler.HandlerId) return new DoomEternalHandler();
		var legacyHandler = SaveHandlerFactory.Get(game.Handler);
		return new LegacySaveHandlerAdapter(game.Handler, legacyHandler);
	}
}
