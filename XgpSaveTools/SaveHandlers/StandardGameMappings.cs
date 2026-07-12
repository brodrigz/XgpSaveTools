using XgpSaveTools.Common;
using XgpSaveTools.Operations;

namespace XgpSaveTools.SaveHandlers;

internal static class StandardGameMappings
{
	public static IEnumerable<MappedSaveEntry> Generic(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		foreach (var entry in container.Files)
			yield return MappedSaveEntry.Create(entry.Name + (context.Game.HandlerArgs?.Suffix ?? string.Empty), container, entry);
	}

	public static IEnumerable<MappedSaveEntry> OneFilePerContainer(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		{
			if (container.Files.Count == 0) continue;
			yield return MappedSaveEntry.Create(
				container.Name + (context.Game.HandlerArgs?.Suffix ?? string.Empty),
				container,
				container.Files[0]);
		}
	}

	public static IEnumerable<MappedSaveEntry> FirstContainer(GameSaveContext context)
	{
		var container = context.Containers.FirstOrDefault();
		if (container == null) yield break;
		foreach (var entry in container.Files)
			yield return MappedSaveEntry.Create(entry.Name + (context.Game.HandlerArgs?.Suffix ?? string.Empty), container, entry);
	}

	public static IEnumerable<MappedSaveEntry> ContainerFolders(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		foreach (var entry in container.Files)
			yield return MappedSaveEntry.Create(
				Path.Combine(container.Name, entry.Name).Replace('\\', '/'), container, entry);
	}

	public static IEnumerable<MappedSaveEntry> ArcadeParadise(GameSaveContext context)
	{
		var container = context.Containers.First();
		yield return MappedSaveEntry.Create("RATSaveData.dat", container, container.Files.First());
	}

	public static IEnumerable<MappedSaveEntry> Balatro(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		foreach (var entry in container.Files)
		{
			var outputName = container.Name.Equals("common", StringComparison.OrdinalIgnoreCase)
				? entry.Name
				: Path.Combine(container.Name, entry.Name).Replace('\\', '/');
			yield return MappedSaveEntry.Create(outputName, container, entry);
		}
	}

	public static IEnumerable<MappedSaveEntry> CoralIsland(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		{
			var name = container.Name + ".sav";
			if (name.StartsWith("Backup", StringComparison.OrdinalIgnoreCase))
				name = $"Backup/{name["Backup".Length..]}";
			yield return MappedSaveEntry.Create(name, container, container.Files[0]);
		}
	}

