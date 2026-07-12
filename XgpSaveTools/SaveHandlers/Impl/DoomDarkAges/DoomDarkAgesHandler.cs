using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using XgpSaveTools.Operations;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveHandlers.Impl.DoomDarkAges;

public abstract class DoomIdTechHandler : IGameSaveHandler
{
	private const string SteamIdKey = "steam-id64";
	private const string SourceDirectoryKey = "source-directory";
	private const string TargetSlotKey = "target-slot";
	private const string SlotFileVersionKey = "slot-file-version";

	private static readonly string[] AutosaveFiles =
	{
		"game.details",
		"game.details-BACKUP",
		"game_duration.dat",
		"game_duration.dat-BACKUP"
	};
	private static readonly byte[] SlotFileMarker = Encoding.ASCII.GetBytes("SlotFile");

	private readonly string _handlerId;
	private readonly string _gameName;
	private readonly string _archiveSlug;
	private readonly int _checksumSidecarLength;
	private readonly uint? _defaultSteamSlotFileVersion;
	private readonly uint? _defaultXgpSlotFileVersion;

	protected DoomIdTechHandler(
		string handlerId,
		string gameName,
		string archiveSlug,
		int checksumSidecarLength,
		uint? defaultSteamSlotFileVersion = null,
		uint? defaultXgpSlotFileVersion = null)
	{
		_handlerId = handlerId;
		_gameName = gameName;
		_archiveSlug = archiveSlug;
		_checksumSidecarLength = checksumSidecarLength;
		_defaultSteamSlotFileVersion = defaultSteamSlotFileVersion;
		_defaultXgpSlotFileVersion = defaultXgpSlotFileVersion;
	}

	public string Id => _handlerId;

	public IReadOnlyList<IGameSaveOperation> GetOperations(GameSaveContext context)
	{
		return new IGameSaveOperation[]
		{
			new ExportToSteamOperation(this),
			new ImportFromSteamOperation(this)
		};
	}

	private sealed class ExportToSteamOperation : IGameSaveOperation
	{
		private readonly DoomIdTechHandler _handler;

		public ExportToSteamOperation(DoomIdTechHandler handler)
		{
			_handler = handler;
		}

		public OperationDefinition Definition { get; } = new(
			"export-to-steam",
			"Export to Steam",
			"Encrypt XGP campaign slots for a Steam account while preserving Steam's native profile.",
			OperationKind.Export);

		public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context)
		{
			var parameters = new List<OperationParameter> { SteamIdParameter("Target SteamID64") };
			if (_handler._defaultSteamSlotFileVersion.HasValue)
			{
				parameters.Add(SlotFileVersionParameter(
					"Target Steam SlotFile version",
					_handler._defaultSteamSlotFileVersion.Value,
					"Enter the target savegame version\n" +
					"*As of the current date, Steam uses version 11. If you get an 'Invalid savegame version' error try bumping this version."));
			}
			return parameters;
		}

		public Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			arguments.Validate(GetParameters(context));
			var steamId = arguments.GetRequiredString(SteamIdKey);
			var slotFileVersion = _handler.GetSlotFileVersion(arguments, _handler._defaultSteamSlotFileVersion);
			var artifacts = new List<ExportArtifact>();
			var autosaves = context.Containers.Where(_handler.IsSaveSlot).ToList();
			if (autosaves.Count == 0) throw new InvalidDataException("No compatible DOOM save-slot containers were found.");

