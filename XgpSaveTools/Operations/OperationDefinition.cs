namespace XgpSaveTools.Operations;

public enum OperationKind
{
	Export,
	Import,
	Maintenance
}

public sealed record OperationDefinition(
	string Id,
	string DisplayName,
	string Description,
	OperationKind Kind);
