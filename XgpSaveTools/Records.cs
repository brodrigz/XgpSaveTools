namespace XgpSaveTools.Records;

public record GameInfoJson(List<GameInfo> Games);
public record GameInfo(
	string Name,
	string Package,
	string Handler,
	HandlerArgs? HandlerArgs,
	string Source = "wgs",
	SourceArgs? SourceArgs = null);
public record UnregisteredGameInfo(
	string Name,
	string Package,
	string Handler,
	HandlerArgs? HandlerArgs,
	string Source = "wgs",
	SourceArgs? SourceArgs = null)
	: GameInfo(Name, Package, Handler, HandlerArgs, Source, SourceArgs);
public record HandlerArgs(string? Suffix, string? IconFormat);
public record SourceArgs(string? GameId);
public record UserContainerFolder(
	string UserTag,
	string Dir,
	string Source = "wgs",
	string? SnapshotId = null);
public record ContainerMetaFile(string Name, int Number, List<ContainerEntry> Files);
public record ContainerEntry(string Name, string Path);
public record PgsSaveFile(string RelativePath, string Path, bool IsMetadata);
public record PgsSnapshot(
	string UserRoot,
	string Xuid,
	string GameId,
	string SnapshotId,
	string SnapshotRoot,
	string ContainersRoot,
	IReadOnlyList<PgsSaveFile> Files,
	bool SelectedThroughCurrent);
