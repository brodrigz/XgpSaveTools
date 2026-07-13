using System.IO.Compression;
using System.Text;
using XgpSaveTools;
using XgpSaveTools.Extensions;
using XgpSaveTools.Records;
using XgpSaveTools.SaveSources;
using Xgpst_ConsoleApp;

namespace Xgpst.Tests;

public sealed class ConsoleIntegrationTests
{
	[Fact]
	public void ScanGames_UsesVirtualPackageRootAndOpensSelectedUserDirectory()
	{
		using var fixture = new VirtualSaveTree();
		var wgsRoot = fixture.CreateInstalledWgs();
		var openedDirectories = new List<string>();
		var console = new ScriptedConsoleHelper(selectionIndexes: new[] { 0, 0, 3 });
		var app = fixture.CreateApp(console, openDirectory: openedDirectories.Add);

		app.Reset();
		app.ScanGamesMode();

		Assert.Equal(fixture.Game, app.SelectedGame);
		Assert.Equal(fixture.UserRoot(wgsRoot), app.SelectedContainer!.Dir);
		Assert.Equal(new[] { fixture.UserRoot(wgsRoot) }, openedDirectories);
	}

	[Fact]
	public void EnterPath_UsesVirtualWgsOutsidePackageRootAndExtractsSave()
	{
		using var fixture = new VirtualSaveTree();
		var customWgs = fixture.CreateCustomWgs();
		var console = new ScriptedConsoleHelper(
			selectionIndexes: new[] { 0, 0 },
			directories: new[] { customWgs });
		var app = fixture.CreateApp(console, useDefaultSources: true);

		app.CustomPathMode();

		Assert.Equal(fixture.UserRoot(customWgs), app.SelectedContainer!.Dir);
		var archive = Assert.Single(Directory.GetFiles(fixture.OutputRoot, "*.zip"));
		using var zip = ZipFile.OpenRead(archive);
		var entry = Assert.Single(zip.Entries);
		Assert.Equal("save.dat", entry.FullName);
		using var stream = entry.Open();
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		Assert.Equal(new byte[] { 1, 2, 3, 4 }, memory.ToArray());
	}

	[Fact]
	public void EnterPath_UsesVirtualPgsSnapshotAndExtractsGameFiles()
	{
		using var fixture = new VirtualSaveTree();
		var pgsUserRoot = fixture.CreatePgs();
		var console = new ScriptedConsoleHelper(
			selectionIndexes: new[] { 1, 0 },
			directories: new[] { pgsUserRoot });
		var app = fixture.CreateApp(console);

		app.CustomPathMode();

		Assert.Equal("pgs", app.SelectedContainer!.Source);
		Assert.Equal(pgsUserRoot, app.SelectedContainer.Dir);
		var archive = Assert.Single(Directory.GetFiles(fixture.OutputRoot, "*.zip"));
		using var zip = ZipFile.OpenRead(archive);
		Assert.Equal("ContainersRoot/User_PROFILE/C_ProfileData", Assert.Single(zip.Entries).FullName);
	}

	private sealed class ScriptedConsoleHelper : ConsoleHelper
	{
		private readonly Queue<int> _selectionIndexes;
		private readonly Queue<string> _directories;

		public ScriptedConsoleHelper(
			IEnumerable<int>? selectionIndexes = null,
			IEnumerable<string>? directories = null)
		{
			_selectionIndexes = new Queue<int>(selectionIndexes ?? Array.Empty<int>());
			_directories = new Queue<string>(directories ?? Array.Empty<string>());
		}

		public override KeyValuePair<int, T> SelectOption<T>(
			IList<T> options,
			Func<T, string>? getLabelFunc = null,
			bool disableGoBack = false) =>
			SelectOption(options, "Select Option", getLabelFunc, disableGoBack);

		public override KeyValuePair<int, T> SelectOption<T>(
			IList<T> options,
			string prompt,
			Func<T, string>? getLabelFunc = null,
			bool disableGoBack = false)
		{
			Assert.NotEmpty(_selectionIndexes);
			var index = _selectionIndexes.Dequeue();
			if (index == -1 && !disableGoBack) return new KeyValuePair<int, T>(-1, default!);
			Assert.InRange(index, 0, options.Count - 1);
			return new KeyValuePair<int, T>(index, options[index]);
		}

		public override string ReadValidDirectory(string prompt)
		{
			Assert.NotEmpty(_directories);
			var path = _directories.Dequeue();
			Assert.True(Directory.Exists(path));
			return path;
		}

		public override void WaitInput() { }
		public override void WriteSuccess(string message) { }
		public override void WriteWarning(string message) { }
	}

	private sealed class VirtualSaveTree : IDisposable
	{
		private const string Package = "Test.VirtualGame_123456";
		private const string PgsGameId = "ABC123";
		private const string UserFolder = "ABC_1";
		private readonly string _root;

