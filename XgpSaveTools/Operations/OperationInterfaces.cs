namespace XgpSaveTools.Operations;

public interface IGameSaveHandler
{
	string Id { get; }
	IReadOnlyList<IGameSaveOperation> GetOperations(GameSaveContext context);
}

public interface IGameSaveOperation
{
	OperationDefinition Definition { get; }
	IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context);
	Task<OperationPlan> PrepareAsync(
		GameSaveContext context,
		OperationArguments arguments,
		ITempWorkspace workspace,
		CancellationToken cancellationToken);
}

public interface IOperationInputProvider
{
	Task<OperationArguments?> CollectAsync(
		IReadOnlyList<OperationParameter> parameters,
		CancellationToken cancellationToken);
}
