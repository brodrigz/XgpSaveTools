using XgpSaveTools.Records;

namespace XgpSaveTools.Operations;

public sealed record GameSaveContext(
	GameInfo Game,
	UserContainerFolder UserContainer,
	string StorePackage,
	IReadOnlyList<ContainerMetaFile> Containers,
	PgsSnapshot? PgsSnapshot = null)
{
	public string Source => string.IsNullOrWhiteSpace(Game.Source) ? "wgs" : Game.Source;
}
