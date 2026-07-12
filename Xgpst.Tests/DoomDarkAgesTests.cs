using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using XgpSaveTools.Operations;
using XgpSaveTools.Records;
using XgpSaveTools.SaveHandlers.Impl.DoomDarkAges;

namespace Xgpst.Tests;

public sealed class DoomDarkAgesTests
{
	private const string SteamId = "76561197960265729";
	private static readonly byte[] Details = Encoding.UTF8.GetBytes(
		"checksum=123\ncompleted=0\ngameVersion=1145896964\nslotId=00000000000000000000000000000000\n");
	private static readonly byte[] Duration =
	{
		10, 0, 0, 0,
		8, 0, 0, 0,
		(byte)'S', (byte)'l', (byte)'o', (byte)'t', (byte)'F', (byte)'i', (byte)'l', (byte)'e',
		0, (byte)'s', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e'
	};

	[Fact]
	public void Decrypt_MatchesPublishedIdSaveDataResignerVector()
	{
		var encrypted = Convert.FromBase64String(
			"YQShxawxtnTP9AY7UeJ9vqSOjgR8e+BBeqI66G7ItWfpo/KGXoeWgwBndHDJIIjmp/duRFra//IsUeDvLjUdH2dlwSMCKS78/eSSh8+16395IrtpnDQoE7++PPQBAaGGcGwbjft3sZ9QbfBnrG41UBvyrqAHxI9xMVvgNC6DCj0wY6A7Eh4GAHw1X7fIKi8VwYSkYKKVyyvjgiTdBzdvz85+kqLGypwaeco7rKiNdbD5nGeSvSYrwC54ZZjHzPy0jofPmoU8iiHWJ81d+/D++zYdEV5/gf2RYj1NFfxgKNErNEvs9j1qYsytzj/fWUKzk4rKwV7XsNFc1QSO1kaW7NpAhH7DSRsEARX1E8sE0mut6WHfb2SW");
		var expected = Convert.FromBase64String(
			"Y2hlY2tzdW09NDc5NTIxMTg3CmNvbXBsZXRlZD0wCmRhdGVDcmVhdGVkPTAKZGllZExhc3RHYW1lPTAKZGlmZmljdWx0eT0xCmV4dHJhTGlmZURyb3BMZXZlbD0yCmV4dHJhTGlmZU1vZGU9MApnYW1lVmVyc2lvbj0xMTQ1ODk2OTY0Cm1hcERlc2M9QmF6YSDFgm93Y8OzdyB6YWfFgmFkeQptYXBOYW1lPWdhbWUvc3AvZTFtNF9ib3NzL2UxbTRfYm9zcwpzbG90SWQ9MDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAKdGltZT0xODE3MQo=");

		var actual = IdTechSteamSaveCrypto.Decrypt(encrypted, "game.details", SteamId);

		Assert.Equal(expected, actual);
	}

	[Fact]
	public void EncryptDecrypt_RoundTripsAndAddsExpectedOverhead()
	{
		var encrypted = IdTechSteamSaveCrypto.Encrypt(Duration, "game_duration.dat", SteamId);
		var decrypted = IdTechSteamSaveCrypto.Decrypt(encrypted, "game_duration.dat", SteamId);

		Assert.Equal(Duration.Length + IdTechSteamSaveCrypto.EncryptionOverhead, encrypted.Length);
		Assert.Equal(Duration, decrypted);
		Assert.Throws<CryptographicException>(() =>
			IdTechSteamSaveCrypto.Decrypt(encrypted, "game_duration.dat", "76561197960265730"));
	}

	[Fact]
	public void BlockChecksum_MatchesKnownIdTechValue()
	{
		var data = Encoding.ASCII.GetBytes("abc");

		Assert.Equal(0x275FA452u, IdTechBlockChecksum.Compute(data));
		Assert.Equal(new byte[] { 0x52, 0xA4, 0x5F, 0x27 }, IdTechBlockChecksum.CreateSidecar(data, sizeof(uint)));
		Assert.Equal(new byte[] { 0x52, 0xA4, 0x5F, 0x27, 0, 0, 0, 0 }, IdTechBlockChecksum.CreateSidecar(data));
		Assert.Throws<ArgumentOutOfRangeException>(() => IdTechBlockChecksum.CreateSidecar(data, 6));
	}

