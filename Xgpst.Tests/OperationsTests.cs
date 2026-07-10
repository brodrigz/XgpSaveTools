using System.IO.Compression;
using System.Text;
using XgpSaveTools;
using XgpSaveTools.Extensions;
using XgpSaveTools.Operations;
using XgpSaveTools.Records;
using XgpSaveTools.SaveHandlers.Impl;
using XgpSaveTools.SaveHandlers.Impl.Generic;

namespace Xgpst.Tests;

public sealed class OperationsTests
{
	[Fact]
	public void OperationArguments_UsesDeclaredParameterValidation()
	{
		var parameter = new TextParameter(
			"id",
			"Identifier",
			Validator: value => value == "valid" ? null : "Identifier is invalid.");
		var arguments = new OperationArguments(new Dictionary<string, object?> { ["id"] = "invalid" });

		var error = Assert.Throws<ArgumentException>(() => arguments.Validate(new[] { parameter }));

		Assert.Contains("Identifier is invalid", error.Message);
	}

	[Fact]
	public void TempWorkspace_RejectsPathTraversal()
	{
		using var workspace = new TempWorkspace();

		Assert.Throws<InvalidOperationException>(() => workspace.GetPath("../escape.bin"));
	}

	[Fact]
	public void TransformedLegacyHandler_AdvertisesExportOnly()
	{
		var adapter = new LegacySaveHandlerAdapter("persona-3-reload", new Persona3ReloadHandler());
		var context = CreateEmptyContext();

		var operations = adapter.GetOperations(context);

		Assert.Single(operations);
		Assert.Equal("extract", operations[0].Definition.Id);
	}

	[Fact]
	public void DirectLegacyHandler_AdvertisesExplicitReplaceAndDeleteOperations()
	{
		var adapter = new LegacySaveHandlerAdapter("generic", new GenericHandler());
		var context = CreateEmptyContext();

		var operationIds = adapter.GetOperations(context).Select(x => x.Definition.Id).ToArray();

		Assert.Equal(new[] { "extract", "replace-entry", "delete-entry" }, operationIds);
	}

	[Fact]
	public void ExportExecutor_CreatesArchiveFromPreparedArtifacts()
	{
		var root = Path.Combine(Path.GetTempPath(), "XgpSaveTools.Tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var prepared = Path.Combine(root, "prepared.bin");
			File.WriteAllBytes(prepared, new byte[] { 1, 2, 3 });
			var plan = new ExportPlan(
				new[] { new ExportArtifact("folder/save.bin", prepared) },
				"test-export.zip");
			var executor = new OperationExecutor(new XboxContainerRepository());

			var result = executor.ExecuteExport(plan, root);

			Assert.Equal(1, result.AffectedFiles);
			Assert.True(File.Exists(result.OutputPath));
			using var zip = ZipFile.OpenRead(result.OutputPath!);
			Assert.Equal("folder/save.bin", Assert.Single(zip.Entries).FullName);
		}
		finally
		{
			if (Directory.Exists(root)) Directory.Delete(root, true);
		}
	}

	[Fact]
	public void ExportExecutor_RejectsPathTraversal()
	{
		var root = Path.Combine(Path.GetTempPath(), "XgpSaveTools.Tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var prepared = Path.Combine(root, "prepared.bin");
			File.WriteAllBytes(prepared, new byte[] { 1 });
			var plan = new ExportPlan(
				new[] { new ExportArtifact("../escape.bin", prepared) },
				"unsafe.zip");
			var executor = new OperationExecutor(new XboxContainerRepository());

			Assert.Throws<InvalidOperationException>(() => executor.ExecuteExport(plan, root));
			Assert.False(File.Exists(Path.Combine(root, "unsafe.zip")));
		}
		finally
		{
			if (Directory.Exists(root)) Directory.Delete(root, true);
		}
	}

