using XgpSaveTools.Operations;
using XgpSaveTools.Records;
using XgpSaveTools.Extensions;

namespace XgpSaveTools.SaveSources;

public sealed class WgsGameSaveSource : IGameSaveSource
{
	private readonly XboxContainerRepository _repository;

	public WgsGameSaveSource(XboxContainerRepository? repository = null)
	{
		_repository = repository ?? new XboxContainerRepository();
	}

	public string Id => "wgs";

	public IEnumerable<string> EnumeratePackageNames()
	{
		var root = new DirectoryInfo(_repository.PackagesRoot);
		if (!root.Exists) yield break;
		foreach (var wgsDir in root.GetDirectories("wgs", SearchOption.AllDirectories))
		{
			var packageName = wgsDir.Parent?.Parent?.Name;
			if (!string.IsNullOrWhiteSpace(packageName)) yield return packageName;
		}
	}

	public IReadOnlyList<UserContainerFolder> FindUserContainers(GameInfo game) =>
		_repository.FindUserContainers(game.Package)
			.Select(x => x with { Source = Id })
			.ToList();

	public GameSaveContext CreateContext(GameInfo game, UserContainerFolder location) =>
		_repository.CreateGameSaveContext(game, location with { Source = Id });
}