	public static IEnumerable<MappedSaveEntry> Cricket24(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		foreach (var entry in container.Files)
		{
			var name = entry.Name;
			if (name.EndsWith(".CHUNK0", StringComparison.OrdinalIgnoreCase)) name = name[..^7];
			else if (name.Contains("CHUNK", StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException($"Unexpected chunk in {entry.Name}");
			yield return MappedSaveEntry.Create($"{container.Name}/{name}.SAV", container, entry);
		}
	}

	public static IEnumerable<MappedSaveEntry> Forza(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		foreach (var entry in container.Files)
			yield return MappedSaveEntry.Create($"{container.Name}.{entry.Name}", container, entry);
	}

	public static IEnumerable<MappedSaveEntry> Galacticare(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		foreach (var entry in container.Files)
		if (entry.Name.Equals("PlayerData", StringComparison.OrdinalIgnoreCase))
			yield return MappedSaveEntry.Create(container.Name, container, entry);
	}

	public static IEnumerable<MappedSaveEntry> LiesOfP(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		{
			var index = 0;
			while (index < container.Name.Length && char.IsDigit(container.Name[index])) index++;
			yield return MappedSaveEntry.Create(container.Name[index..] + ".sav", container, container.Files[0]);
		}
	}

	public static IEnumerable<MappedSaveEntry> LikeADragon(GameSaveContext context)
	{
		var iconFormat = context.Game.HandlerArgs?.IconFormat;
		foreach (var container in context.Containers)
		{
			var path = Path.GetDirectoryName(container.Name) ?? string.Empty;
			var leaf = Path.GetFileName(container.Name);
			var baseName = leaf switch
			{
				"datasav" => Path.Combine(path, "data.sav"),
				"datasys" => Path.Combine(path, "data.sys"),
				_ => container.Name
			};

			foreach (var entry in container.Files)
			{
				if (entry.Name.Equals("data", StringComparison.OrdinalIgnoreCase))
					yield return MappedSaveEntry.Create(baseName, container, entry);
				else if (entry.Name.Equals("icon", StringComparison.OrdinalIgnoreCase) && iconFormat != null)
				{
					var parent = Path.GetFileName(path);
					var iconName = Path.ChangeExtension(Path.Combine(path, parent + "_icon"), iconFormat);
					yield return MappedSaveEntry.Create(iconName, container, entry);
				}
			}
		}
	}

	public static IEnumerable<MappedSaveEntry> MetaphorRefantazio(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		{
			if (container.Files.Count == 0) continue;

			string outputName;
			if (container.Name.StartsWith("System", StringComparison.OrdinalIgnoreCase))
				outputName = "system.sav";
			else if (container.Name.StartsWith("SaveData", StringComparison.OrdinalIgnoreCase))
				outputName = "save" + container.Name["SaveData".Length..] + ".sav";
			else
				throw new InvalidDataException(
					$"Unexpected Metaphor: ReFantazio save container '{container.Name}'.");

			yield return MappedSaveEntry.Create(outputName, container, container.Files[0]);
		}
	}

	public static IEnumerable<MappedSaveEntry> NinjaGaiden2Black(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		{
			if (container.Files.Count == 0) continue;
			var name = container.Name;
			if (!name.EndsWith("DAT", StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException(
					$"Unexpected Ninja Gaiden 2 Black container '{container.Name}'.");

			var beforeDat = name[..^3];
			var trailingDigits = 0;
			while (trailingDigits < beforeDat.Length && char.IsDigit(beforeDat[^(trailingDigits + 1)]))
				trailingDigits++;
			string? category = null;
			for (var suffixLength = 1; suffixLength <= trailingDigits; suffixLength++)
			{
				var core = beforeDat[..^suffixLength];
				if (core.Length == 0 || core.Length % 2 != 0) continue;
				var half = core.Length / 2;
				if (core[..half].Equals(core[half..], StringComparison.OrdinalIgnoreCase))
				{
					category = core[..half];
					break;
				}
			}
			if (category == null)
				throw new InvalidDataException(
					$"Could not derive a repeated save category from '{container.Name}'.");

			if (container.Files.Count != 1)
				throw new InvalidDataException(
					$"Ninja Gaiden 2 Black container '{container.Name}' contains {container.Files.Count} files; expected one.");
			yield return MappedSaveEntry.Create(
				$"{category}/{category}.sav", container, container.Files[0]);
		}
	}

	public static IEnumerable<MappedSaveEntry> Palworld(GameSaveContext context)
	{
		foreach (var container in context.Containers)
			yield return MappedSaveEntry.Create(
				container.Name.Replace("-", "/") + ".sav", container, container.Files[0]);
	}

	public static IEnumerable<MappedSaveEntry> RailwayEmpire2(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		foreach (var entry in container.Files)
			if (entry.Name.Equals("savegame", StringComparison.OrdinalIgnoreCase))
				yield return MappedSaveEntry.Create(container.Name, container, entry);
	}

	public static IEnumerable<MappedSaveEntry> Scorn(GameSaveContext context)
	{
		var suffixes = new[] { "dat", "sav", "info" };
		foreach (var entry in OneFilePerContainer(context))
		{
			var name = entry.OutputName.EndsWithAny(suffixes)
				? StringExtensions.FixMissingDotOnExtension(entry.OutputName, suffixes)
				: entry.OutputName;
			yield return entry with { OutputName = name };
		}
	}

	public static IEnumerable<MappedSaveEntry> Silksong(GameSaveContext context)
	{
		foreach (var container in context.Containers)
		foreach (var entry in container.Files)
		{
			string outputName;
			if (container.Name.Contains("shared", StringComparison.OrdinalIgnoreCase) ||
			    container.Name.Contains("save", StringComparison.OrdinalIgnoreCase))
			{
				outputName = entry.Name;
			}
			else if (container.Name.Contains("restore", StringComparison.OrdinalIgnoreCase))
			{
				var numberStart = -1;
				var numberLength = 0;
				for (var index = 0; index < container.Name.Length; index++)
				{
					if (!char.IsDigit(container.Name[index]))
					{
						if (numberStart >= 0) break;
						continue;
					}

					if (numberStart < 0) numberStart = index;
					numberLength++;
				}

				if (numberStart < 0)
					throw new InvalidDataException(
						$"Silksong restore container '{container.Name}' does not contain a restore-point number.");

				var number = container.Name.Substring(numberStart, numberLength);
				outputName = Path.Combine($"Restore_Points{number}", entry.Name).Replace('\\', '/');
			}
			else
			{
				outputName = Path.Combine(container.Name, entry.Name).Replace('\\', '/');
			}

			yield return MappedSaveEntry.Create(outputName, container, entry);
		}
	}

	public static IEnumerable<MappedSaveEntry> StateOfDecay2(GameSaveContext context)
	{
		var container = context.Containers.First();
		foreach (var entry in container.Files)
			yield return MappedSaveEntry.Create(Path.GetFileName(entry.Name) + ".sav", container, entry);
	}
}
