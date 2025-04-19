using System;
using XgpSaveTools;
using XgpSaveTools.Records;
using static XgpSaveTools.Extensions.IoExtensions;
using static XgpSaveTools.Common.GameList;

namespace Xgpst_ConsoleApp
{
	internal class Program
	{
		static SaveManager manager;
		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.ProcessExit += (_, _) => XgpSaveTools.Extensions.IoExtensions.ClearTempFolders();
			manager = new SaveManager();
			Console.WriteLine("Xbox Game Pass save extractor");
			Console.WriteLine(new string('=', 40));
			List<GameInfo> gameList;

			try
			{
				// Attempt to read game list
				gameList = ReadGameList();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Failed to read game list. Check that games.json exists and is valid.");
				Console.WriteLine();
				Console.WriteLine("Press enter to quit");
				Console.ReadLine();
				return;
			}

			// Discover games
			var found = DiscoverGames(gameList).ToList();
			if (!found.Any())
			{
				Console.WriteLine("No supported games installed");
				Console.WriteLine();
				Console.WriteLine("Press enter to quit");
				Console.ReadLine();
				return;
			}

			// User selects game
			Console.WriteLine("Installed supported games:");
			for (int i = 0; i < found.Count; i++)
			{
				Console.WriteLine($"{i + 1}) {found[i].Name}");
			}
			Console.WriteLine("Enter number:");
			if (!int.TryParse(Console.ReadLine(), out int input)) throw new ArgumentException("Invalid input");
			var selectedGame = found[input - 1];
			Console.WriteLine("");

			// Discover user containers
			var userContainers = manager.FindUserContainers(selectedGame.Package).ToList();
			Console.WriteLine("Found user folders:");
			for (int i = 0; i < userContainers.Count; i++)
			{
				Console.WriteLine($"{i + 1}) {userContainers[i].UserTag}");
			}
			Console.WriteLine("Enter number:");
			if (!int.TryParse(Console.ReadLine(), out input)) throw new ArgumentException("Invalid input");
			var selectedUserContainer = userContainers[input - 1];
			Console.WriteLine("");

			// User selects mode
			Console.WriteLine("Select operation:");
			Console.WriteLine("1) Extract Files");
			Console.WriteLine("2) Replace entry");
			if (!int.TryParse(Console.ReadLine(), out input)) throw new ArgumentException("Invalid input");
			Console.WriteLine("");

			// Action 
			switch (input)
			{
				case 1: manager.Extract(selectedGame, selectedUserContainer); break;
				case 2: ReplaceEntryMode(selectedGame, selectedUserContainer); break;
				default: throw new NotImplementedException();
			}

			Console.WriteLine("");
			Console.WriteLine("Done");
			Console.ReadLine();
		}

		private static EntryReplacement CreateEntryReplacement(SaveFile selectedEntry)
		{
			Console.WriteLine("Enter replacement file path:");
			var file = new FileInfo(Console.ReadLine()!.Unquote());
			if (!File.Exists(file.FullName)) throw new FileNotFoundException();
			Console.WriteLine("");

			return new EntryReplacement(selectedEntry.ContainerEntry, file);
		}

		private static void ReplaceEntryMode(GameInfo gameInfo, UserContainerFolder userContainer)
		{
			Dictionary<int, EntryReplacement> replacements = new();

			Console.WriteLine("Select entry to replace, or type 'OK' to finish:");
			// List entries
			var entries = manager.GetSaveEntries(gameInfo, userContainer).ToList();
			if (!entries.Any())
			{
				Console.WriteLine($"No entries found for {userContainer.UserTag}");
				return;
			}
			for (int i = 0; i < entries.Count; i++)
			{
				Console.WriteLine($"{i + 1}) {entries[i].OutputName} ({entries[i].GetReadableFileSize()})");
			}
			Console.WriteLine("Input option:");

			while (int.TryParse(Console.ReadLine(), out int input))
			{
				Console.Clear();
				replacements[input] = CreateEntryReplacement(entries[input - 1]);
				Console.WriteLine("Select one more entry to replace, or type 'OK' to finish:");
				for (int i = 0; i < entries.Count; i++)
				{
					var entry = entries[i];
					Console.WriteLine($"{i + 1}) {entry.OutputName} ({entry.GetReadableFileSize()}) {(replacements.ContainsKey(i + 1) ? "[R]" : "")}");
				}
			}

			manager.ReplaceEntries(gameInfo, userContainer, replacements.Values);
		}

		private static void AddEntryMode(GameInfo gameInfo, UserContainerFolder userContainer)
		{
			Console.WriteLine("Enter replacement file path:");
			string path = Console.ReadLine()!;
			if (!File.Exists(path)) throw new FileNotFoundException();

			//if (!int.TryParse(Console.ReadLine(), out input)) throw new ArgumentException("Invalid input");
		}
	}
}
