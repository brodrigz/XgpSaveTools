using XgpSaveTools.Operations;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveSources;

public interface IGameSaveSource
{
	string Id { get; }
	IReadOnlyList<UserContainerFolder> FindUserContainers(GameInfo game);
	GameSaveContext CreateContext(GameInfo game, UserContainerFolder location);
}
