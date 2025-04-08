using Microsoft.Extensions.Configuration;
using Spider.Shared;
using Spider.Shared.Helpers;
using System.Data.SqlClient;
using System.Reflection;

namespace Spider.ProjectInitializer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            string initType = config["init"];
            string primaryColor = config["primary-color"];
            string appName = config["app-name"];
            string currentPath = Environment.CurrentDirectory;

            if (initType != null)
            {
                if (appName == null)
                    throw new Exception("You need to define app-name");

                if (initType == "default")
                {
                    NetAndAngularStructureGenerator.Generate(currentPath, appName, primaryColor);
                }
            }
        }
    }
}