		public VirtualSaveTree()
		{
			_root = Path.Combine(Path.GetTempPath(), "XgpSaveTools.ConsoleTests", Guid.NewGuid().ToString("N"));
			PackagesRoot = Directory.CreateDirectory(Path.Combine(_root, "Packages")).FullName;
			PgsRoot = Directory.CreateDirectory(Path.Combine(_root, "pgs")).FullName;
			OutputRoot = Directory.CreateDirectory(Path.Combine(_root, "output")).FullName;
			Game = new GameInfo("Virtual WGS Game", Package, "generic", null);
			PgsGame = new GameInfo(
				"Virtual PGS Game", "Test.PgsPackage", "pgs-files", null,
				"pgs", new SourceArgs(PgsGameId));
		}

		public string PackagesRoot { get; }
		public string PgsRoot { get; }
		public string OutputRoot { get; }
		public GameInfo Game { get; }
		public GameInfo PgsGame { get; }

		public string CreateInstalledWgs()
		{
			var root = Directory.CreateDirectory(
				Path.Combine(PackagesRoot, Package, "SystemAppData", "wgs")).FullName;
			CreateWgs(root);
			return root;
		}

		public string CreateCustomWgs()
		{
			var root = Directory.CreateDirectory(Path.Combine(_root, "external", "wgs")).FullName;
			CreateWgs(root);
			return root;
		}

		public string CreatePgs()
		{
			var userRoot = Directory.CreateDirectory(
				Path.Combine(PgsRoot, $"u_123456789_{PgsGameId}")).FullName;
			var profile = Directory.CreateDirectory(
				Path.Combine(userRoot, "1", "ContainersRoot", "User_PROFILE")).FullName;
			File.WriteAllBytes(Path.Combine(profile, "C_ProfileData"), new byte[] { 5, 6, 7, 8 });
			File.WriteAllText(Path.Combine(userRoot, "1.json"), "{\"Manifest\":{}}");
			return userRoot;
		}

		public string UserRoot(string wgsRoot) => Path.Combine(wgsRoot, UserFolder);

		public ConsoleApp CreateApp(
			ConsoleHelper helper,
			Action<string>? openDirectory = null,
			bool useDefaultSources = false)
		{
			var repository = new XboxContainerRepository(PackagesRoot);
			var sources = useDefaultSources
				? null
				: new GameSaveSourceResolver(
					new WgsGameSaveSource(repository),
					new PgsGameSaveSource(PgsRoot));
			IReadOnlyList<GameInfo> games = useDefaultSources
				? new[] { Game }
				: new[] { Game, PgsGame };
			return new ConsoleApp(
				repository,
				helper,
				games,
				sources,
				OutputRoot,
				openDirectory);
		}

		private static void CreateWgs(string wgsRoot)
		{
			var userRoot = Directory.CreateDirectory(Path.Combine(wgsRoot, UserFolder)).FullName;
			var containerId = Guid.NewGuid();
			var fileId = Guid.NewGuid();

			using (var stream = File.Create(Path.Combine(userRoot, "containers.index")))
			using (var writer = new BinaryWriter(stream, Encoding.Unicode))
			{
				writer.Write(14);
				writer.Write(1);
				writer.WriteUtf16(string.Empty);
				writer.WriteUtf16(Package + "!Game");
				writer.Write(new byte[8]);
				writer.Write(new byte[4]);
				writer.WriteUtf16(string.Empty);
				writer.Write(new byte[8]);
				writer.WriteUtf16("TEST-SLOT");
				writer.WriteUtf16("TEST-SLOT");
				writer.WriteUtf16("\"0x0\"");
				writer.Write((byte)1);
				writer.Write(1);
				writer.Write(containerId.ToByteArray());
				writer.Write(new byte[24]);
			}

			var containerRoot = Directory.CreateDirectory(
				Path.Combine(userRoot, containerId.ToString("N").ToUpperInvariant())).FullName;
			using (var stream = File.Create(Path.Combine(containerRoot, "container.1")))
			using (var writer = new BinaryWriter(stream, Encoding.Unicode))
			{
				writer.Write(new byte[4]);
				writer.Write(1);
				var name = new byte[64 * 2];
				Encoding.Unicode.GetBytes("save.dat").CopyTo(name, 0);
				writer.Write(name);
				writer.Write(fileId.ToByteArray());
				writer.Write(fileId.ToByteArray());
			}

			File.WriteAllBytes(
				Path.Combine(containerRoot, fileId.ToString("N").ToUpperInvariant()),
				new byte[] { 1, 2, 3, 4 });
		}

		public void Dispose()
		{
			if (Directory.Exists(_root)) Directory.Delete(_root, true);
		}
	}
}
