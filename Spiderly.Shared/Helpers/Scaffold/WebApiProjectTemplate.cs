using Spiderly.Shared.Classes;
using Spiderly.Shared.Enums;
using static Spiderly.Shared.Helpers.Scaffold.TemplateComments;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The <c>{AppName}.WebAPI</c> project: its project file, settings files and host program, with its
    /// startup (<see cref="StartupTemplate"/>) and controllers (<see cref="WebApiControllersTemplate"/>).
    /// </summary>
    internal static class WebApiProjectTemplate
    {
        internal static SpiderlyFolder Folder(string appName, string spiderlyVersion, bool isRunningFromNuget, DbProviderCodes dbProvider) => new()
        {
            Name = $"{appName}.WebAPI",
            ChildFolders =
            {
                new SpiderlyFolder
                {
                    Name = "Properties",
                    Files =
                    {
                        new SpiderlyFile { Name = "launchSettings.json", Data = GetLaunchSettingsJsonData() },
                    }
                },
                WebApiControllersTemplate.Folder(appName),
                StartupTemplate.ExtensionsFolder(appName),
            },
            Files =
            {
                new SpiderlyFile { Name = "appsettings.json", Data = GetAppSettingsJsonData(appName) },
                new SpiderlyFile { Name = "appsettings.Development.json", Data = GetAppSettingsDevelopmentJsonData(appName) },
                new SpiderlyFile { Name = "appsettings.Development.local.example.json", Data = GetAppSettingsDevelopmentLocalExampleJsonData() },
                new SpiderlyFile { Name = "appsettings.Production.json", Data = GetAppSettingsProductionJsonData() },
                new SpiderlyFile { Name = $"{appName}.WebAPI.csproj", Data = GetWebAPICsProjData(appName, spiderlyVersion, isRunningFromNuget, dbProvider) },
                new SpiderlyFile { Name = $"{appName}.WebAPI.csproj.user", Data = GetWebAPICsProjUserData() },
                new SpiderlyFile { Name = "Program.cs", Data = GetProgramCsData(appName) },
                new SpiderlyFile { Name = "Settings.cs", Data = GetWebAPISettingsCsData(appName) },
                StartupTemplate.StartupFile(appName, dbProvider),
            }
        };

        private static string GetWebAPISettingsCsData(string appName)
        {
            return $$"""
namespace {{appName}}.WebAPI
{
    public class Settings
    {
        public const string ConfigurationSection = "AppSettings:{{appName}}.WebAPI";
    }
}
""";
        }

        private static string GetProgramCsData(string appName)
        {
            return $$"""
using Serilog;

namespace {{appName}}.WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host
                .CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .UseSerilog((context, configuration) =>
                {
                    configuration.ReadFrom.Configuration(context.Configuration);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
""";
        }

        private static string GetWebAPICsProjData(string appName, string spiderlyVersion, bool isRunningFromNuget, DbProviderCodes dbProvider)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk.Web">

	<PropertyGroup>
		<TargetFramework>net9.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<GenerateDocumentationFile>true</GenerateDocumentationFile>
		<NoWarn>1591</NoWarn>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.1" />
		<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.14" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.1">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="Microsoft.EntityFrameworkCore.Proxies" Version="9.0.1" />
		<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="9.0.1" />
		{{(dbProvider == DbProviderCodes.SQLServer ? "<PackageReference Include=\"Microsoft.EntityFrameworkCore.SqlServer\" Version=\"9.0.1\" />" : "<PackageReference Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"9.0.1\" />")}}
		<PackageReference Include="Hangfire.AspNetCore" Version="1.8.*" />
		{{(dbProvider == DbProviderCodes.SQLServer ? "<PackageReference Include=\"Hangfire.SqlServer\" Version=\"1.8.*\" />" : "<PackageReference Include=\"Hangfire.PostgreSql\" Version=\"1.20.*\" />")}}
		<PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.7.0" />
		<PackageReference Include="Microsoft.VisualStudio.Azure.Containers.Tools.Targets" Version="1.19.5" />
        <PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
        <PackageReference Include="Serilog.Extensions.Hosting" Version="9.0.0" />
        <PackageReference Include="Serilog.Settings.Configuration" Version="9.0.0" />
        <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
		<PackageReference Include="Swashbuckle.AspNetCore" Version="6.4.0" />
		<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.7.0" />
	</ItemGroup>

	<ItemGroup>
{{XmlCommented("""
        <ProjectReference Include="..\..\..\spiderly\Spiderly.Infrastructure\Spiderly.Infrastructure.csproj" />
        <ProjectReference Include="..\..\..\spiderly\Spiderly.Security\Spiderly.Security.csproj" />
        <ProjectReference Include="..\..\..\spiderly\Spiderly.Shared\Spiderly.Shared.csproj" />
        <ProjectReference Include="..\..\..\spiderly\Spiderly.SourceGenerators\Spiderly.SourceGenerators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
""", isRunningFromNuget)}}
		<ProjectReference Include="..\{{appName}}.Business\{{appName}}.Business.csproj" />
		<ProjectReference Include="..\{{appName}}.Infrastructure\{{appName}}.Infrastructure.csproj" />
		<ProjectReference Include="..\{{appName}}.Shared\{{appName}}.Shared.csproj" />
	</ItemGroup>

	<ItemGroup>
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Infrastructure" Version="{{spiderlyVersion}}" />
        <PackageReference Include="Spiderly.Security" Version="{{spiderlyVersion}}" />
        <PackageReference Include="Spiderly.Shared" Version="{{spiderlyVersion}}" />
        <PackageReference Include="Spiderly.SourceGenerators" Version="{{spiderlyVersion}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

</Project>
""";
        }

        private static string GetWebAPICsProjUserData()
        {
            return $$"""
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|AnyCPU'">
    <DebuggerFlavor>ProjectDebugger</DebuggerFlavor>
  </PropertyGroup>
</Project>
""";
        }

        private static string GetAppSettingsJsonData(string appName)
        {
            return $$"""
{
  "$schema": "https://raw.githubusercontent.com/filiptrivan/spiderly/main/schemas/appsettings.schema.json",
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  },
  "AppSettings": {
    "AllowedHosts": "*",
    "{{appName}}.WebAPI": {
    },
    "{{appName}}.Business": {
    },
    "Spiderly.Infrastructure": {
    },
    "Spiderly.Shared": {
      "ApplicationName": "{{appName}}",
      "ExternalProviders": [],
      "EmailSender": {
        "email": "youremail@gmail.com",
        "name": ""
      }
    },
    "Spiderly.Security": {
    }
  }
}
""";
        }

        private static string GetAppSettingsDevelopmentJsonData(string appName)
        {
            return $$"""
{
  "$schema": "https://raw.githubusercontent.com/filiptrivan/spiderly/main/schemas/appsettings.schema.json",
  "AppSettings": {
    "{{appName}}.WebAPI": {
    },
    "{{appName}}.Business": {
    },
    "Spiderly.Infrastructure": {
    },
    "Spiderly.Shared": {
      "FrontendUrl": "http://localhost:4200",
      "CookieDomain": "localhost"
    },
    "Spiderly.Security": {
    }
  }
}
""";
        }

        private static string GetAppSettingsDevelopmentLocalExampleJsonData()
        {
            return $$"""
{
  "$schema": "https://raw.githubusercontent.com/filiptrivan/spiderly/main/schemas/appsettings.schema.json",
  "_comment": "Copy this file to appsettings.Development.local.json and fill in real dev secrets. The .local.json file is gitignored.",
  "AppSettings": {
    "Spiderly.Shared": {
      "ConnectionString": "",
      "JwtKey": ""
    }
  }
}
""";
        }

        private static string GetAppSettingsProductionJsonData()
        {
            return $$"""
{
  "$schema": "https://raw.githubusercontent.com/filiptrivan/spiderly/main/schemas/appsettings.schema.json",
  "AppSettings": {
    "Spiderly.Shared": {
      // 2 = Cloudflare -> reverse-proxy -> backend; drop to 1 if only one proxy hop. See the Spiderly 'deployment' skill for TrustedProxyNetworks.
      "ForwardLimit": 2
    }
  }
}
""";
        }

        private static string GetLaunchSettingsJsonData()
        {
            return $$"""
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "launchUrl": "swagger",
      "applicationUrl": "http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "launchUrl": "swagger",
      "applicationUrl": "https://localhost:44388",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}

""";
        }
    }
}