	[Fact]
	public async Task DarkAgesExport_UpgradesSlotFileVersionAndEncryptsPayloads()
	{
		using var fixture = new DoomFixture();
		var handler = new DoomDarkAgesHandler();
		var operation = handler.GetOperations(fixture.Context).Single(x => x.Definition.Id == "export-to-steam");
		var arguments = new OperationArguments(new Dictionary<string, object?>
		{
			["steam-id64"] = SteamId,
			["slot-file-version"] = "11"
		});
		using var workspace = new TempWorkspace();

		var plan = Assert.IsType<ExportPlan>(await operation.PrepareAsync(
			fixture.Context, arguments, workspace, CancellationToken.None));

		Assert.Equal(4, plan.Files.Count);
		var versionParameter = Assert.IsType<TextParameter>(
			operation.GetParameters(fixture.Context).Single(x => x.Key == "slot-file-version"));
		Assert.Equal("11", versionParameter.DefaultValue);
		Assert.Contains("Steam uses version 11", versionParameter.Description);
		Assert.DoesNotContain(operation.GetParameters(fixture.Context), x => x.Key == "include-profile");
		Assert.DoesNotContain(plan.Files, x => x.OutputName.StartsWith("PROFILE/", StringComparison.Ordinal));
		Assert.DoesNotContain(plan.Files, x => x.OutputName.EndsWith(".checksum", StringComparison.Ordinal));
		foreach (var artifact in plan.Files)
		{
			var fileName = Path.GetFileName(artifact.OutputName);
			var expected = fileName.StartsWith("game.details", StringComparison.Ordinal)
				? Details
				: WithSlotFileVersion(Duration, 11);
			var decrypted = IdTechSteamSaveCrypto.Decrypt(File.ReadAllBytes(artifact.PreparedFile), fileName, SteamId);
			Assert.Equal(expected, decrypted);
			if (!fileName.StartsWith("game.details", StringComparison.Ordinal))
				Assert.Equal(Duration.AsSpan(4).ToArray(), decrypted.AsSpan(4).ToArray());
		}
	}

	[Fact]
	public async Task ImportFromSteam_PreparesFourPayloadsAndTwoChecksums()
	{
		using var fixture = new DoomFixture();
		var steamDirectory = fixture.CreateDirectory("steam/GAME-AUTOSAVE1");
		var steamDuration = WithSlotFileVersion(Duration, 11);
		foreach (var fileName in DoomFixture.AutosaveFileNames)
		{
			var data = fileName.StartsWith("game.details", StringComparison.Ordinal) ? Details : steamDuration;
			File.WriteAllBytes(
				Path.Combine(steamDirectory, fileName),
				IdTechSteamSaveCrypto.Encrypt(data, fileName, SteamId));
		}

		var handler = new DoomDarkAgesHandler();
		var operation = handler.GetOperations(fixture.Context).Single(x => x.Definition.Id == "import-from-steam");
		var arguments = new OperationArguments(new Dictionary<string, object?>
		{
			["source-directory"] = steamDirectory,
			["steam-id64"] = SteamId,
			["slot-file-version"] = "10",
			["target-slot"] = "GAME-AUTOSAVE1"
		});
		using var workspace = new TempWorkspace();

		var plan = Assert.IsType<ImportPlan>(await operation.PrepareAsync(
			fixture.Context, arguments, workspace, CancellationToken.None));

		Assert.Equal(6, plan.Mutations.Count);
		var versionParameter = Assert.IsType<TextParameter>(
			operation.GetParameters(fixture.Context).Single(x => x.Key == "slot-file-version"));
		Assert.Equal("10", versionParameter.DefaultValue);
		Assert.Contains("Xbox/Game Pass build uses version 10", versionParameter.Description);
		Assert.DoesNotContain(operation.GetParameters(fixture.Context), x => x.Key == "include-profile");
		Assert.DoesNotContain(plan.Mutations, x => x.Target.ContainerName == "PROFILE");
		Assert.All(plan.Mutations, x => Assert.Equal("GAME-AUTOSAVE1", x.Target.ContainerName));
		var currentChecksum = Assert.IsType<PlannedReplacement>(
			plan.Mutations.Single(x => x.Target.FileName == "game_duration.dat.checksum"));
		Assert.Equal(IdTechBlockChecksum.CreateSidecar(Duration), File.ReadAllBytes(currentChecksum.PreparedFile));
		var currentDuration = Assert.IsType<PlannedReplacement>(
			plan.Mutations.Single(x => x.Target.FileName == "game_duration.dat"));
		Assert.Equal(Duration, File.ReadAllBytes(currentDuration.PreparedFile));
	}

