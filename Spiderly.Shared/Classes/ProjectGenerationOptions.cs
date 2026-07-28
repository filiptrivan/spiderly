using Spiderly.Shared.Enums;

namespace Spiderly.Shared.Classes
{
    public class ProjectGenerationOptions
    {
        public string AppName { get; set; } = null!; // Always set by the CLI before generation runs
        public string SpiderlyVersion { get; set; } = null!; // Always set by the CLI before generation runs
        public bool IsRunningFromNuget { get; set; }
        public DbProviderCodes DbProvider { get; set; } = DbProviderCodes.PostgreSQL;
        public PackageManagerCodes PackageManager { get; set; } = PackageManagerCodes.Npm;
    }
}
