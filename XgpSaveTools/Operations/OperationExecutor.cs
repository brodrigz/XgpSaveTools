using System.IO.Compression;
using XgpSaveTools.Extensions;
using XgpSaveTools.Records;

namespace XgpSaveTools.Operations;

public sealed record OperationExecutionResult(
	int AffectedFiles,
	string? OutputPath = null,
	string? BackupPath = null);

public sealed class OperationExecutor
{
	private readonly XboxContainerRepository _repository;
	private readonly string _backupRoot;
	private readonly Action<int>? _beforeCommit;

	public OperationExecutor(XboxContainerRepository repository, string? backupRoot = null)
		: this(repository, backupRoot, null)
	{
	}

	internal OperationExecutor(
		XboxContainerRepository repository,
		string? backupRoot,
		Action<int>? beforeCommit)
	{
		_repository = repository;
		_backupRoot = backupRoot ?? IoExtensions.BackupOutput;
		_beforeCommit = beforeCommit;
	}

	public OperationExecutionResult ExecuteExport(ExportPlan plan, string? outputDirectory = null)
	{
		if (plan.Files.Count == 0) throw new InvalidOperationException("The export plan contains no files.");
		outputDirectory ??= Directory.GetCurrentDirectory();
		Directory.CreateDirectory(outputDirectory);

		var archiveName = Path.GetFileName(plan.SuggestedArchiveName);
		if (string.IsNullOrWhiteSpace(archiveName)) throw new InvalidOperationException("Invalid archive name.");
		if (!archiveName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) archiveName += ".zip";
		var archivePath = Path.Combine(outputDirectory, archiveName);
		if (File.Exists(archivePath)) throw new IOException($"Output archive already exists: {archivePath}");

		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var artifact in plan.Files)
		{
			ValidateExportName(artifact.OutputName);
			if (!names.Add(artifact.OutputName))
				throw new InvalidOperationException($"Duplicate export path: {artifact.OutputName}");
			if (!File.Exists(artifact.PreparedFile))
				throw new FileNotFoundException("Prepared export file was not found.", artifact.PreparedFile);
		}

