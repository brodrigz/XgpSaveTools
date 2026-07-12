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
using System.Runtime.Versioning;
using System.IO;
using XgpSaveTools.Operations;

namespace XgpSaveTools
{
	public class XboxContainerRepository
	{
		public string? OverrideWgsPath { get; set; }


		public List<UserContainerFolder> FindUserContainers(string pkg)
		{
			var baseDir = Path.Combine(PackagesRoot, pkg, "SystemAppData", "wgs");
			return InnerFindUserContainers(OverrideWgsPath ?? baseDir);
		}

		public GameInfo? DiscoverGameInfoFromPath(string path)
		{
			var userContainers = FindUserContainers(path);
			if (!userContainers.Any()) throw new Exception("No user container found, directory is not on wgs format");
			// read first to discover package
			var result = ReadUserContainers(userContainers.FirstOrDefault().Dir);
			var found = ReadGameList().FirstOrDefault(x => x.Package == result.StorePkg);
			if (found == null)
			{
				return new UnregisteredGameInfo(result.StorePkg, result.StorePkg, "generic", null);
			}
			return found;
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

		public GameSaveContext CreateGameSaveContext(GameInfo info, UserContainerFolder userContainer)
		{
			var (storePackage, containers) = ReadUserContainers(userContainer.Dir);
			return new GameSaveContext(info, userContainer, storePackage, containers);
		}

		#region PRIVATE
		private List<UserContainerFolder> InnerFindUserContainers(string baseDir)
		{
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
				result.Add(new(parts[0], d));
			}
			return result;
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

				if (chosen == null || !File.Exists(chosen))
				{
					Console.WriteLine($"!! Missing file blob for {fileName}");
					continue;
				}

				results.Add(new ContainerEntry(fileName, chosen));
			}

			return results;
		}

		#endregion

	}

}
