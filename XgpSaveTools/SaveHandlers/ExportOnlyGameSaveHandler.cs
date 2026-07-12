using XgpSaveTools.Operations;

namespace XgpSaveTools.SaveHandlers;

public abstract class ExportOnlyGameSaveHandler : IGameSaveHandler
{
	public abstract string Id { get; }

	public IReadOnlyList<IGameSaveOperation> GetOperations(GameSaveContext context) =>
		new IGameSaveOperation[] { new ExportOperation(this) };

	protected abstract Task<IReadOnlyList<ExportArtifact>> PrepareExportAsync(
		GameSaveContext context,
		ITempWorkspace workspace,
		CancellationToken cancellationToken);

	private sealed class ExportOperation : IGameSaveOperation
	{
		private readonly ExportOnlyGameSaveHandler _handler;

		public ExportOperation(ExportOnlyGameSaveHandler handler) => _handler = handler;

		public OperationDefinition Definition { get; } = new(
			"extract", "Extract Files", "Extract and transform save files.", OperationKind.Export);

		public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context) =>
			Array.Empty<OperationParameter>();

		public async Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			var files = await _handler.PrepareExportAsync(context, workspace, cancellationToken);
			return new ExportPlan(files, ArchiveName.Create(context));
		}
	}
}
