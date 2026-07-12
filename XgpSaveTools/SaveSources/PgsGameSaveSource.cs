using System.Text.RegularExpressions;
using XgpSaveTools.Extensions;
using XgpSaveTools.Operations;
using XgpSaveTools.Records;

namespace XgpSaveTools.SaveSources;

public sealed class PgsGameSaveSource : IGameSaveSource
{
	private static readonly Regex UserRootPattern = new(
		@"^u_(?<xuid>[0-9]+)_(?<gameId>[A-Fa-f0-9]+)$",
		RegexOptions.CultureInvariant | RegexOptions.Compiled);

	public PgsGameSaveSource(string? rootPath = null)
	{
		RootPath = Path.GetFullPath(rootPath ?? Path.Combine(
			Environment.GetEnvironmentVariable("SystemDrive") ?? "C:",
			"XboxGames", "GameSave", "pgs"));
	}

	public string Id => "pgs";
	public string RootPath { get; }

	public IReadOnlyList<UserContainerFolder> FindUserContainers(GameInfo game)
	{
		var gameId = RequireGameId(game);
		if (!Directory.Exists(RootPath)) return Array.Empty<UserContainerFolder>();

		var results = new List<UserContainerFolder>();
		foreach (var directory in Directory.EnumerateDirectories(RootPath))
		{
			if (!TryParseUserRoot(directory, out var xuid, out var foundGameId) ||
				!string.Equals(gameId, foundGameId, StringComparison.OrdinalIgnoreCase))
				continue;

			if (TryResolveCurrentSnapshot(directory, out var snapshotId, out _))
			{
				results.Add(new UserContainerFolder(xuid, directory, Id));
				continue;
			}

			foreach (var snapshot in EnumerateNumericSnapshots(directory))
				results.Add(new UserContainerFolder(
					$"{xuid} (snapshot {snapshot.Name})", directory, Id, snapshot.Name));
		}

		return results;
	}