	[Fact]
	public async Task DarkAgesExport_UsesSelectedSlotFileVersion()
	{
		using var fixture = new DoomFixture();
		var version11 = WithSlotFileVersion(Duration, 11);
		foreach (var entry in fixture.Context.Containers.SelectMany(x => x.Files)
			.Where(x => x.Name.StartsWith("game_duration.dat", StringComparison.Ordinal)))
		{
			File.WriteAllBytes(entry.Path, version11);
		}

		var handler = new DoomDarkAgesHandler();
		var operation = handler.GetOperations(fixture.Context).Single(x => x.Definition.Id == "export-to-steam");
		using var workspace = new TempWorkspace();
		var plan = Assert.IsType<ExportPlan>(await operation.PrepareAsync(
			fixture.Context,
			new OperationArguments(new Dictionary<string, object?>
			{
				["steam-id64"] = SteamId,
				["slot-file-version"] = "12"
			}),
			workspace,
			CancellationToken.None));

		foreach (var artifact in plan.Files.Where(x =>
			Path.GetFileName(x.OutputName).StartsWith("game_duration.dat", StringComparison.Ordinal)))
		{
			var fileName = Path.GetFileName(artifact.OutputName);
			var decrypted = IdTechSteamSaveCrypto.Decrypt(File.ReadAllBytes(artifact.PreparedFile), fileName, SteamId);
			Assert.Equal(WithSlotFileVersion(version11, 12), decrypted);
			Assert.Equal(version11.AsSpan(4).ToArray(), decrypted.AsSpan(4).ToArray());
		}
	}

	[Fact]
	public async Task DoomEternal_UsesFourByteChecksumsForDlcSlots()
	{
		const string slotName = "DLC1-AUTOSAVE3";
		using var fixture = new DoomFixture(slotName, doomEternal: true);
		var steamDirectory = fixture.CreateDirectory($"steam/{slotName}");
		foreach (var fileName in DoomFixture.AutosaveFileNames)
		{
			var data = fileName.StartsWith("game.details", StringComparison.Ordinal) ? Details : Duration;
			File.WriteAllBytes(
				Path.Combine(steamDirectory, fileName),
				IdTechSteamSaveCrypto.Encrypt(data, fileName, SteamId));
		}

		var handler = new DoomEternalHandler();
		var exportOperation = handler.GetOperations(fixture.Context).Single(x => x.Definition.Id == "export-to-steam");
		using var exportWorkspace = new TempWorkspace();
		var exportPlan = Assert.IsType<ExportPlan>(await exportOperation.PrepareAsync(
			fixture.Context,
			new OperationArguments(new Dictionary<string, object?>
			{
				["steam-id64"] = SteamId
			}),
			exportWorkspace,
			CancellationToken.None));
		Assert.Equal(4, exportPlan.Files.Count);
		Assert.DoesNotContain(exportPlan.Files, x => x.OutputName.StartsWith("PROFILE/", StringComparison.Ordinal));
		Assert.All(exportPlan.Files, x => Assert.StartsWith($"{slotName}/", x.OutputName));
		foreach (var artifact in exportPlan.Files.Where(x =>
			Path.GetFileName(x.OutputName).StartsWith("game_duration.dat", StringComparison.Ordinal)))
		{
			var fileName = Path.GetFileName(artifact.OutputName);
			var decrypted = IdTechSteamSaveCrypto.Decrypt(File.ReadAllBytes(artifact.PreparedFile), fileName, SteamId);
			Assert.Equal(Duration, decrypted);
		}

		var importOperation = handler.GetOperations(fixture.Context).Single(x => x.Definition.Id == "import-from-steam");
		using var importWorkspace = new TempWorkspace();
		var importPlan = Assert.IsType<ImportPlan>(await importOperation.PrepareAsync(
			fixture.Context,
			new OperationArguments(new Dictionary<string, object?>
			{
				["source-directory"] = steamDirectory,
				["steam-id64"] = SteamId,
				["target-slot"] = slotName
			}),
			importWorkspace,
			CancellationToken.None));

		Assert.Equal(6, importPlan.Mutations.Count);
		Assert.All(importPlan.Mutations, x => Assert.Equal(slotName, x.Target.ContainerName));
		Assert.DoesNotContain(importPlan.Mutations, x => x.Target.ContainerName == "PROFILE");
		var checksum = Assert.IsType<PlannedReplacement>(
			importPlan.Mutations.Single(x => x.Target.FileName == "game_duration.dat.checksum"));
		Assert.Equal(IdTechBlockChecksum.CreateSidecar(Duration, sizeof(uint)), File.ReadAllBytes(checksum.PreparedFile));
	}

