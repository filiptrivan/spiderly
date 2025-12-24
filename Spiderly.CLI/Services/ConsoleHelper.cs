namespace Spiderly.CLI.Services
{
    internal static class ConsoleHelper
    {
        public static bool PromptYesNo(string message)
        {
            Console.Write(message);
            string response = Console.ReadLine()?.ToLower();
            return response == "y" || response == "yes" || response == "Yes";
        }
    }
}
