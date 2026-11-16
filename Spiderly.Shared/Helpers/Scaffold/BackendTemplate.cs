using Spiderly.Shared.Classes;
using Spiderly.Shared.Enums;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The <c>Backend/</c> folder: the .NET solution, its local tool manifest, and its five projects.
    /// </summary>
    internal static class BackendTemplate
    {
        internal static SpiderlyFolder Folder(ProjectGenerationOptions options)
        {
            string appName = options.AppName;
            string spiderlyVersion = options.SpiderlyVersion;
            bool isRunningFromNuget = options.IsRunningFromNuget;
            DbProviderCodes dbProvider = options.DbProvider;

            return new SpiderlyFolder
            {
                Name = "Backend",
                ChildFolders =
                {
                    new SpiderlyFolder
                    {
                        Name = ".config",
                        Files =
                        {
                            new SpiderlyFile { Name = "dotnet-tools.json", Data = GetDotnetToolsJsonData(spiderlyVersion) },
                        }
                    },
                    BusinessProjectTemplate.Folder(appName, spiderlyVersion, isRunningFromNuget),
                    DatabaseProjectsTemplate.InfrastructureFolder(appName, spiderlyVersion, isRunningFromNuget, dbProvider),
                    DatabaseProjectsTemplate.MigrationsFolder(appName, dbProvider),
                    SharedProjectTemplate.Folder(appName, spiderlyVersion, isRunningFromNuget),
                    WebApiProjectTemplate.Folder(appName, spiderlyVersion, isRunningFromNuget, dbProvider),
                },
                Files =
                {
                    new SpiderlyFile { Name = $"{appName}.sln", Data = GetNetSolutionData(appName) }
                }
            };
        }

        private static string GetNetSolutionData(string appName)
        {
            return $$"""
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.8.34525.116
MinimumVisualStudioVersion = 10.0.40219.1
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.WebAPI", "{{appName}}.WebAPI\{{appName}}.WebAPI.csproj", "{1063DCDA-9291-4FAA-87B2-555E12511EE2}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.Infrastructure", "{{appName}}.Infrastructure\{{appName}}.Infrastructure.csproj", "{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.Business", "{{appName}}.Business\{{appName}}.Business.csproj", "{50AD9ADA-4E90-4E69-97BB-92FA455115DE}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.Shared", "{{appName}}.Shared\{{appName}}.Shared.csproj", "{2D65E133-33C4-4169-A175-D744800941D6}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{appName}}.Migrations", "{{appName}}.Migrations\{{appName}}.Migrations.csproj", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{1063DCDA-9291-4FAA-87B2-555E12511EE2}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{1063DCDA-9291-4FAA-87B2-555E12511EE2}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{1063DCDA-9291-4FAA-87B2-555E12511EE2}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{1063DCDA-9291-4FAA-87B2-555E12511EE2}.Release|Any CPU.Build.0 = Release|Any CPU
		{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{8E0E2A3B-7A46-452E-9695-80E2BB1F4E9C}.Release|Any CPU.Build.0 = Release|Any CPU
		{50AD9ADA-4E90-4E69-97BB-92FA455115DE}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{50AD9ADA-4E90-4E69-97BB-92FA455115DE}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{50AD9ADA-4E90-4E69-97BB-92FA455115DE}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{50AD9ADA-4E90-4E69-97BB-92FA455115DE}.Release|Any CPU.Build.0 = Release|Any CPU
		{2D65E133-33C4-4169-A175-D744800941D6}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{2D65E133-33C4-4169-A175-D744800941D6}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{2D65E133-33C4-4169-A175-D744800941D6}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{2D65E133-33C4-4169-A175-D744800941D6}.Release|Any CPU.Build.0 = Release|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {173A0B43-6F68-4847-ABBF-97106E9B08E6}
	EndGlobalSection
EndGlobal
""";
        }

        private static string GetDotnetToolsJsonData(string spiderlyVersion)
        {
            return $$"""
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "9.0.1",
      "commands": [
        "dotnet-ef"
      ]
    },
    "Spiderly.CLI": {
      "version": "{{spiderlyVersion}}",
      "commands": [
        "spiderly"
      ]
    }
  }
}
""";
        }
    }
}
