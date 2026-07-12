namespace XgpSaveTools.Operations;

public abstract record OperationPlan;

public sealed record ExportArtifact(string OutputName, string PreparedFile);

public sealed record ExportPlan(
	IReadOnlyList<ExportArtifact> Files,
	string SuggestedArchiveName)
	: OperationPlan;

public sealed record WgsEntryKey(string ContainerName, string FileName);

public abstract record PlannedWgsMutation(WgsEntryKey Target);

public sealed record PlannedReplacement(WgsEntryKey Entry, string PreparedFile)
	: PlannedWgsMutation(Entry);

public sealed record PlannedDeletion(WgsEntryKey Entry, bool DeleteContainerFolder = false)
	: PlannedWgsMutation(Entry);

public sealed record ImportPlan(
	IReadOnlyList<PlannedWgsMutation> Mutations,
	IReadOnlyList<string> Warnings)
	: OperationPlan;
