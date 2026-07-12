using System.IO.Compression;
using System.Text.Json;
using XgpSaveTools;
using XgpSaveTools.Operations;
using XgpSaveTools.Records;
using XgpSaveTools.SaveHandlers;
using XgpSaveTools.SaveSources;

namespace Xgpst.Tests;

public sealed class PgsTests
{
	[Fact]
	public void PgsSource_DiscoversNumericSnapshotAndPreservesFileTree()
	{
		using var fixture = new PgsFixture();

		var locations = fixture.Source.FindUserContainers(fixture.Game);
		var location = Assert.Single(locations);
		var context = fixture.Source.CreateContext(fixture.Game, location);

		Assert.Equal("pgs", context.Source);
		Assert.Equal("1", context.PgsSnapshot!.SnapshotId);
		Assert.Equal("123456789", context.PgsSnapshot.Xuid);
		Assert.Equal(
			new[] { "1.json", "ContainersRoot/User_PROFILE/C_ProfileData" },
			context.PgsSnapshot.Files.Select(x => x.RelativePath).ToArray());
	}

	[Fact]
	public async Task PgsHandler_PreparesIntegrityManifestedBackupFromStagedCopies()
	{
		using var fixture = new PgsFixture();
		var context = fixture.CreateContext();
		var handler = GameSaveHandlerRegistry.Resolve(fixture.Game);
		var operation = handler.GetOperations(context).Single(x => x.Definition.Id == "backup-pgs");
		using var workspace = new TempWorkspace();

		var plan = Assert.IsType<ExportPlan>(await operation.PrepareAsync(
			context,
			new OperationArguments(new Dictionary<string, object?>()),
			workspace,
			CancellationToken.None));

		Assert.NotEmpty(plan.Warnings!);
		Assert.Equal(
			new[]
			{
				"pgs-backup.json",
				"metadata/1.json",
				"snapshot/ContainersRoot/User_PROFILE/C_ProfileData"
			},
			plan.Files.Select(x => x.OutputName).ToArray());
		Assert.All(plan.Files, x => Assert.StartsWith(workspace.RootPath, x.PreparedFile));

		using var manifest = JsonDocument.Parse(File.ReadAllText(plan.Files[0].PreparedFile));
		Assert.Equal("16D460", manifest.RootElement.GetProperty("game_id").GetString());
		Assert.True(manifest.RootElement.GetProperty("consistency_guard_passed").GetBoolean());
		Assert.Equal(2, manifest.RootElement.GetProperty("files").GetArrayLength());
	}

	[Fact]
	public async Task PgsHandler_ExtractsOnlyContainersRoot()
	{
		using var fixture = new PgsFixture();
		var context = fixture.CreateContext();
		var handler = GameSaveHandlerRegistry.Resolve(fixture.Game);
		var operation = handler.GetOperations(context).Single(x => x.Definition.Id == "extract");
		using var workspace = new TempWorkspace();

		var plan = Assert.IsType<ExportPlan>(await operation.PrepareAsync(
			context,
			new OperationArguments(new Dictionary<string, object?>()),
			workspace,
			CancellationToken.None));

		Assert.Equal(
			"ContainersRoot/User_PROFILE/C_ProfileData",
			Assert.Single(plan.Files).OutputName);
	}

	[Fact]
	public void ImportExecutor_RejectsPgsContext()
	{
		using var fixture = new PgsFixture();
		var context = fixture.CreateContext();
		var plan = new ImportPlan(
			new PlannedWgsMutation[]
			{
				new PlannedReplacement(new WgsEntryKey("container", "file"), fixture.ProfilePath)
			},
			Array.Empty<string>());

		var error = Assert.Throws<NotSupportedException>(() =>
			new OperationExecutor(new XboxContainerRepository()).ExecuteImport(context, plan));

		Assert.Contains("read-only", error.Message);
	}

	private sealed class PgsFixture : IDisposable
	{
		private readonly string _root;
		public PgsGameSaveSource Source { get; }
		public GameInfo Game { get; } = new(
			"Forza Horizon 6",
			"Microsoft.ForteBaseGame_8wekyb3d8bbwe",
			"pgs-forza",
			null,
			"pgs",
			new SourceArgs("16D460"));
		public string ProfilePath { get; }

		public PgsFixture()
		{
			_root = Path.Combine(Path.GetTempPath(), "XgpSaveTools.Tests", Guid.NewGuid().ToString("N"));
			var pgsRoot = Directory.CreateDirectory(Path.Combine(_root, "pgs")).FullName;
			var userRoot = Directory.CreateDirectory(
				Path.Combine(pgsRoot, "u_123456789_16D460")).FullName;
			var profileRoot = Directory.CreateDirectory(
				Path.Combine(userRoot, "1", "ContainersRoot", "User_PROFILE")).FullName;
			ProfilePath = Path.Combine(profileRoot, "C_ProfileData");
			File.WriteAllBytes(ProfilePath, new byte[] { 1, 2, 3, 4 });
			File.WriteAllText(Path.Combine(userRoot, "1.json"), "{\"Manifest\":{}}");
			Source = new PgsGameSaveSource(pgsRoot);
		}

		public GameSaveContext CreateContext()
		{
			var location = Assert.Single(Source.FindUserContainers(Game));
			return Source.CreateContext(Game, location);
		}

		public void Dispose()
		{
			if (Directory.Exists(_root)) Directory.Delete(_root, true);
		}
	}
}