		try
		{
			using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create);
			foreach (var artifact in plan.Files)
			{
				var entryName = artifact.OutputName.Replace('\\', '/');
				zip.CreateEntryFromFile(artifact.PreparedFile, entryName, CompressionLevel.Optimal);
			}
		}
		catch
		{
			if (File.Exists(archivePath)) File.Delete(archivePath);
			throw;
		}

		return new OperationExecutionResult(plan.Files.Count, archivePath);
	}

	public OperationExecutionResult ExecuteImport(GameSaveContext context, ImportPlan plan)
	{
		if (plan.Mutations.Count == 0) throw new InvalidOperationException("The import plan contains no mutations.");
		if (plan.Mutations.Count > 1 && plan.Mutations.OfType<PlannedDeletion>().Any(x => x.DeleteContainerFolder))
			throw new InvalidOperationException("Deleting an entire WGS container folder must be executed as a standalone operation.");

		var (currentStorePackage, currentContainers) = _repository.ReadUserContainers(context.UserContainer.Dir);
		if (!string.Equals(currentStorePackage, context.StorePackage, StringComparison.Ordinal))
			throw new InvalidOperationException("The selected WGS store package changed while preparing the operation.");
		var resolved = ResolveTargets(context.UserContainer, currentContainers, plan.Mutations);
		ValidatePreparedFiles(resolved);

		var backupPath = CreateBackup(context);
		var transactionId = Guid.NewGuid().ToString("N");
		var staged = new List<StagedMutation>();
		var committed = new List<StagedMutation>();

		try
		{
			foreach (var item in resolved)
			{
				if (item.Mutation is PlannedDeletion { DeleteContainerFolder: true })
				{
					var targetDirectory = Path.GetDirectoryName(item.TargetPath)
						?? throw new InvalidOperationException("Could not resolve the WGS container directory.");
					var rollbackDirectory = targetDirectory + $".xgpst-{transactionId}.rollback";
					if (Directory.Exists(rollbackDirectory))
						throw new IOException($"Transaction directory already exists: {rollbackDirectory}");
					staged.Add(new StagedMutation(item.Mutation, item.TargetPath, null, null, rollbackDirectory));
					continue;
				}

				var rollbackPath = item.TargetPath + $".xgpst-{transactionId}.rollback";
				File.Copy(item.TargetPath, rollbackPath, overwrite: false);

				string? stagedPath = null;
				if (item.Mutation is PlannedReplacement replacement)
				{
					stagedPath = item.TargetPath + $".xgpst-{transactionId}.stage";
					staged.Add(new StagedMutation(item.Mutation, item.TargetPath, stagedPath, rollbackPath, null));
					File.Copy(replacement.PreparedFile, stagedPath, overwrite: false);
				}
				else
				{
					staged.Add(new StagedMutation(item.Mutation, item.TargetPath, null, rollbackPath, null));
				}
			}

			for (var commitIndex = 0; commitIndex < staged.Count; commitIndex++)
			{
				var item = staged[commitIndex];
				_beforeCommit?.Invoke(commitIndex);
				switch (item.Mutation)
				{
					case PlannedReplacement:
						File.Move(item.StagedPath!, item.TargetPath, overwrite: true);
						break;
					case PlannedDeletion { DeleteContainerFolder: true }:
						var targetDirectory = Path.GetDirectoryName(item.TargetPath)!;
						Directory.Move(targetDirectory, item.RollbackDirectory!);
						break;
					case PlannedDeletion:
						File.Delete(item.TargetPath);
						break;
					default:
						throw new NotSupportedException($"Unsupported mutation: {item.Mutation.GetType().Name}");
				}
				committed.Add(item);
			}
		}
		catch (Exception commitError)
		{
			var rollbackErrors = new List<Exception>();
			foreach (var item in committed.AsEnumerable().Reverse())
			{
				try
				{
					if (item.RollbackDirectory != null && Directory.Exists(item.RollbackDirectory))
					{
						var targetDirectory = Path.GetDirectoryName(item.TargetPath)!;
						if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, true);
						Directory.Move(item.RollbackDirectory, targetDirectory);
					}
					else if (item.RollbackPath != null && File.Exists(item.RollbackPath))
					{
						File.Copy(item.RollbackPath, item.TargetPath, overwrite: true);
					}
				}
				catch (Exception rollbackError)
				{
					rollbackErrors.Add(rollbackError);
				}
			}

			if (rollbackErrors.Count > 0)
				throw new AggregateException("Import failed and one or more files could not be rolled back. Restore the WGS backup manually.", new[] { commitError }.Concat(rollbackErrors));

			throw new InvalidOperationException("Import failed. Original WGS files were restored.", commitError);
		}
		finally
		{
			foreach (var item in staged)
			{
				TryDelete(item.StagedPath);
				TryDelete(item.RollbackPath);
				TryDeleteDirectory(item.RollbackDirectory);
			}
		}

		return new OperationExecutionResult(plan.Mutations.Count, BackupPath: backupPath);
	}

	private static List<ResolvedMutation> ResolveTargets(
		UserContainerFolder userContainer,
		IReadOnlyList<ContainerMetaFile> containers,
		IReadOnlyList<PlannedWgsMutation> mutations)
	{
		var root = Path.GetFullPath(userContainer.Dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var seen = new HashSet<WgsEntryKey>();
		var result = new List<ResolvedMutation>();

		foreach (var mutation in mutations)
		{
			if (!seen.Add(mutation.Target))
				throw new InvalidOperationException($"Duplicate WGS target: {mutation.Target.ContainerName}/{mutation.Target.FileName}");

			var container = containers.SingleOrDefault(x => x.Name == mutation.Target.ContainerName)
				?? throw new InvalidOperationException($"WGS container not found: {mutation.Target.ContainerName}");
			var entry = container.Files.SingleOrDefault(x => x.Name == mutation.Target.FileName)
				?? throw new InvalidOperationException($"WGS entry not found: {mutation.Target.ContainerName}/{mutation.Target.FileName}");
			var targetPath = Path.GetFullPath(entry.Path);
			if (!targetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("Resolved WGS target is outside the selected user container.");
			if (!File.Exists(targetPath)) throw new FileNotFoundException("Resolved WGS target is missing.", targetPath);

			result.Add(new ResolvedMutation(mutation, targetPath));
		}

		return result;
	}

	private static void ValidatePreparedFiles(IEnumerable<ResolvedMutation> mutations)
	{
		foreach (var item in mutations)
		{
			if (item.Mutation is PlannedReplacement replacement && !File.Exists(replacement.PreparedFile))
				throw new FileNotFoundException("Prepared replacement file was not found.", replacement.PreparedFile);
		}
	}

	private static void ValidateExportName(string name)
	{
		if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name))
			throw new InvalidOperationException($"Invalid export path: {name}");
		var parts = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0 || parts.Any(x => x == ".." || x == "."))
			throw new InvalidOperationException($"Invalid export path: {name}");
	}

	private string CreateBackup(GameSaveContext context)
	{
		var gameName = string.Concat(context.Game.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
		var destination = Path.Combine(
			_backupRoot,
			DateTime.Now.ToString("yyyy.MM.dd"),
			gameName,
			context.UserContainer.UserTag,
			$"{DateTime.Now:HH-mm-ss-fff}-{Guid.NewGuid():N}");
		return IoExtensions.CopyDirectory(context.UserContainer.Dir, destination);
	}

	private static void TryDelete(string? path)
	{
		if (path == null) return;
		try
		{
			if (File.Exists(path)) File.Delete(path);
		}
		catch
		{
			// The original data and full backup are more important than transaction-file cleanup.
		}
	}

	private static void TryDeleteDirectory(string? path)
	{
		if (path == null) return;
		try
		{
			if (Directory.Exists(path)) Directory.Delete(path, true);
		}
		catch
		{
			// The full WGS backup remains available if transaction-directory cleanup fails.
		}
	}

	private sealed record ResolvedMutation(PlannedWgsMutation Mutation, string TargetPath);
	private sealed record StagedMutation(
		PlannedWgsMutation Mutation,
		string TargetPath,
		string? StagedPath,
		string? RollbackPath,
		string? RollbackDirectory);
}