	[Fact]
	public void ImportExecutor_RollsBackAlreadyCommittedFiles()
	{
		using var fixture = new WgsFixture();
		var firstOriginal = File.ReadAllBytes(fixture.FirstEntry.Path);
		var secondOriginal = File.ReadAllBytes(fixture.SecondEntry.Path);
		var replacement1 = fixture.WriteFile("replacement-1", new byte[] { 9, 9, 9 });
		var replacement2 = fixture.WriteFile("replacement-2", new byte[] { 8, 8, 8 });
		var plan = new ImportPlan(
			new PlannedWgsMutation[]
			{
				new PlannedReplacement(new WgsEntryKey("TEST-SLOT", "first"), replacement1),
				new PlannedReplacement(new WgsEntryKey("TEST-SLOT", "second"), replacement2)
			},
			Array.Empty<string>());
		var executor = new OperationExecutor(
			fixture.Repository,
			fixture.BackupRoot,
			commitIndex =>
			{
				if (commitIndex == 1) throw new IOException("Injected commit failure.");
			});

		Assert.Throws<InvalidOperationException>(() => executor.ExecuteImport(fixture.Context, plan));
		Assert.Equal(firstOriginal, File.ReadAllBytes(fixture.FirstEntry.Path));
		Assert.Equal(secondOriginal, File.ReadAllBytes(fixture.SecondEntry.Path));
		Assert.Empty(Directory.GetFiles(fixture.UserRoot, "*.xgpst-*", SearchOption.AllDirectories));
	}

	private static GameSaveContext CreateEmptyContext()
	{
		var game = new GameInfo("Test", "Test.Package", "generic", null);
		var user = new UserContainerFolder("TEST", Path.GetTempPath());
		return new GameSaveContext(game, user, "Test.Package", Array.Empty<ContainerMetaFile>());
	}

	private sealed class WgsFixture : IDisposable
	{
		private readonly string _root;
		public string UserRoot { get; }
		public string BackupRoot { get; }
		public XboxContainerRepository Repository { get; } = new();
		public GameSaveContext Context { get; }
		public ContainerEntry FirstEntry { get; }
		public ContainerEntry SecondEntry { get; }

		public WgsFixture()
		{
			_root = Path.Combine(Path.GetTempPath(), "XgpSaveTools.Tests", Guid.NewGuid().ToString("N"));
			UserRoot = Directory.CreateDirectory(Path.Combine(_root, "wgs-user")).FullName;
			BackupRoot = Path.Combine(_root, "backups");
			var containerId = Guid.NewGuid();
			var firstId = Guid.NewGuid();
			var secondId = Guid.NewGuid();
			WriteIndex(containerId);
			var containerDirectory = Directory.CreateDirectory(
				Path.Combine(UserRoot, containerId.ToString("N").ToUpperInvariant())).FullName;
			WriteContainer(containerDirectory, firstId, secondId);
			File.WriteAllBytes(Path.Combine(containerDirectory, firstId.ToString("N").ToUpperInvariant()), new byte[] { 1, 2, 3 });
			File.WriteAllBytes(Path.Combine(containerDirectory, secondId.ToString("N").ToUpperInvariant()), new byte[] { 4, 5, 6 });

			var game = new GameInfo("Test", "Test.Package", "generic", null);
			var user = new UserContainerFolder("TESTUSER", UserRoot);
			Context = Repository.CreateGameSaveContext(game, user);
			var container = Assert.Single(Context.Containers);
			FirstEntry = container.Files.Single(x => x.Name == "first");
			SecondEntry = container.Files.Single(x => x.Name == "second");
		}

		public string WriteFile(string name, byte[] data)
		{
			var path = Path.Combine(_root, name);
			File.WriteAllBytes(path, data);
			return path;
		}

		private void WriteIndex(Guid containerId)
		{
			using var stream = File.Create(Path.Combine(UserRoot, "containers.index"));
			using var writer = new BinaryWriter(stream, Encoding.Unicode);
			writer.Write(14);
			writer.Write(1);
			writer.WriteUtf16(string.Empty);
			writer.WriteUtf16("Test.Package!Game");
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

		private static void WriteContainer(string directory, Guid firstId, Guid secondId)
		{
			using var stream = File.Create(Path.Combine(directory, "container.1"));
			using var writer = new BinaryWriter(stream, Encoding.Unicode);
			writer.Write(new byte[4]);
			writer.Write(2);
			WriteContainerEntry(writer, "first", firstId);
			WriteContainerEntry(writer, "second", secondId);
		}

		private static void WriteContainerEntry(BinaryWriter writer, string name, Guid id)
		{
			var nameBytes = new byte[64 * 2];
			Encoding.Unicode.GetBytes(name).CopyTo(nameBytes, 0);
			writer.Write(nameBytes);
			writer.Write(id.ToByteArray());
			writer.Write(id.ToByteArray());
		}

		public void Dispose()
		{
			if (Directory.Exists(_root)) Directory.Delete(_root, true);
		}
	}
}
