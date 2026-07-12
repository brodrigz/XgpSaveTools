using XgpSaveTools.Operations;

namespace XgpSaveTools.SaveHandlers.Impl;

public sealed class Persona3ReloadHandler : ExportOnlyGameSaveHandler
{
	private const string Key = "ae5zeitaix1joowooNgie3fahP5Ohph";

	public override string Id => "persona-3-reload";

	protected override Task<IReadOnlyList<ExportArtifact>> PrepareExportAsync(
		GameSaveContext context,
		ITempWorkspace workspace,
		CancellationToken cancellationToken)
	{
		var artifacts = new List<ExportArtifact>();
		foreach (var container in context.Containers)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var fileName = container.Name + ".sav";
			var data = File.ReadAllBytes(container.Files[0].Path);
			var output = new byte[data.Length];
			for (var index = 0; index < data.Length; index++)
			{
				var value = data[index];
				var transformed = (byte)(((value >> 4) & 0x03) | ((value & 0x03) << 4) | (value & 0xCC));
				output[index] = (byte)(transformed ^ (byte)Key[index % Key.Length]);
			}

			var destination = workspace.GetPath(Path.Combine("P3R", fileName));
			File.WriteAllBytes(destination, output);
			artifacts.Add(new ExportArtifact(fileName, destination));
		}
		return Task.FromResult<IReadOnlyList<ExportArtifact>>(artifacts);
	}
}
