namespace XgpSaveTools.Records;

public record GameInfoJson(List<GameInfo> Games);
public record GameInfo(string Name, string Package, string Handler, HandlerArgs? HandlerArgs);
public record UnregisteredGameInfo(string Name, string Package, string Handler, HandlerArgs? HandlerArgs)
	: GameInfo(Name, Package, Handler, HandlerArgs);
public record HandlerArgs(string? Suffix, string? IconFormat);
public record UserContainerFolder(string UserTag, string Dir);
public record ContainerMetaFile(string Name, int Number, List<ContainerEntry> Files);
public record ContainerEntry(string Name, string Path);
