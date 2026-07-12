using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;
using XgpSaveTools.Operations;

namespace XgpSaveTools.SaveHandlers.Impl;

public sealed class OneLonelyOutpostHandler : ExportOnlyGameSaveHandler
{
	public override string Id => "one-lonely-outpost";

	protected override Task<IReadOnlyList<ExportArtifact>> PrepareExportAsync(
		GameSaveContext context,
		ITempWorkspace workspace,
		CancellationToken cancellationToken)
	{
		if (context.Containers.Count == 0 || context.Containers[0].Files.Count == 0)
			return Task.FromResult<IReadOnlyList<ExportArtifact>>(Array.Empty<ExportArtifact>());

		string json;
		using (var source = File.OpenRead(context.Containers[0].Files[0].Path))
		using (var gzip = new GZipStream(source, CompressionMode.Decompress))
		using (var output = new MemoryStream())
		{
			gzip.CopyTo(output);
			json = Encoding.UTF8.GetString(output.ToArray());
		}

		JObject root;
		try
		{
			root = JObject.Parse(json);
		}
		catch (Exception exception)
		{
			throw new InvalidDataException("Failed to parse decompressed JSON for One Lonely Outpost.", exception);
		}

		var artifacts = new List<ExportArtifact>();
		if (root.SelectToken("files.$values") is not JArray files) return Task.FromResult<IReadOnlyList<ExportArtifact>>(artifacts);
		foreach (var file in files)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var rawName = file["name"]?.ToString();
			if (string.IsNullOrEmpty(rawName)) continue;
			const string prefix = "ConsoleSaves/";
			var name = rawName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? rawName[prefix.Length..] : rawName;
			var data = file.SelectToken("datas.$values[0]");
			if (data == null) continue;
			var destination = workspace.GetPath(name);
			File.WriteAllText(destination, data.ToString());
			artifacts.Add(new ExportArtifact(name.Replace('\\', '/'), destination));
		}
		return Task.FromResult<IReadOnlyList<ExportArtifact>>(artifacts);
	}
}
