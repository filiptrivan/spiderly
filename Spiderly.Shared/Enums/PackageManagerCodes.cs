namespace Spiderly.Shared.Enums
{
    public enum PackageManagerCodes
    {
        Npm,
        Pnpm,
        Yarn,
        Bun
    }

    public static class PackageManagerCodesExtensions
    {
        public static string GetCommandName(this PackageManagerCodes packageManager)
        {
            return packageManager switch
            {
                PackageManagerCodes.Pnpm => "pnpm",
                PackageManagerCodes.Yarn => "yarn",
                PackageManagerCodes.Bun => "bun",
                PackageManagerCodes.Npm => "npm",
                _ => throw new ArgumentOutOfRangeException(nameof(packageManager), packageManager, null)
            };
        }
    }
}
