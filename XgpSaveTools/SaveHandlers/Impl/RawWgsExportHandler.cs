using XgpSaveTools.Operations;

namespace XgpSaveTools.SaveHandlers.Impl;

public sealed class RawWgsExportHandler : IGameSaveHandler
{
	public RawWgsExportHandler(string id) => Id = id;

	public string Id { get; }

	public IReadOnlyList<IGameSaveOperation> GetOperations(GameSaveContext context) =>
		new IGameSaveOperation[] { new RawExportOperation() };

	private sealed class RawExportOperation : IGameSaveOperation
	{
		public OperationDefinition Definition { get; } = new(
			"extract-raw", "Export Raw Xbox Data",
			"Archive WGS entries without claiming storefront conversion.", OperationKind.Export);

		public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context) =>
			Array.Empty<OperationParameter>();

		public Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			var files = new List<ExportArtifact>();
			foreach (var container in context.Containers)
			foreach (var entry in container.Files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var name = Path.Combine(container.Name, entry.Name).Replace('\\', '/');
				files.Add(new ExportArtifact(name, entry.Path));
			}

			OperationPlan plan = new ExportPlan(
				files,
				ArchiveName.Create(context),
				new[]
				{
					"These are raw Xbox save payloads. They are not known to be directly compatible with Steam."
				});
			return Task.FromResult(plan);
		}
	}
}
