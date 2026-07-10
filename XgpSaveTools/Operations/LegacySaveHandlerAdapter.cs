using XgpSaveTools.Records;
using XgpSaveTools.SaveHandlers;

namespace XgpSaveTools.Operations;

public sealed class LegacySaveHandlerAdapter : IGameSaveHandler
{
	private readonly ISaveHandler _handler;

	public LegacySaveHandlerAdapter(string id, ISaveHandler handler)
	{
		Id = id;
		_handler = handler;
	}

	public string Id { get; }

	public IReadOnlyList<IGameSaveOperation> GetOperations(GameSaveContext context)
	{
		var operations = new List<IGameSaveOperation>
		{
			new LegacyExportOperation(_handler)
		};

		if (_handler is not IExportOnlySaveHandler)
		{
			operations.Add(new LegacyReplaceOperation(_handler));
			operations.Add(new LegacyDeleteOperation(_handler));
		}

		return operations;
	}

	private sealed class LegacyExportOperation : IGameSaveOperation
	{
		private readonly ISaveHandler _handler;

		public LegacyExportOperation(ISaveHandler handler) => _handler = handler;

		public OperationDefinition Definition { get; } = new(
			"extract",
			"Extract Files",
			"Extract files using the configured game handler.",
			OperationKind.Export);

		public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context) => Array.Empty<OperationParameter>();

		public Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var files = _handler
				.GetSaveEntries(context.Containers.ToList(), context.Game.HandlerArgs)
				.Select(x => new ExportArtifact(x.OutputName, x.ContainerEntry.Path))
				.ToList();

			return Task.FromResult<OperationPlan>(new ExportPlan(files, CreateArchiveName(context)));
		}
	}

	private abstract class LegacyMutationOperation : IGameSaveOperation
	{
		protected const string TargetKey = "target";
		protected readonly ISaveHandler Handler;

		protected LegacyMutationOperation(ISaveHandler handler) => Handler = handler;

		public abstract OperationDefinition Definition { get; }
		public abstract Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken);

		public virtual IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context)
		{
			var choices = GetDirectEntries(context)
				.Select(x => new ChoiceOption(x.DisplayName, x.SelectionKey))
				.ToList();
			if (choices.Count == 0)
				throw new InvalidOperationException("This handler does not expose any directly replaceable WGS entries.");

			return new OperationParameter[]
			{
				new ChoiceParameter(TargetKey, "Select WGS entry", choices)
			};
		}

		protected LegacyMappedEntry GetSelectedEntry(GameSaveContext context, OperationArguments arguments)
		{
			var selection = arguments.GetRequiredString(TargetKey);
			return GetDirectEntries(context).SingleOrDefault(x => x.SelectionKey == selection)
				?? throw new InvalidOperationException("The selected WGS entry is no longer available.");
		}

		private IReadOnlyList<LegacyMappedEntry> GetDirectEntries(GameSaveContext context)
		{
			var byPath = new Dictionary<string, WgsEntryKey>(StringComparer.OrdinalIgnoreCase);
			for (var containerIndex = 0; containerIndex < context.Containers.Count; containerIndex++)
			{
				var container = context.Containers[containerIndex];
				for (var fileIndex = 0; fileIndex < container.Files.Count; fileIndex++)
				{
					var entry = container.Files[fileIndex];
					byPath[Path.GetFullPath(entry.Path)] = new WgsEntryKey(container.Name, entry.Name);
				}
			}

			var result = new List<LegacyMappedEntry>();
			var mapped = Handler.GetSaveEntries(context.Containers.ToList(), context.Game.HandlerArgs).ToList();
			foreach (var save in mapped)
			{
				if (!byPath.TryGetValue(Path.GetFullPath(save.ContainerEntry.Path), out var target)) continue;
				var selectionKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{target.ContainerName}\n{target.FileName}"));
				result.Add(new LegacyMappedEntry(selectionKey, save.OutputName, target, save.ContainerEntry.Path));
			}

			return result;
		}
	}

	private sealed class LegacyReplaceOperation : LegacyMutationOperation
	{
		private const string ReplacementKey = "replacement";

		public LegacyReplaceOperation(ISaveHandler handler) : base(handler) { }

		public override OperationDefinition Definition { get; } = new(
			"replace-entry",
			"Replace Entry",
			"Replace one directly mapped WGS entry.",
			OperationKind.Import);

		public override IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context)
		{
			return base.GetParameters(context)
				.Append(new FileParameter(ReplacementKey, "Replacement file"))
				.ToList();
		}

		public override Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var selected = GetSelectedEntry(context, arguments);
			var replacement = arguments.GetRequiredString(ReplacementKey);
			OperationPlan plan = new ImportPlan(
				new PlannedWgsMutation[] { new PlannedReplacement(selected.Target, replacement) },
				Array.Empty<string>());
			return Task.FromResult(plan);
		}
	}

	private sealed class LegacyDeleteOperation : LegacyMutationOperation
	{
		public LegacyDeleteOperation(ISaveHandler handler) : base(handler) { }

		public override OperationDefinition Definition { get; } = new(
			"delete-entry",
			"Delete Entry",
			"Delete one directly mapped WGS entry.",
			OperationKind.Maintenance);

		public override Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var selected = GetSelectedEntry(context, arguments);
			var deleteFolder = ShouldDeleteContainerFolder(selected.SourcePath);
			OperationPlan plan = new ImportPlan(
				new PlannedWgsMutation[] { new PlannedDeletion(selected.Target, deleteFolder) },
				new[] { "Deleting save data can make a slot unavailable. Keep the generated backup until the game has been verified." });
			return Task.FromResult(plan);
		}

		private static bool ShouldDeleteContainerFolder(string sourcePath)
		{
			var file = new FileInfo(sourcePath);
			var directory = file.Directory;
			if (directory == null) return false;
			var otherFiles = directory.EnumerateFiles()
				.Where(x => !string.Equals(x.FullName, file.FullName, StringComparison.OrdinalIgnoreCase))
				.ToArray();
			return otherFiles.Length == 1
				&& !string.IsNullOrEmpty(otherFiles[0].Extension)
				&& int.TryParse(otherFiles[0].Extension.TrimStart('.'), out _);
		}
	}

	private static string CreateArchiveName(GameSaveContext context)
	{
		var game = new string(context.Game.Name
			.ToLowerInvariant()
			.Select(c => char.IsLetterOrDigit(c) ? c : '_')
			.ToArray())
			.Trim('_');
		return $"{game}_{context.UserContainer.UserTag}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip";
	}

	private sealed record LegacyMappedEntry(
		string SelectionKey,
		string DisplayName,
		WgsEntryKey Target,
		string SourcePath);
}
