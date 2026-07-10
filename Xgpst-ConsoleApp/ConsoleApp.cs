using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XgpSaveTools;
using XgpSaveTools.Extensions;
using XgpSaveTools.Records;
using static XgpSaveTools.Extensions.IoExtensions;
using static XgpSaveTools.Common.GameList;
using System.ComponentModel;
using System.Diagnostics;
using XgpSaveTools.Operations;
using XgpSaveTools.SaveHandlers;
using Xgpst_ConsoleApp.Operations;

namespace Xgpst_ConsoleApp
{
	public class ConsoleApp
	{
		private XboxContainerRepository _manager;
		private List<GameInfo> _discoveredGames;
		private GameInfo? _selectedGame;
		private UserContainerFolder? _selectedContainer;
		private ConsoleHelper _helper;

		private IEnumerable<UnregisteredGameInfo> DiscoveredUnregisteredGame => _discoveredGames.OfType<UnregisteredGameInfo>();
		private IEnumerable<GameInfo> DiscoveredSupportedGames => _discoveredGames.Where(x => x is not UnregisteredGameInfo);
		public ConsoleApp()
		{
			AppDomain.CurrentDomain.ProcessExit += (_, _) => ClearTempFolders();
		}

		private void Reset()
		{
			Console.ResetColor();
			_manager = new();
			_helper = new();
			try
			{
				_discoveredGames = DiscoverUserGames(ReadGameList()).ToList();
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
		private void ScanGamesMode() => _ScanGamesMode();
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

			var orderedList = options.OrderBy(x => x is UnregisteredGameInfo).ThenBy(x => x.Name);
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

		private void CustomPathMode()
		{
			Newline();
			string path = _helper.ReadValidDirectory("Enter wgs folder path:");
			var dir = new DirectoryInfo(path);
			if (!Directory.Exists(dir.FullName)) throw new FileNotFoundException();
			_manager.OverrideWgsPath = dir.FullName;
			Newline();
			_selectedGame = _manager.DiscoverGameInfoFromPath(dir.FullName);
			if (_selectedGame is UnregisteredGameInfo) _helper.WriteWarning($"Package '{_selectedGame.Name}' is not registered on games.json, generic handler will be used");
			SelectUserContainer();
		}

		private void SelectUserContainer()
		{
			if (_selectedGame == null) return;
			Newline();
			var containers = _manager.FindUserContainers(_selectedGame.Package).ToList();
			if (!containers.Any()) throw new Exception("No user containers found");

			var selection = _helper.SelectOption(
				containers,
				"Select user folder:",
				c => c.UserTag);

			if (selection.Key == -1) return; // Back
			_selectedContainer = selection.Value;
			SelectOperation();
		}

		private void OpenDirectory(string path)
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
			var context = _manager.CreateGameSaveContext(_selectedGame, _selectedContainer);
			var handler = GameSaveHandlerFactory.Get(_selectedGame);
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
				OpenDirectory(_selectedContainer.Dir);
				return;
			}

			ExecuteOperation(context, choice.Value.Operation);
		}

		private void ExecuteOperation(GameSaveContext context, IGameSaveOperation operation)
		{
			try
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
					ExportPlan export => executor.ExecuteExport(export),
					ImportPlan import => executor.ExecuteImport(context, import),
					_ => throw new NotSupportedException($"Unsupported operation plan: {plan.GetType().Name}")
				};

				if (result.OutputPath != null) _helper.WriteSuccess($"Files written to {result.OutputPath}");
				if (result.BackupPath != null) _helper.WriteSuccess($"Backup created at {result.BackupPath}");
				_helper.WriteSuccess($"Operation completed ({result.AffectedFiles} files).");
				_helper.WaitInput();
			}
			finally
			{
				ClearTempFolders();
			}
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
