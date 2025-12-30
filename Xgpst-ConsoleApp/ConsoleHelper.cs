using XgpSaveTools.Extensions;

namespace Xgpst_ConsoleApp
{
    public class ConsoleHelper
    {
        public KeyValuePair<int, T?> SelectOption<T>(IList<T> options, Func<T, string>? getLabelFunc = null, bool disableGoBack = false) => SelectOption(options, "Select Option", getLabelFunc, disableGoBack);

        public KeyValuePair<int, T?> SelectOption<T>(
        IList<T> options,
        string prompt,
        Func<T, string>? getLabelFunc = null, bool disableGoBack = false)
        {
            getLabelFunc ??= ((x) => x.ToString());

            Console.WriteLine(prompt);
            for (int i = 0; i < options.Count; i++)
            {
                Console.WriteLine($"{i + 1}) {getLabelFunc(options[i])}");
            }

            while (true)
            {
                Console.Write($"Enter selection{(disableGoBack ? "" : "(or '0' to go back)")}: ");
                var input = Console.ReadLine();

                if (input == "0" && !disableGoBack) return new KeyValuePair<int, T?>(-1, default);

                if (int.TryParse(input, out int result))
                {
                    if (result > 0 && result <= options.Count)
                    {
                        return new KeyValuePair<int, T>(result - 1, options[result - 1]);
                    }
                }
                WriteError("Invalid selection");
            }
        }

        public void DisplayHeader(string title, int width)
        {
            Console.Clear();
            var separator = new string('=', width);
            Console.WriteLine(separator);
            Console.WriteLine(title);
            Console.WriteLine(separator);
            Console.WriteLine();
        }

        public FileInfo ReadValidFile(string prompt)
        {
            while (true)
            {
                Console.Write(prompt + " ");
                var path = Console.ReadLine()?.Unquote();
                if (File.Exists(path)) return new FileInfo(path);
                WriteError("File not found");
            }
        }

        public string ReadValidDirectory(string prompt)
        {
            while (true)
            {
                Console.Write(prompt + " ");
                var path = Console.ReadLine()?.Unquote();
                if (Directory.Exists(path)) return path;
                WriteError("Directory not found");
            }
        }

        public void WriteError(string message)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[!] {message}");
            Console.ForegroundColor = originalColor;
        }

        public void WriteSuccess(string message)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] {message}");
            Console.ForegroundColor = originalColor;
        }

        public void WriteWarning(string message)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING]: {message}");
            Console.ForegroundColor = originalColor;
        }

        public void WaitInput()
        {
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        public void WaitForExit(int code = 0)
        {
            WaitInput();
            Environment.Exit(code);
        }
    }
}