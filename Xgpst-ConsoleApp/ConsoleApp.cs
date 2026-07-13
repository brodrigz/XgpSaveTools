using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XgpSaveTools;
using XgpSaveTools.Extensions;
using XgpSaveTools.Records;
using static XgpSaveTools.Common.GameList;
using System.ComponentModel;
using System.Diagnostics;
using XgpSaveTools.Operations;
using XgpSaveTools.SaveHandlers;
using XgpSaveTools.SaveSources;
using Xgpst_ConsoleApp.Operations;

namespace Xgpst_ConsoleApp
{
	public class ConsoleApp
	{
		private readonly XboxContainerRepository _manager;
		private readonly IReadOnlyList<GameInfo> _registeredGames;
		private readonly GameSaveSourceResolver _sources;
		private readonly string? _exportOutputDirectory;
		private readonly Action<string> _openDirectory;
		private List<GameInfo> _discoveredGames;
		private GameInfo? _selectedGame;
		private UserContainerFolder? _selectedContainer;
		private readonly ConsoleHelper _helper;

		public ConsoleApp(
			XboxContainerRepository? manager = null,
			ConsoleHelper? helper = null,
			IReadOnlyList<GameInfo>? registeredGames = null,
			GameSaveSourceResolver? sources = null,
			string? exportOutputDirectory = null,
			Action<string>? openDirectory = null)
		{
			_registeredGames = registeredGames ?? ReadGameList();
			_manager = manager ?? new XboxContainerRepository();
			_helper = helper ?? new ConsoleHelper();
			_sources = sources ?? new GameSaveSourceResolver(new WgsGameSaveSource(_manager));
			_exportOutputDirectory = exportOutputDirectory;
			_openDirectory = openDirectory ?? OpenDirectory;
			_discoveredGames = new List<GameInfo>();
		}

		private IEnumerable<UnregisteredGameInfo> DiscoveredUnregisteredGame => _discoveredGames.OfType<UnregisteredGameInfo>();
		private IEnumerable<GameInfo> DiscoveredSupportedGames => _discoveredGames.Where(x => x is not UnregisteredGameInfo);
		internal GameInfo? SelectedGame => _selectedGame;
		internal UserContainerFolder? SelectedContainer => _selectedContainer;

		internal void Reset()
		{
			Console.ResetColor();
			_manager.OverrideWgsPath = null;
			_selectedGame = null;
			_selectedContainer = null;
			try
			{
				_discoveredGames = DiscoverUserGames(_registeredGames, _sources).ToList();
			}
			catch
			{
				_discoveredGames = new List<GameInfo>();
			}
		}


		#region Main Navigation
		public void RunMainLoop()
		{
			try
			{
				MainLoop();
			}
			catch (Exception ex)
			{
				HandleException(ex);
				RunMainLoop();
			}
		}


		private void MainLoop()
		{
			while (true)
			{
				Reset();
				_helper.DisplayHeader("Xbox Game Pass Save Tools", 40);

				var options = new List<(string, Action)>()
				{
					("Scan Games",ScanGamesMode),
					("Enter Path",CustomPathMode),
					("Exit",Exit)
				};

				_helper.SelectOption(options, x => x.Item1, true)
					.Value.Item2.Invoke();
			}
		}
		#endregion


		#region Handlers
		internal void ScanGamesMode() => _ScanGamesMode();
		private void _ScanGamesMode(bool showUnregistered = false)
		{
			if (!_discoveredGames.Any()) throw new Exception("No games found");
			Newline();

			var options = DiscoveredSupportedGames;
			GameInfo showUnregisteredOpt = new($"Show Unregistered ({DiscoveredUnregisteredGame.Count()})", null, null, null);
			if (showUnregistered)
			{
				options = _discoveredGames;
			}
			else if (DiscoveredUnregisteredGame.Any())
			{
				options = options.Append(showUnregisteredOpt).ToList();
			}

			var orderedList = options
				.OrderBy(x => x == showUnregisteredOpt ? 2 : x is UnregisteredGameInfo ? 1 : 0)
				.ThenBy(x => x.Name);
			string getLabel(GameInfo gameInfo)
			{
				return (gameInfo is UnregisteredGameInfo) ?
					$"[Unregistered] {gameInfo.Name}" : gameInfo.Name;
			}

			var selection = _helper.SelectOption(
				orderedList.ToList(),
				"Select a game:",
				getLabel);

			if (selection.Value == null) return;
			if (selection.Value == showUnregisteredOpt)
			{
				Newline();
				_helper.WriteWarning("Unregistered games will use generic handler, and output files might have no extension, consider creating entry on games.json");
				_ScanGamesMode(true);
				return;
			}
			_selectedGame = selection.Value;
			SelectUserContainer();
		}

