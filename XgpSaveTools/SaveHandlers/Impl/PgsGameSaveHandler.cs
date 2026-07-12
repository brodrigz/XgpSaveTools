using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using XgpSaveTools.Operations;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl;

public class PgsGameSaveHandler : IGameSaveHandler
{
	private static readonly IReadOnlyList<string> BackupWarnings = new[]
	{
		"Close the game and allow 30 seconds for cloud synchronization before exporting.",
		"A complete PGS backup contains Xbox user and device metadata. Treat the archive as private."
	};

	public PgsGameSaveHandler(string id = "pgs-files") => Id = id;

	public string Id { get; }

	public IReadOnlyList<IGameSaveOperation> GetOperations(GameSaveContext context) =>
		new IGameSaveOperation[]
		{
			new BackupOperation(),
			new ExtractFilesOperation()
		};

	private sealed class BackupOperation : IGameSaveOperation
	{
		public OperationDefinition Definition { get; } = new(
			"backup-pgs", "Export Complete PGS Backup",
			"Archive the active PGS snapshot, metadata, and an integrity manifest.",
			OperationKind.Export);

		public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context) =>
			Array.Empty<OperationParameter>();

		public Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			var snapshot = RequireSnapshot(context);
			var captured = Capture(context, snapshot, snapshot.Files, workspace, true, cancellationToken);
			var archiveName = ArchiveName.Create(context).Replace(".zip", "_pgs-backup.zip");
			return Task.FromResult<OperationPlan>(new ExportPlan(captured, archiveName, BackupWarnings));
		}
	}

	private sealed class ExtractFilesOperation : IGameSaveOperation
	{
		public OperationDefinition Definition { get; } = new(
			"extract", "Extract Game Files",
			"Extract the active PGS ContainersRoot tree without metadata.",
			OperationKind.Export);

		public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context) =>
			Array.Empty<OperationParameter>();

		public Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			var snapshot = RequireSnapshot(context);
			var selected = snapshot.Files.Where(x => !x.IsMetadata).ToList();
			var captured = Capture(context, snapshot, selected, workspace, false, cancellationToken);
			var archiveName = ArchiveName.Create(context).Replace(".zip", "_game-files.zip");
			return Task.FromResult<OperationPlan>(new ExportPlan(
				captured,
				archiveName,
				new[] { "Close the game and allow 30 seconds for cloud synchronization before exporting." }));
		}
	}

	private static PgsSnapshot RequireSnapshot(GameSaveContext context) =>
		context.PgsSnapshot ?? throw new InvalidOperationException(
			$"Handler '{context.Game.Handler}' requires a PGS save context.");

	private static IReadOnlyList<ExportArtifact> Capture(
		GameSaveContext context,
		PgsSnapshot snapshot,
		IReadOnlyList<PgsSaveFile> selected,
		ITempWorkspace workspace,
		bool includeManifest,
		CancellationToken cancellationToken)
	{
		if (selected.Count == 0) throw new InvalidDataException("The PGS snapshot contains no exportable files.");
		var before = selected.ToDictionary(x => x.RelativePath, Fingerprint.Create, StringComparer.OrdinalIgnoreCase);
		var artifacts = new List<ExportArtifact>();
		var manifestFiles = new List<PgsBackupFile>();
		var outputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var file in selected.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var outputName = includeManifest
				? file.IsMetadata
					? $"metadata/{Path.GetFileName(file.RelativePath)}"
					: $"snapshot/{file.RelativePath}"
				: file.RelativePath;
			ValidateArchivePath(outputName);
			if (!outputNames.Add(outputName))
				throw new InvalidDataException($"Duplicate PGS archive path: {outputName}");

			var destination = workspace.GetPath(Path.Combine("pgs-capture", outputName.Replace('/', Path.DirectorySeparatorChar)));
			File.Copy(file.Path, destination, false);
			using var hashStream = File.OpenRead(destination);
			using var sha256 = SHA256.Create();
			var hash = Convert.ToHexString(sha256.ComputeHash(hashStream)).ToLowerInvariant();
			artifacts.Add(new ExportArtifact(outputName, destination));
			manifestFiles.Add(new PgsBackupFile(outputName, new FileInfo(destination).Length, hash));
		}

		var expectedPaths = selected.Select(x => x.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var currentPaths = EnumerateCurrentPaths(snapshot, selected.Any(x => x.IsMetadata));
		if (!expectedPaths.SetEquals(currentPaths))
			throw new IOException(
				"The PGS file set changed during capture. Close the game, wait for synchronization, and retry.");

		foreach (var file in selected)
		{
			var after = Fingerprint.Create(file);
			if (!before[file.RelativePath].Equals(after))
				throw new IOException(
					$"PGS file changed during capture: {file.RelativePath}. Close the game, wait for synchronization, and retry.");
		}

		if (!includeManifest) return artifacts;

		var manifest = new PgsBackupManifest(
			1,
			"pgs",
			context.Game.Name,
			context.Game.Package,
			snapshot.GameId,
			snapshot.Xuid,
			snapshot.SnapshotId,
			Path.GetFileName(snapshot.UserRoot),
			snapshot.SelectedThroughCurrent ? snapshot.SnapshotId : null,
			DateTimeOffset.UtcNow,
			true,
			manifestFiles);
		var manifestPath = workspace.GetPath(Path.Combine("pgs-capture", "pgs-backup.json"));
		File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		artifacts.Insert(0, new ExportArtifact("pgs-backup.json", manifestPath));
		return artifacts;
	}

	private static HashSet<string> EnumerateCurrentPaths(PgsSnapshot snapshot, bool includeMetadata)
	{
		var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var pending = new Stack<string>();
		pending.Push(snapshot.ContainersRoot);
		while (pending.Count > 0)
		{
			var directory = pending.Pop();
			foreach (var file in Directory.EnumerateFiles(directory))
			{
				if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
				var relative = Path.GetRelativePath(snapshot.ContainersRoot, file).Replace('\\', '/');
				paths.Add($"ContainersRoot/{relative}");
			}

			foreach (var child in Directory.EnumerateDirectories(directory))
			{
				if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
				pending.Push(child);
			}
		}

		if (includeMetadata)
			foreach (var file in Directory.EnumerateFiles(snapshot.UserRoot, "*.json", SearchOption.TopDirectoryOnly))
				if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
					paths.Add(Path.GetFileName(file));
		return paths;
	}

	private static void ValidateArchivePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
			throw new InvalidDataException($"Invalid PGS archive path: {path}");
		var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0 || parts.Any(x => x is "." or ".."))
			throw new InvalidDataException($"Invalid PGS archive path: {path}");
	}

	private sealed record Fingerprint(long Length, DateTime LastWriteUtc)
	{
		public static Fingerprint Create(PgsSaveFile file)
		{
			var info = new FileInfo(file.Path);
			if (!info.Exists) throw new FileNotFoundException("PGS file disappeared during capture.", file.Path);
			return new Fingerprint(info.Length, info.LastWriteTimeUtc);
		}
	}

	private sealed record PgsBackupManifest(
		[property: JsonPropertyName("schema_version")] int SchemaVersion,
		[property: JsonPropertyName("source")] string Source,
		[property: JsonPropertyName("game_name")] string GameName,
		[property: JsonPropertyName("package")] string Package,
		[property: JsonPropertyName("game_id")] string GameId,
		[property: JsonPropertyName("xuid")] string Xuid,
		[property: JsonPropertyName("snapshot_id")] string SnapshotId,
		[property: JsonPropertyName("original_user_root")] string OriginalUserRoot,
		[property: JsonPropertyName("original_current_target")] string? OriginalCurrentTarget,
		[property: JsonPropertyName("captured_utc")] DateTimeOffset CapturedUtc,
		[property: JsonPropertyName("consistency_guard_passed")] bool ConsistencyGuardPassed,
		[property: JsonPropertyName("files")] IReadOnlyList<PgsBackupFile> Files);

	private sealed record PgsBackupFile(
		[property: JsonPropertyName("path")] string Path,
		[property: JsonPropertyName("size")] long Size,
		[property: JsonPropertyName("sha256")] string Sha256);
}

public sealed class PgsForzaHandler : PgsGameSaveHandler
{
	public const string HandlerId = "pgs-forza";

	public PgsForzaHandler() : base(HandlerId)
	{
	}
}