	public GameSaveContext CreateContext(GameInfo game, UserContainerFolder location)
	{
		if (!string.Equals(location.Source, Id, StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("The selected location is not a PGS save.");

		var userRoot = Path.GetFullPath(location.Dir);
		if (!TryParseUserRoot(userRoot, out var xuid, out var gameId))
			throw new InvalidDataException($"Invalid PGS user directory: {userRoot}");
		var expectedGameId = RequireGameId(game);
		if (!string.Equals(gameId, expectedGameId, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException(
				$"PGS game ID mismatch. Expected {expectedGameId}, found {gameId}.");

		string snapshotId;
		string snapshotRoot;
		var selectedThroughCurrent = false;
		if (!string.IsNullOrWhiteSpace(location.SnapshotId))
		{
			snapshotId = location.SnapshotId;
			if (!snapshotId.All(char.IsDigit))
				throw new InvalidDataException($"Invalid PGS snapshot ID: {snapshotId}");
			snapshotRoot = Path.GetFullPath(Path.Combine(userRoot, snapshotId));
			EnsureWithinRoot(userRoot, snapshotRoot);
		}
		else if (TryResolveCurrentSnapshot(userRoot, out snapshotId, out snapshotRoot))
		{
			selectedThroughCurrent = true;
		}
		else
		{
			throw new InvalidDataException(
				"The PGS current snapshot is missing or invalid. Select a numeric snapshot explicitly.");
		}

		if (!Directory.Exists(snapshotRoot))
			throw new DirectoryNotFoundException(snapshotRoot);
		var containersRoot = Path.Combine(snapshotRoot, "ContainersRoot");
		if (!Directory.Exists(containersRoot))
			throw new DirectoryNotFoundException(
				$"The PGS snapshot does not contain ContainersRoot: {snapshotRoot}");

		var files = new List<PgsSaveFile>();
		foreach (var file in EnumerateRegularFiles(containersRoot))
		{
			var relative = Path.GetRelativePath(containersRoot, file)
				.Replace('\\', '/');
			ValidateRelativePath(relative);
			files.Add(new PgsSaveFile($"ContainersRoot/{relative}", file, false));
		}

		foreach (var file in Directory.EnumerateFiles(userRoot, "*.json", SearchOption.TopDirectoryOnly))
		{
			if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
			var name = Path.GetFileName(file);
			ValidateRelativePath(name);
			files.Add(new PgsSaveFile(name, Path.GetFullPath(file), true));
		}

		var duplicate = files.GroupBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault(x => x.Count() > 1);
		if (duplicate != null)
			throw new InvalidDataException($"Duplicate PGS path: {duplicate.Key}");

		var snapshot = new PgsSnapshot(
			userRoot, xuid, gameId, snapshotId, snapshotRoot, containersRoot,
			files.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToList(),
			selectedThroughCurrent);
		return new GameSaveContext(
			game,
			location with { UserTag = xuid, SnapshotId = snapshotId },
			game.Package,
			Array.Empty<ContainerMetaFile>(),
			snapshot);
	}

	public IEnumerable<(string Xuid, string GameId, string Path)> EnumerateUserRoots()
	{
		if (!Directory.Exists(RootPath)) yield break;
		foreach (var directory in Directory.EnumerateDirectories(RootPath))
			if (TryParseUserRoot(directory, out var xuid, out var gameId))
				yield return (xuid, gameId, directory);
	}

	private static string RequireGameId(GameInfo game) =>
		!string.IsNullOrWhiteSpace(game.SourceArgs?.GameId)
			? game.SourceArgs.GameId
			: throw new InvalidDataException($"PGS game '{game.Name}' does not define source_args.game_id.");

	private static bool TryParseUserRoot(string path, out string xuid, out string gameId)
	{
		var match = UserRootPattern.Match(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));
		xuid = match.Success ? match.Groups["xuid"].Value : string.Empty;
		gameId = match.Success ? match.Groups["gameId"].Value : string.Empty;
		return match.Success;
	}

	private static IReadOnlyList<DirectoryInfo> EnumerateNumericSnapshots(string userRoot) =>
		new DirectoryInfo(userRoot).EnumerateDirectories()
			.Select(x => (Directory: x, Parsed: ulong.TryParse(x.Name, out var value), Value: value))
			.Where(x => x.Parsed)
			.OrderBy(x => x.Value)
			.Select(x => x.Directory)
			.ToList();

	private static bool TryResolveCurrentSnapshot(
		string userRoot,
		out string snapshotId,
		out string snapshotRoot)
	{
		snapshotId = string.Empty;
		snapshotRoot = string.Empty;
		var current = new DirectoryInfo(Path.Combine(userRoot, "current"));
		if (!current.Exists || !current.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;

		try
		{
			var target = current.ResolveLinkTarget(true);
			if (target == null || !target.Exists) return false;
			snapshotRoot = Path.GetFullPath(target.FullName);
			EnsureWithinRoot(userRoot, snapshotRoot);
			snapshotId = Path.GetFileName(Path.TrimEndingDirectorySeparator(snapshotRoot));
			if (!snapshotId.All(char.IsDigit)) return false;
			var parent = Path.GetDirectoryName(snapshotRoot);
			return string.Equals(
				Path.TrimEndingDirectorySeparator(parent ?? string.Empty),
				Path.TrimEndingDirectorySeparator(Path.GetFullPath(userRoot)),
				StringComparison.OrdinalIgnoreCase);
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
		catch (InvalidDataException)
		{
			return false;
		}
	}

	private static IEnumerable<string> EnumerateRegularFiles(string root)
	{
		var pending = new Stack<string>();
		pending.Push(Path.GetFullPath(root));
		while (pending.Count > 0)
		{
			var directory = pending.Pop();
			foreach (var file in Directory.EnumerateFiles(directory))
			{
				if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
				yield return Path.GetFullPath(file);
			}

			foreach (var child in Directory.EnumerateDirectories(directory))
			{
				if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
				pending.Push(child);
			}
		}
	}

	private static void EnsureWithinRoot(string root, string candidate)
	{
		var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
			Path.DirectorySeparatorChar;
		var normalizedCandidate = Path.GetFullPath(candidate);
		if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("Resolved PGS path escapes its user directory.");
	}

	private static void ValidateRelativePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
			throw new InvalidDataException($"Invalid PGS relative path: {path}");
		var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0 || parts.Any(x => x is "." or ".."))
			throw new InvalidDataException($"Invalid PGS relative path: {path}");
	}
}
