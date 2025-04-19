using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using static XgpSaveTools.Extensions.BinaryReaderExtensions;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XgpSaveTools.Records;
using XgpSaveTools.SaveHandlers;
using static XgpSaveTools.Common.GameList;
using XgpSaveTools.BinaryStructures;
using static XgpSaveTools.Extensions.IoExtensions;
using XgpSaveTools.Extensions;

namespace XgpSaveTools
{
	// Main extractor class
	public class SaveManager
	{
		private string? GetXboxUserName(ulong uid)
		{
			try
			{
				var path = Path.Combine(PackagesRoot, "Microsoft.XboxApp_8wekyb3d8bbwe", "LocalState", "XboxLiveGamer.xml");
				if (!File.Exists(path)) return null;
				using var doc = JsonDocument.Parse(File.ReadAllText(path));
				var root = doc.RootElement;
				if (root.GetProperty("XboxUserId").GetUInt64() == uid)
					return root.GetProperty("Gamertag").GetString();
			}
			catch { }
			return null;
		}

		public List<UserContainerFolder> FindUserContainers(string pkg)
		{
			var baseDir = Path.Combine(PackagesRoot, pkg, "SystemAppData", "wgs");
			if (!Directory.Exists(baseDir)) return new();
			var dirs = Directory.GetDirectories(baseDir)
				.Where(d => !Path.GetFileName(d).Equals("t", StringComparison.OrdinalIgnoreCase))
				.Where(d => !Path.GetFileName(d).Contains("backup", StringComparison.OrdinalIgnoreCase))
				.Where(d => Path.GetFileName(d).Split('_').Length == 2);
			var result = new List<UserContainerFolder>();
			foreach (var d in dirs)
			{
				var parts = Path.GetFileName(d).Split('_', 2);
				var uid = Convert.ToUInt64(parts[0], 16);
				result.Add(new(GetXboxUserName(uid) ?? parts[0], d));
			}
			return result;
		}

		public (string StorePkg, List<ContainerMetaFile> Containers) ReadUserContainers(string userWgsDir)
		{
			// parse file structure info from container index content
			var idxPath = Path.Combine(userWgsDir, "containers.index");
			var idxModel = ContainerIndexBinaryStructure.Parse(idxPath);
			var storePkg = idxModel.StorePackage.Split('!')[0];
			var containers = new List<ContainerMetaFile>();

			// map into model
			foreach (var slot in idxModel.Entries)
			{
				// the folder is named by the GUID
				var containerFolder = Path.Combine(
					userWgsDir,
					slot.FileId.ToString("N").ToUpper()
				);

				// container blob file is named 'container.{slot.ContainerNum}'
				var blobPath = Path.Combine(containerFolder, $"container.{slot.ContainerNum}");
				if (!File.Exists(blobPath))
				{
					Console.WriteLine($"!! Missing container for slot {slot.Name1}");
					continue;
				}
				var files = ReadContainerBlob(containerFolder, slot.ContainerNum);

				containers.Add(new ContainerMetaFile(
					slot.Name1,
					slot.ContainerNum,
				 files
				));
			}

			return (storePkg, containers);
		}

		private List<ContainerEntry> ReadContainerBlob(string containerFolder, int containerNum)
		{
			var results = new List<ContainerEntry>();
			var blobPath = Path.Combine(containerFolder, $"container.{containerNum}");
			using var fs = File.OpenRead(blobPath);
			using var br = new BinaryReader(fs, Encoding.Unicode);

			// skip the 4‑byte unknown header
			br.ReadBytes(4);

			// number of files
			int fileCount = br.ReadInt32();

			for (int i = 0; i < fileCount; i++)
			{
				// 128‑char UTF‑16 file name
				string fileName = br.ReadUtf16(64);

				// two GUID entries
				var g1 = new Guid(br.ReadBytes(16));
				var g2 = new Guid(br.ReadBytes(16));

				// find which GUID file actually exists
				string path1 = Path.Combine(containerFolder, g1.ToString("N").ToUpper());
				string path2 = Path.Combine(containerFolder, g2.ToString("N").ToUpper());

				string chosen = (g1 == g2 || File.Exists(path1) && !File.Exists(path2))
					? path1
					: File.Exists(path2)
						? path2
						: null!;

				if (chosen == null)
				{
					Console.WriteLine($"!! Missing file blob for {fileName}");
					continue;
				}

				results.Add(new ContainerEntry(fileName, chosen));
			}

			return results;
		}

