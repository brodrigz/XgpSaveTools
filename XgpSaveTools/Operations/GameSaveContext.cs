using XgpSaveTools.Records;

namespace XgpSaveTools.Operations;

public sealed record GameSaveContext(
	GameInfo Game,
	UserContainerFolder UserContainer,
	string StorePackage,
	IReadOnlyList<ContainerMetaFile> Containers);