			foreach (var container in autosaves)
			{
				foreach (var fileName in AutosaveFiles)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var entry = RequireEntry(container, fileName);
					var payload = PrepareSlotFilePayload(fileName, File.ReadAllBytes(entry.Path), slotFileVersion);
					var encrypted = IdTechSteamSaveCrypto.Encrypt(payload, fileName, steamId);
					var outputPath = workspace.GetPath(Path.Combine(container.Name, fileName));
					File.WriteAllBytes(outputPath, encrypted);
					artifacts.Add(new ExportArtifact($"{container.Name}/{fileName}", outputPath));
				}
			}

			OperationPlan plan = new ExportPlan(artifacts, _handler.CreateArchiveName(context, "steam"));
			return Task.FromResult(plan);
		}
	}

	private sealed class ImportFromSteamOperation : IGameSaveOperation
	{
		private readonly DoomIdTechHandler _handler;

		public ImportFromSteamOperation(DoomIdTechHandler handler)
		{
			_handler = handler;
		}

		public OperationDefinition Definition { get; } = new(
			"import-from-steam",
			"Import from Steam",
			"Decrypt a Steam campaign save and replace an existing XGP slot while preserving the XGP profile.",
			OperationKind.Import);

		public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context)
		{
			var slots = context.Containers
				.Where(_handler.IsSaveSlot)
				.Select(x => new ChoiceOption(x.Name, x.Name))
				.ToList();
			if (slots.Count == 0) throw new InvalidOperationException("Create an XGP autosave slot before importing.");

			var parameters = new List<OperationParameter>
			{
				new DirectoryParameter(
					SourceDirectoryKey,
					"Steam save-slot directory",
					"Select the directory that directly contains game.details and game_duration.dat."),
				SteamIdParameter("Source SteamID64")
			};
			if (_handler._defaultXgpSlotFileVersion.HasValue)
			{
				parameters.Add(SlotFileVersionParameter(
					"Target XGP SlotFile version",
					_handler._defaultXgpSlotFileVersion.Value,
					"Enter the target savegame version\n" +
					"*As of the current date, the Xbox/Game Pass build uses version 10. If you get an 'Invalid savegame version' error try changing this version."));
			}
			parameters.Add(new ChoiceParameter(TargetSlotKey, "Target existing XGP slot", slots));
			return parameters;
		}

		public Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			arguments.Validate(GetParameters(context));
			var sourceDirectory = arguments.GetRequiredString(SourceDirectoryKey);
			var steamId = arguments.GetRequiredString(SteamIdKey);
			var slotFileVersion = _handler.GetSlotFileVersion(arguments, _handler._defaultXgpSlotFileVersion);
			var targetSlotName = arguments.GetRequiredString(TargetSlotKey);
			var targetContainer = RequireContainer(context, targetSlotName);
			var mutations = new List<PlannedWgsMutation>();
			var decrypted = new Dictionary<string, byte[]>(StringComparer.Ordinal);

			foreach (var fileName in AutosaveFiles)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var sourcePath = Path.Combine(sourceDirectory, fileName);
				if (!File.Exists(sourcePath)) throw new FileNotFoundException($"Required Steam save file not found: {fileName}", sourcePath);
				var plaintext = IdTechSteamSaveCrypto.Decrypt(File.ReadAllBytes(sourcePath), fileName, steamId);
				ValidateAutosavePayload(fileName, plaintext);
				plaintext = PrepareSlotFilePayload(fileName, plaintext, slotFileVersion);
				decrypted[fileName] = plaintext;

				var preparedPath = workspace.GetPath(Path.Combine("import", fileName));
				File.WriteAllBytes(preparedPath, plaintext);
				RequireEntry(targetContainer, fileName);
				mutations.Add(new PlannedReplacement(new WgsEntryKey(targetSlotName, fileName), preparedPath));
			}

			_handler.AddChecksumMutation(
				targetContainer,
				targetSlotName,
				"game_duration.dat.checksum",
				decrypted["game_duration.dat"],
				workspace,
				mutations);
			_handler.AddChecksumMutation(
				targetContainer,
				targetSlotName,
				"game_duration.dat-BACKUP.checksum",
				decrypted["game_duration.dat-BACKUP"],
				workspace,
				mutations);

			OperationPlan plan = new ImportPlan(
				mutations,
				new[]
				{
					$"Close {_handler._gameName} before continuing.",
					"The destination's existing PROFILE/profile.bin will be preserved.",
					"Cloud synchronization may overwrite imported files. Keep the generated WGS backup until the save is verified."
				});
			return Task.FromResult(plan);
		}
	}

	private static TextParameter SteamIdParameter(string label)
	{
		return new TextParameter(
			SteamIdKey,
			label,
			"Enter the SteamID64 of the account that will own the save (can check online with https://steamid.io/)",
			Validator: IdTechSteamSaveCrypto.ValidateSteamId64);
	}

	private static TextParameter SlotFileVersionParameter(string label, uint defaultValue, string description)
	{
		return new TextParameter(
			SlotFileVersionKey,
			label,
			description,
			DefaultValue: defaultValue.ToString(CultureInfo.InvariantCulture),
			Validator: value => uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
				&& version > 0
				? null
				: "SlotFile version must be a positive 32-bit integer.");
	}

	private uint? GetSlotFileVersion(OperationArguments arguments, uint? defaultValue)
	{
		if (!defaultValue.HasValue) return null;
		return uint.Parse(arguments.GetRequiredString(SlotFileVersionKey), CultureInfo.InvariantCulture);
	}

	private static ContainerMetaFile RequireContainer(GameSaveContext context, string name)
	{
		return context.Containers.SingleOrDefault(x => x.Name == name)
			?? throw new InvalidDataException($"Required WGS container not found: {name}");
	}

	private static ContainerEntry RequireEntry(ContainerMetaFile container, string name)
	{
		return container.Files.SingleOrDefault(x => x.Name == name)
			?? throw new InvalidDataException($"Required WGS entry not found: {container.Name}/{name}");
	}

	private void AddChecksumMutation(
		ContainerMetaFile targetContainer,
		string targetSlotName,
		string checksumName,
		byte[] payload,
		ITempWorkspace workspace,
		ICollection<PlannedWgsMutation> mutations)
	{
		RequireEntry(targetContainer, checksumName);
		var preparedPath = workspace.GetPath(Path.Combine("import", checksumName));
		File.WriteAllBytes(preparedPath, IdTechBlockChecksum.CreateSidecar(payload, _checksumSidecarLength));
		mutations.Add(new PlannedReplacement(new WgsEntryKey(targetSlotName, checksumName), preparedPath));
	}

	private static byte[] PrepareSlotFilePayload(string fileName, byte[] payload, uint? targetVersion)
	{
		if (!targetVersion.HasValue || !fileName.StartsWith("game_duration.dat", StringComparison.Ordinal))
			return payload;

		if (payload.Length < 16
			|| BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4, 4)) != 8
			|| !payload.AsSpan(8, 8).SequenceEqual(SlotFileMarker))
		{
			throw new InvalidDataException($"'{fileName}' does not contain the expected SlotFile header.");
		}

		var currentVersion = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
		if (currentVersion == targetVersion.Value)
			return payload;

		var patched = (byte[])payload.Clone();
		BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(0, 4), targetVersion.Value);
		return patched;
	}

	private static void ValidateAutosavePayload(string fileName, byte[] data)
	{
		if (fileName.StartsWith("game.details", StringComparison.Ordinal))
		{
			var text = Encoding.UTF8.GetString(data);
			if (!text.Contains("checksum=", StringComparison.Ordinal) || !text.Contains("slotId=", StringComparison.Ordinal))
				throw new InvalidDataException($"Decrypted '{fileName}' does not look like a DOOM details file.");
			return;
		}

		var headerLength = Math.Min(data.Length, 64);
		var header = Encoding.ASCII.GetString(data, 0, headerLength);
		if (!header.Contains("SlotFile", StringComparison.Ordinal))
			throw new InvalidDataException($"Decrypted '{fileName}' does not contain the expected SlotFile header.");
	}

	private bool IsSaveSlot(ContainerMetaFile container)
	{
		return AutosaveFiles.All(fileName =>
			container.Files.Any(entry => entry.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)));
	}

	private string CreateArchiveName(GameSaveContext context, string suffix)
	{
		return $"{_archiveSlug}_{context.UserContainer.UserTag}_{suffix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip";
	}
}

public sealed class DoomDarkAgesHandler : DoomIdTechHandler
{
	public const string HandlerId = "doom-dark-ages";

	public DoomDarkAgesHandler()
		: base(
			HandlerId,
			"DOOM: The Dark Ages",
			"doom_the_dark_ages",
			checksumSidecarLength: sizeof(ulong),
			defaultSteamSlotFileVersion: 11,
			defaultXgpSlotFileVersion: 10)
	{
	}
}