		public void AddEntry(FileInfo file, GameInfo info, UserContainerFolder userContainer)
		{
			throw new NotImplementedException();
			var (storePkg, conts) = ReadUserContainers(userContainer.Dir);

			using var fs = File.OpenRead(file.FullName);
			using var br = new BinaryReader(fs, Encoding.Unicode);

			//build entry
			var entry = new ContainerEntryBinaryModel()
			{
				ContainerNum = (byte)(conts.Max(x => x.Number) + 1)
			};
		}

		public void RemoveEntry(Guid fileId, GameInfo info, UserContainerFolder userContainer) => throw new NotImplementedException();

		public string BackupFolder(GameInfo gameInfo, UserContainerFolder userContainer) => CopyDirectory(userContainer.Dir, Path.Combine(BackupOutput, gameInfo.Name, userContainer.UserTag));
		private string GetZipName(GameInfo gameInfo, UserContainerFolder userContainer)
		{
			var formatted = gameInfo.Name.Replace(' ', '_').Replace(':', '_').Replace("'", "").Replace("!", "").ToLower();
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
			return $"{formatted}_{userContainer.UserTag}_{timestamp}.zip";
		}

		public IEnumerable<SaveFile> GetSaveEntries(GameInfo info, UserContainerFolder userContainer)
		{
			var (storePkg, conts) = ReadUserContainers(userContainer.Dir);
			var handler = SaveHandlerFactory.Get(info.Handler);
			return handler.GetSaveEntries(conts, info.HandlerArgs);
		}

		public void ReplaceEntries(GameInfo info, UserContainerFolder userContainer, IEnumerable<EntryReplacement> replacements)
		{
			Console.WriteLine($"{replacements.Count()} entries will be replaced");
			Console.WriteLine($"Backup created at {BackupFolder(info, userContainer)}");

			foreach (var rep in replacements)
			{
				Console.WriteLine($"Replacing {rep.TargetFile.Path}");
				File.Copy(rep.ReplacementFile.FullName, rep.TargetFile.Path, overwrite: true);
			}
			Console.WriteLine("");
			Console.WriteLine("All replacements completed.");
		}

		public void Extract(GameInfo info, UserContainerFolder userContainer)
		{
			Console.WriteLine($"- {info.Name}");
			try
			{
				var entries = GetSaveEntries(info, userContainer).ToList();
				if (!entries.Any())
				{
					Console.WriteLine($"No entries found for {userContainer.UserTag}");
					return;
				}

				string zipName = GetZipName(info, userContainer);
				using var zip = ZipFile.Open(zipName, ZipArchiveMode.Create);

				Console.WriteLine($"Saving files for user {userContainer.UserTag}:");
				foreach (var saveEntry in entries)
				{
					Console.WriteLine($"  - {saveEntry.OutputName}");
					zip.CreateEntryFromFile(saveEntry.ContainerEntry.Path, saveEntry.OutputName, CompressionLevel.Optimal);
				}

				Console.WriteLine($"Save files written to \"{zipName}\"\n");
			}
			catch (Exception ex)
			{
				Console.WriteLine("Failed to extract saves:");
				Console.WriteLine(ex);
				Console.WriteLine();
			}
			finally
			{
				IoExtensions.ClearTempFolders();
			}
		}


	}

}
