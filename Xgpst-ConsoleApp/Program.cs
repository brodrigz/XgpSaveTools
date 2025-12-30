namespace Xgpst_ConsoleApp
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.InputEncoding = System.Text.Encoding.UTF8;
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            ConsoleApp app = new();
            app.RunMainLoop();
        }
    }
}