	private static byte[] WithSlotFileVersion(byte[] payload, uint version)
	{
		var output = (byte[])payload.Clone();
		BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0, sizeof(uint)), version);
		return output;
	}

	private sealed class DoomFixture : IDisposable
	{
		public static readonly string[] AutosaveFileNames =
		{
			"game.details",
			"game.details-BACKUP",
			"game_duration.dat",
			"game_duration.dat-BACKUP"
		};

		private readonly string _root;
		public GameSaveContext Context { get; }

		public DoomFixture(
			string slotName = "GAME-AUTOSAVE1",
			bool doomEternal = false)
		{
			_root = Path.Combine(Path.GetTempPath(), "XgpSaveTools.Tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_root);
			var entries = new List<ContainerEntry>();
			foreach (var fileName in AutosaveFileNames)
			{
				var data = fileName.StartsWith("game.details", StringComparison.Ordinal) ? Details : Duration;
				entries.Add(CreateEntry("xgp", fileName, data));
			}
			var checksumLength = doomEternal ? sizeof(uint) : sizeof(ulong);
			entries.Add(CreateEntry("xgp", "game_duration.dat.checksum", new byte[checksumLength]));
			entries.Add(CreateEntry("xgp", "game_duration.dat-BACKUP.checksum", new byte[checksumLength]));

			var user = new UserContainerFolder("TESTUSER", _root);
			var game = new GameInfo(
				doomEternal ? "Doom Eternal" : "DOOM: The Dark Ages",
				doomEternal ? "BethesdaSoftworks.DOOMEternal-PC_3275kfvn8vcwc" : "BethesdaSoftworks.ProjectTitan_3275kfvn8vcwc",
				doomEternal ? DoomEternalHandler.HandlerId : DoomDarkAgesHandler.HandlerId,
				null);
			Context = new GameSaveContext(
				game,
				user,
				game.Package,
				new[] { new ContainerMetaFile(slotName, 1, entries) });
		}

		public string CreateDirectory(string relative)
		{
			return Directory.CreateDirectory(Path.Combine(_root, relative)).FullName;
		}

		private ContainerEntry CreateEntry(string directory, string name, byte[] data)
		{
			var path = Path.Combine(CreateDirectory(directory), Guid.NewGuid().ToString("N"));
			File.WriteAllBytes(path, data);
			return new ContainerEntry(name, path);
		}

		public void Dispose()
		{
			if (Directory.Exists(_root)) Directory.Delete(_root, true);
		}
	}
}
