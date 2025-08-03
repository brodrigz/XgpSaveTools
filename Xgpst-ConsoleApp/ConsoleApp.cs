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
				_helper.WriteWarning("Unregistered games will use generic handler, and output files will have no extension, consider creating entry on games.json");
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
			int result = -1;
			if (_selectedGame == null || _selectedContainer == null) return;
			Newline();
			var choice = _helper.SelectOption(
				new[] { "Extract Files", "Replace/Delete Entries", "Open Directory" },
				"Select operation:",
				item => item).Key;
			Newline();
			switch (choice)
			{
				case 0:
					result = _manager.Extract(_selectedGame, _selectedContainer);
					if (result > 0) _helper.WriteSuccess($"{result} files extracted");
					_helper.WaitInput();
					break;
				case 1:
					//result = HandleEditMode();
					var editResult = HandleEditMode();
					if (editResult != null)
					{
						if (editResult.Removed > 0) _helper.WriteSuccess($"{editResult.Removed} entries removed");
						Newline();
						if (editResult.Replaced > 0) _helper.WriteSuccess($"{editResult.Replaced} entries replaced");
					}
					_helper.WaitInput();
					break;
				case 2:
					OpenDirectory(_selectedContainer.Dir);
					break;
				case -1: // Back
					return;
			}
		}
		#endregion


		public record EditOperationResult(int Replaced, int Removed);

		private EditOperationResult? HandleEditMode()
		{
			if (_selectedGame == null || _selectedContainer == null) return null;
			Newline();

			var entries = _manager.GetSaveEntries(_selectedGame, _selectedContainer).ToList();
			if (!entries.Any()) throw new Exception("No save entries found");

			var replacements = new Dictionary<int, EntryReplacement>();
			while (true)
			{

				var finishOpt = new SaveFile("Finish", ContainerEntry: null);
				var selection = _helper.SelectOption(
						 entries.Append(finishOpt).ToList(),
					"Select an entry (or 'Finish' to continue):",
					e => GetReplacementOptionLabel(e, replacements));

				if (selection.Value == finishOpt) break; // finished selection
				if (selection.Key == -1) return null;
				var result = CreateEntryReplacement(selection.Value!);
				Newline();
				if (result == null) continue;
				replacements[selection.Key] = result;
			}

			if (replacements.Any())
			{
				_manager.ReplaceEntries(_selectedGame, _selectedContainer, replacements.Values);
				return new EditOperationResult(replacements.Where(x => x.Value?.ReplacementFile != null).Count(), replacements.Where(x => x.Value?.ReplacementFile == null).Count());
			}
			else return null;
		}

		private string GetReplacementOptionLabel(SaveFile entry, Dictionary<int, EntryReplacement> dict)
		{
			if (entry.ContainerEntry == null) return "Finish";
			string lbl = "";
			var existingEntry = dict.Values.FirstOrDefault(x => x.TargetFile == entry.ContainerEntry);
			if (existingEntry != null)
			{
				lbl = (existingEntry.ReplacementFile == null ? "[D]" : "[R]");
			}
			return $"{entry.OutputName} ({entry.GetReadableFileSize()}) {lbl}";
		}

		private EntryReplacement? CreateEntryReplacement(SaveFile entry)
		{
			Newline();
			var option = _helper.SelectOption(new[] { "Replace", "Delete" });
			switch (option.Key)
			{
				case 0:
					var file = _helper.ReadValidFile("Enter replacement file path:");
					return new EntryReplacement(entry.ContainerEntry, file);
				case 1:
					return new EntryReplacement(entry.ContainerEntry, null);
				default:
					return null;
			}
		}

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
