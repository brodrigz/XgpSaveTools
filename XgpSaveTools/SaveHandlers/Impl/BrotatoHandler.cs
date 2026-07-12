using System.IO.Compression;
using System.Text;
using XgpSaveTools.Operations;

namespace XgpSaveTools.SaveHandlers.Impl;

public sealed class BrotatoHandler : IGameSaveHandler
{
	public const string HandlerId = "brotato";
	private const string SteamIdKey = "steam-id";
	private static readonly byte[] CompressedMagic = { 0xFB, 0x04, 0xFE, 0x05 };

	public string Id => HandlerId;

	public IReadOnlyList<IGameSaveOperation> GetOperations(GameSaveContext context) =>
		new IGameSaveOperation[] { new ExportToSteamOperation() };

	private sealed class ExportToSteamOperation : IGameSaveOperation
	{
		public OperationDefinition Definition { get; } = new(
			"export-steam", "Export for Steam",
			"Unpack the Xbox file bundle and decompress Brotato's Steam JSON files.",
			OperationKind.Export);

		public IReadOnlyList<OperationParameter> GetParameters(GameSaveContext context) =>
			new OperationParameter[]
			{
				new TextParameter(
					SteamIdKey,
					"SteamID64",
					"Files are placed in a folder named with this Steam account ID.",
					Validator: ValidateSteamId)
			};

		public Task<OperationPlan> PrepareAsync(
			GameSaveContext context,
			OperationArguments arguments,
			ITempWorkspace workspace,
			CancellationToken cancellationToken)
		{
			var parameters = GetParameters(context);
			arguments.Validate(parameters);
			var steamId = arguments.GetRequiredString(SteamIdKey);
			var source = RequireBundle(context);
			var bundle = BrotatoBundle.Read(source.Path);
			var artifacts = new List<ExportArtifact>();
			var outputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var file in bundle.Files.Where(x =>
				x.Data.Length > 0 && x.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
			{
				cancellationToken.ThrowIfCancellationRequested();
				var fileName = Path.GetFileName(file.Name.Replace('\\', '/'));
				if (string.IsNullOrWhiteSpace(fileName) || !outputNames.Add(fileName))
					throw new InvalidDataException($"Duplicate or invalid Brotato output file '{file.Name}'.");
				var data = DecompressIfNeeded(file.Data, file.Name);
				var relative = Path.Combine(steamId, fileName);
				var destination = workspace.GetPath(relative);
				File.WriteAllBytes(destination, data);
				artifacts.Add(new ExportArtifact(relative.Replace('\\', '/'), destination));
			}

			if (artifacts.Count == 0)
				throw new InvalidDataException("The Brotato Xbox bundle contains no non-empty JSON save files.");

			OperationPlan plan = new ExportPlan(
				artifacts,
				$"brotato_{steamId}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip",
				new[]
				{
					$"Copy the '{steamId}' folder into %APPDATA%\\Brotato after closing the game and disabling Steam Cloud temporarily.",
					"This operation is export-only; the original Xbox bundle is not modified."
				});
			return Task.FromResult(plan);
		}
	}

	private static string? ValidateSteamId(string value) =>
		value.Length is >= 16 and <= 20 && ulong.TryParse(value, out var id) && id > 0
			? null
			: "SteamID64 must be a 16-20 digit positive number.";

	private static Records.ContainerEntry RequireBundle(GameSaveContext context)
	{
		var files = context.Containers.SelectMany(x => x.Files).ToList();
		if (files.Count != 1)
			throw new InvalidDataException($"Expected one Brotato WGS bundle, found {files.Count} files.");
		return files[0];
	}

	private static byte[] DecompressIfNeeded(byte[] data, string name)
	{
		if (data.Length < 8 || !data.AsSpan(0, 4).SequenceEqual(CompressedMagic)) return data;
		var expectedLength = BitConverter.ToInt32(data, 4);
		if (expectedLength < 0 || expectedLength > 128 * 1024 * 1024)
			throw new InvalidDataException($"Invalid decompressed size in Brotato file '{name}'.");
		using var input = new MemoryStream(data, 8, data.Length - 8, false);
		using var zlib = new ZLibStream(input, CompressionMode.Decompress);
		using var output = new MemoryStream(expectedLength);
		zlib.CopyTo(output);
		if (output.Length != expectedLength)
			throw new InvalidDataException(
				$"Brotato file '{name}' decompressed to {output.Length} bytes; expected {expectedLength}.");
		return output.ToArray();
	}

	private sealed record BrotatoBundleFile(string Name, byte[] Data);

	private sealed record BrotatoBundle(IReadOnlyList<BrotatoBundleFile> Files)
	{
		public static BrotatoBundle Read(string path)
		{
			using var stream = File.OpenRead(path);
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);
			if (stream.Length < 32) throw new InvalidDataException("Brotato bundle is too short.");
			var usedLength = reader.ReadUInt32();
			var version = reader.ReadUInt32();
			if (usedLength > stream.Length || usedLength < 32)
				throw new InvalidDataException("Brotato bundle has an invalid used length.");
			if (version != 1) throw new InvalidDataException($"Unsupported Brotato bundle version {version}.");
			stream.Position = 28;
			var fileCount = ReadCount(reader, "file");
			var files = new List<BrotatoBundleFile>(fileCount);
			for (var index = 0; index < fileCount; index++)
			{
				var name = ReadString(reader, usedLength, "file name");
				ValidateBundlePath(name);
				var length = reader.ReadUInt32();
				if (length > usedLength - stream.Position)
					throw new InvalidDataException($"Brotato file '{name}' exceeds the bundle boundary.");
				files.Add(new BrotatoBundleFile(name, reader.ReadBytes((int)length)));
			}

			var directoryCount = ReadCount(reader, "directory");
			for (var index = 0; index < directoryCount; index++)
				ValidateBundlePath(ReadString(reader, usedLength, "directory name"));
			if (stream.Position != usedLength)
				throw new InvalidDataException(
					$"Brotato bundle ended at {stream.Position}; expected {usedLength}.");
			return new BrotatoBundle(files);
		}

		private static int ReadCount(BinaryReader reader, string label)
		{
			var count = reader.ReadInt32();
			if (count < 0 || count > 10_000)
				throw new InvalidDataException($"Invalid Brotato {label} count {count}.");
			return count;
		}

		private static string ReadString(BinaryReader reader, uint usedLength, string label)
		{
			var length = reader.ReadUInt32();
			if (length > 4096 || length > usedLength - reader.BaseStream.Position)
				throw new InvalidDataException($"Invalid Brotato {label} length {length}.");
			var bytes = reader.ReadBytes((int)length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return new UTF8Encoding(false, true).GetString(bytes);
		}

		private static void ValidateBundlePath(string value)
		{
			var normalized = value.Replace('\\', '/');
			while (normalized.StartsWith("./", StringComparison.Ordinal)) normalized = normalized[2..];
			if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized) ||
				normalized.StartsWith("/", StringComparison.Ordinal) ||
				normalized.Split('/').Any(x => x is "." or ".."))
				throw new InvalidDataException($"Invalid Brotato bundle path '{value}'.");
		}
	}
}