		internal void CustomPathMode()
		{
			Newline();
			string path = _helper.ReadValidDirectory("Enter wgs or PGS folder path:");
			var dir = new DirectoryInfo(path);
			if (!Directory.Exists(dir.FullName)) throw new FileNotFoundException();

			var pgsCandidates = _registeredGames
				.Where(x => string.Equals(x.Source, "pgs", StringComparison.OrdinalIgnoreCase))
				.SelectMany(game => _sources.Resolve(game)
					.FindUserContainers(game)
					.Where(location => PathsOverlap(dir.FullName, location.Dir))
					.Select(location => (Game: game, Location: location)))
				.ToList();
			if (pgsCandidates.Count > 0)
			{
				var selected = pgsCandidates.Count == 1
					? pgsCandidates[0]
					: _helper.SelectOption(
						pgsCandidates,
						"Select PGS save:",
						x => $"{x.Game.Name} - {x.Location.UserTag}").Value;
				_selectedGame = selected.Game;
				_selectedContainer = selected.Location;
				SelectOperation();
				return;
			}

			_manager.OverrideWgsPath = dir.FullName;
			Newline();
			_selectedGame = _manager.DiscoverGameInfoFromPath(dir.FullName, _registeredGames);
			if (_selectedGame is UnregisteredGameInfo) _helper.WriteWarning($"Package '{_selectedGame.Name}' is not registered on games.json, generic handler will be used");
			SelectUserContainer();
		}

		private static bool PathsOverlap(string first, string second)
		{
			static string Normalize(string value) =>
				Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
			var a = Normalize(first);
			var b = Normalize(second);
			return a.StartsWith(b, StringComparison.OrdinalIgnoreCase) ||
				b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
		}

		private void SelectUserContainer()
		{
			if (_selectedGame == null) return;
			Newline();
			var source = _sources.Resolve(_selectedGame);
			var containers = source.FindUserContainers(_selectedGame).ToList();
			if (!containers.Any()) throw new Exception("No user containers found");

			var selection = _helper.SelectOption(
				containers,
				"Select user folder:",
				c => c.UserTag);

			if (selection.Key == -1) return; // Back
			_selectedContainer = selection.Value;
			SelectOperation();
		}

		private static void OpenDirectory(string path)
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = path,
				UseShellExecute = true
			});
		}

		private void SelectOperation()
		{
			if (_selectedGame == null || _selectedContainer == null) return;
			var source = _sources.Resolve(_selectedGame);
			var context = source.CreateContext(_selectedGame, _selectedContainer);
			var handler = GameSaveHandlerRegistry.Resolve(_selectedGame);
			var menu = handler.GetOperations(context)
				.Select(x => new OperationMenuItem(x.Definition.DisplayName, x))
				.Append(new OperationMenuItem("Open Directory", null))
				.ToList();

			Newline();
			var choice = _helper.SelectOption(
				menu,
				"Select operation:",
				item => item.Label);
			if (choice.Key == -1 || choice.Value == null) return;
			Newline();

			if (choice.Value.Operation == null)
			{
				_openDirectory(_selectedContainer.Dir);
				return;
			}

			ExecuteOperation(context, choice.Value.Operation);
		}

		private void ExecuteOperation(GameSaveContext context, IGameSaveOperation operation)
		{
			var parameters = operation.GetParameters(context);
			var inputProvider = new ConsoleOperationInputProvider(_helper);
			var arguments = inputProvider.CollectAsync(parameters, CancellationToken.None).GetAwaiter().GetResult();
			if (arguments == null) return;

			using var workspace = new TempWorkspace();
			var plan = operation.PrepareAsync(context, arguments, workspace, CancellationToken.None).GetAwaiter().GetResult();
			var presenter = new ConsoleOperationPresenter(_helper);
			if (!presenter.PresentAndConfirm(plan)) return;

			var executor = new OperationExecutor(_manager);
			var result = plan switch
			{
				ExportPlan export => executor.ExecuteExport(export, _exportOutputDirectory),
				ImportPlan import => executor.ExecuteImport(context, import),
				_ => throw new NotSupportedException($"Unsupported operation plan: {plan.GetType().Name}")
			};

			if (result.OutputPath != null) _helper.WriteSuccess($"Files written to {result.OutputPath}");
			if (result.BackupPath != null) _helper.WriteSuccess($"Backup created at {result.BackupPath}");
			_helper.WriteSuccess($"Operation completed ({result.AffectedFiles} files).");
			_helper.WaitInput();
		}
		#endregion

		private sealed record OperationMenuItem(string Label, IGameSaveOperation? Operation);

		private void HandleException(Exception ex)
		{
			_helper.WriteError(ex?.Message ?? "Unknown error");
			_helper.WaitInput();
		}

		private void Newline() => Console.Write("\n");

		private void Exit()
		{
			Environment.Exit(0);
		}
	}
}
