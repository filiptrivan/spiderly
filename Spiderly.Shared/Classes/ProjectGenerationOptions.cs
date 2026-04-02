using Spiderly.Shared.Enums;

namespace Spiderly.Shared.Classes
{
    public class ProjectGenerationOptions
    {
        public string AppName { get; set; }
        public string SpiderlyVersion { get; set; }
        public bool IsRunningFromNuget { get; set; }
        public DbProviderCodes DbProvider { get; set; } = DbProviderCodes.PostgreSQL;
        public PackageManagerCodes PackageManager { get; set; } = PackageManagerCodes.Npm;
    }
}
