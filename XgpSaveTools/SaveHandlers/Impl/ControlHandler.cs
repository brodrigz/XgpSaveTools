using XgpSaveTools.Operations;

namespace XgpSaveTools.SaveHandlers.Impl;

public sealed class ControlHandler : ExportOnlyGameSaveHandler
{
	public override string Id => "control";

	protected override Task<IReadOnlyList<ExportArtifact>> PrepareExportAsync(
		GameSaveContext context,
		ITempWorkspace workspace,
		CancellationToken cancellationToken)
	{
		var artifacts = new List<ExportArtifact>();
		foreach (var container in context.Containers)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var displayName = workspace.GetPath(Path.Combine(container.Name, "--containerDisplayName.chunk"));
			File.WriteAllText(displayName, container.Name);
			artifacts.Add(new ExportArtifact($"{container.Name}/--containerDisplayName.chunk", displayName));

			foreach (var entry in container.Files)
				artifacts.Add(new ExportArtifact($"{container.Name}/{entry.Name}.chunk", entry.Path));
		}
		return Task.FromResult<IReadOnlyList<ExportArtifact>>(artifacts);
	}
}
