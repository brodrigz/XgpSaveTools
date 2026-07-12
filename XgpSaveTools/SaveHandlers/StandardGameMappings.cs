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

	public static IEnumerable<MappedSaveEntry> StateOfDecay2(GameSaveContext context)
	{
		var container = context.Containers.First();
		foreach (var entry in container.Files)
			yield return MappedSaveEntry.Create(Path.GetFileName(entry.Name) + ".sav", container, entry);
	}
}
