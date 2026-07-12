using System.Text;
using XgpSaveTools.Operations;

namespace XgpSaveTools.SaveHandlers.Impl;

public sealed class StarfieldHandler : ExportOnlyGameSaveHandler
{
	public override string Id => "starfield";

	protected override Task<IReadOnlyList<ExportArtifact>> PrepareExportAsync(
		GameSaveContext context,
		ITempWorkspace workspace,
		CancellationToken cancellationToken)
	{
		var artifacts = new List<ExportArtifact>();
		var padding = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("padding\0", 2)));
		foreach (var container in context.Containers)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var logicalPath = container.Name.Replace("\\", "/");
			if (!logicalPath.StartsWith("Saves/", StringComparison.Ordinal)) continue;

			var saveName = Path.GetFileName(logicalPath);
			var parts = new SortedDictionary<int, string>();
			var isNewFormat = container.Files.Any(x => x.Name == "toc");
			foreach (var entry in container.Files)
			{
				if (entry.Name == "toc") continue;
				var index = isNewFormat
					? int.Parse(entry.Name.Replace("BlobData", string.Empty))
					: entry.Name == "BETHESDAPFH" ? 0 : int.Parse(entry.Name.TrimStart('P')) + 1;
				parts[index] = entry.Path;
			}

			var output = workspace.GetPath(Path.Combine("Starfield", saveName));
			using (var stream = File.Create(output))
			foreach (var part in parts.Values)
			{
				var data = File.ReadAllBytes(part);
				stream.Write(data, 0, data.Length);
				var padLength = 16 - data.Length % 16;
				if (padLength < 16) stream.Write(padding, 0, padLength);
			}
			artifacts.Add(new ExportArtifact(saveName, output));
		}
		return Task.FromResult<IReadOnlyList<ExportArtifact>>(artifacts);
	}
}
