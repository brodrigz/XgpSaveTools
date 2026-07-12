using System.Text;
using XgpSaveTools.Operations;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers;

public sealed record MappedSaveEntry(string OutputName, WgsEntryKey Target, string SourcePath)
{
	public static MappedSaveEntry Create(string outputName, ContainerMetaFile container, ContainerEntry entry) =>
		new(outputName, new WgsEntryKey(container.Name, entry.Name), entry.Path);
}

public sealed class StandardGameSaveHandler : IGameSaveHandler
{
	private readonly Func<GameSaveContext, IEnumerable<MappedSaveEntry>> _mapEntries;

	public StandardGameSaveHandler(
		string id,
		Func<GameSaveContext, IEnumerable<MappedSaveEntry>> mapEntries)
	{
		Id = id;
		_mapEntries = mapEntries;
	}

	public string Id { get; }

	public IReadOnlyList<IGameSaveOperation> GetOperations(GameSaveContext context) =>
		new IGameSaveOperation[]
		{
			new ExportOperation(this),
			new ReplaceOperation(this),
			new DeleteOperation(this)
		};

	private IReadOnlyList<MappedSaveEntry> MapEntries(GameSaveContext context) =>
		_mapEntries(context).ToList();

	private sealed class ExportOperation : IGameSaveOperation
	{
		private readonly StandardGameSaveHandler _handler;

		public ExportOperation(StandardGameSaveHandler handler) => _handler = handler;

		public OperationDefinition Definition { get; } = new(
			"extract", "Extract Files", "Extract directly mapped WGS entries.", OperationKind.Export);

		public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context) =>
			Array.Empty<OperationParameter>();

		public Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var files = _handler.MapEntries(context)
				.Select(x => new ExportArtifact(x.OutputName, x.SourcePath))
				.ToList();
			return Task.FromResult<OperationPlan>(new ExportPlan(files, ArchiveName.Create(context)));
		}
	}

	private abstract class MutationOperation : IGameSaveOperation
	{
		protected const string TargetKey = "target";
		protected readonly StandardGameSaveHandler Handler;

		protected MutationOperation(StandardGameSaveHandler handler) => Handler = handler;

		public abstract OperationDefinition Definition { get; }

		public virtual IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context)
		{
			var choices = Handler.MapEntries(context)
				.Select(x => new ChoiceOption(x.OutputName, SelectionKey(x.Target)))
				.ToList();
			if (choices.Count == 0)
				throw new InvalidOperationException("This handler does not expose any replaceable WGS entries.");
			return new OperationParameter[] { new ChoiceParameter(TargetKey, "Select WGS entry", choices) };
		}

		public abstract Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken);

		protected MappedSaveEntry GetSelectedEntry(GameSaveContext context, OperationArguments arguments)
		{
			var selected = arguments.GetRequiredString(TargetKey);
			return Handler.MapEntries(context).SingleOrDefault(x => SelectionKey(x.Target) == selected)
				?? throw new InvalidOperationException("The selected WGS entry is no longer available.");
		}

		private static string SelectionKey(WgsEntryKey target) =>
			Convert.ToBase64String(Encoding.UTF8.GetBytes($"{target.ContainerName}\n{target.FileName}"));
	}

	private sealed class ReplaceOperation : MutationOperation
	{
		private const string ReplacementKey = "replacement";

		public ReplaceOperation(StandardGameSaveHandler handler) : base(handler) { }

		public override OperationDefinition Definition { get; } = new(
			"replace-entry", "Replace Entry", "Replace one directly mapped WGS entry.", OperationKind.Import);

		public override IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context) =>
			base.GetParameters(context)
				.Append(new FileParameter(ReplacementKey, "Replacement file"))
				.ToList();

		public override Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			arguments.Validate(GetParameters(context));
			var selected = GetSelectedEntry(context, arguments);
			OperationPlan plan = new ImportPlan(
				new PlannedWgsMutation[]
				{
					new PlannedReplacement(selected.Target, arguments.GetRequiredString(ReplacementKey))
				},
				Array.Empty<string>());
			return Task.FromResult(plan);
		}
	}

	private sealed class DeleteOperation : MutationOperation
	{
		public DeleteOperation(StandardGameSaveHandler handler) : base(handler) { }

		public override OperationDefinition Definition { get; } = new(
			"delete-entry", "Delete Entry", "Delete one directly mapped WGS entry.", OperationKind.Maintenance);

		public override Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			arguments.Validate(GetParameters(context));
			var selected = GetSelectedEntry(context, arguments);
			OperationPlan plan = new ImportPlan(
				new PlannedWgsMutation[]
				{
					new PlannedDeletion(selected.Target, ShouldDeleteContainerFolder(selected.SourcePath))
				},
				new[] { "Deleting save data can make a slot unavailable. Keep the backup until the game has been verified." });
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
}

internal static class ArchiveName
{
	public static string Create(GameSaveContext context)
	{
		var game = new string(context.Game.Name
			.ToLowerInvariant()
			.Select(c => char.IsLetterOrDigit(c) ? c : '_')
			.ToArray())
			.Trim('_');
		return $"{game}_{context.UserContainer.UserTag}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip";
	}
}
