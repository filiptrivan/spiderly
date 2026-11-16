using Spiderly.Shared.Classes;
using Spiderly.Shared.Enums;
using static Spiderly.Shared.Helpers.Scaffold.TemplateComments;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The app's DbContext: the <c>{AppName}.Infrastructure</c> project declares it with its seed data. The
    /// <c>{AppName}.Migrations</c> project's factory builds it for the EF Core tools, so both change with its constructor.
    /// </summary>
    internal static class DbContextTemplate
    {
        internal static SpiderlyFolder InfrastructureFolder(string appName, string spiderlyVersion, bool isRunningFromNuget, DbProviderCodes dbProvider) => new()
        {
            Name = $"{appName}.Infrastructure",
            Files = GetInfrastructureFiles(appName, spiderlyVersion, isRunningFromNuget, dbProvider)
        };

        internal static SpiderlyFolder MigrationsFolder(string appName, DbProviderCodes dbProvider) => new()
        {
            Name = $"{appName}.Migrations",
            Files =
            {
                new SpiderlyFile { Name = $"{appName}.Migrations.csproj", Data = GetMigrationsCsProjData(appName, dbProvider) },
                new SpiderlyFile { Name = "MigrationsDbContextFactory.cs", Data = GetMigrationsDbContextFactoryCsData(appName, dbProvider) },
                new SpiderlyFile { Name = "Program.cs", Data = GetMigrationsProgramCsData() },
            }
        };

        private static string GetInfrastructureApplicationDbContextData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using {{appName}}.Business.Entities;
using Spiderly.Infrastructure;

namespace {{appName}}.Infrastructure
{
    public partial class {{appName}}ApplicationDbContext : ApplicationDbContext<User> // https://stackoverflow.com/questions/41829229/how-do-i-implement-dbcontext-inheritance-for-multiple-databases-in-ef7-net-co
    {
        public {{appName}}ApplicationDbContext(DbContextOptions<{{appName}}ApplicationDbContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            SeedData(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
""";
        }

        private static string GetInfrastructureApplicationDbContextSeedDataData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using {{appName}}.Business.Entities;

namespace {{appName}}.Infrastructure
{
    // Demo seed data lives in its own partial file so it can be replaced (e.g. by the e2e fixture overlay)
    // without copying the DbContext plumbing (constructor / OnModelCreating), which is what causes template drift.
    public partial class {{appName}}ApplicationDbContext
    {
        private static void SeedData(ModelBuilder modelBuilder)
        {
            Permission[] permissions =
            [
                new Permission { Id = 1, Name = "View users", Code = "ReadUser" },
                new Permission { Id = 2, Name = "Edit existing users", Code = "UpdateUser" },
                new Permission { Id = 3, Name = "Add new users", Code = "InsertUser" },
                new Permission { Id = 4, Name = "Delete users", Code = "DeleteUser" },
                new Permission { Id = 5, Name = "View roles", Code = "ReadRole" },
                new Permission { Id = 6, Name = "Edit existing roles", Code = "UpdateRole" },
                new Permission { Id = 7, Name = "Add new roles", Code = "InsertRole" },
                new Permission { Id = 8, Name = "Delete roles", Code = "DeleteRole" }
            ];

            modelBuilder.Entity<Permission>().HasData(permissions);

            DateTime seedDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<Role>().HasData(new Role
            {
                Id = 1,
                Name = "Admin",
                CreatedAt = seedDate,
                ModifiedAt = seedDate,
            });

            modelBuilder.Entity<Role>()
                .HasMany(r => r.Permissions)
                .WithMany(p => p.Roles)
                .UsingEntity(j => j.HasData(
                    new { RoleId = 1, PermissionId = 1 },
                    new { RoleId = 1, PermissionId = 2 },
                    new { RoleId = 1, PermissionId = 3 },
                    new { RoleId = 1, PermissionId = 4 },
                    new { RoleId = 1, PermissionId = 5 },
                    new { RoleId = 1, PermissionId = 6 },
                    new { RoleId = 1, PermissionId = 7 },
                    new { RoleId = 1, PermissionId = 8 }
                ));
        }
    }
}
""";
        }

        private static List<SpiderlyFile> GetInfrastructureFiles(string appName, string spiderlyVersion, bool isRunningFromNuget, DbProviderCodes dbProvider)
        {
            List<SpiderlyFile> files = new()
        {
            new SpiderlyFile { Name = $"{appName}ApplicationDbContext.cs", Data = GetInfrastructureApplicationDbContextData(appName) },
            new SpiderlyFile { Name = $"{appName}ApplicationDbContext.SeedData.cs", Data = GetInfrastructureApplicationDbContextSeedDataData(appName) },
            new SpiderlyFile { Name = $"{appName}.Infrastructure.csproj", Data = GetInfrastructureCsProjData(appName, spiderlyVersion, isRunningFromNuget, dbProvider) },
        };

            if (dbProvider == DbProviderCodes.PostgreSQL)
            {
                files.Add(new SpiderlyFile { Name = "HangfireStorageExtensions.cs", Data = GetHangfireStorageExtensionsCsData(appName) });
            }

            return files;
        }

        private static string GetHangfireStorageExtensionsCsData(string appName)
        {
            return $$"""
using Hangfire;
using Hangfire.PostgreSql;

namespace {{appName}}.Infrastructure
{
    public static class HangfireStorageExtensions
    {
        public static IGlobalConfiguration UseHangfirePostgreSqlStorage(this IGlobalConfiguration config, string connectionString)
        {
            return config.UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(connectionString)
            );
        }
    }
}
""";
        }

        private static string GetInfrastructureCsProjData(string appName, string version, bool isRunningFromNuget, DbProviderCodes dbProvider)
        {
            return $$"""
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net9.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<GenerateDocumentationFile>true</GenerateDocumentationFile>
		<NoWarn>1591</NoWarn>
	</PropertyGroup>

	<ItemGroup>
{{(dbProvider == DbProviderCodes.PostgreSQL ? "\t\t<PackageReference Include=\"Hangfire.PostgreSql\" Version=\"1.20.*\" />" : "")}}
	</ItemGroup>

	<ItemGroup>
{{XmlCommented($$"""
		<ProjectReference Include="..\..\..\spiderly\Spiderly.Infrastructure\Spiderly.Infrastructure.csproj" />
""", isRunningFromNuget)}}
		<ProjectReference Include="..\{{appName}}.Business\{{appName}}.Business.csproj" />
		<ProjectReference Include="..\{{appName}}.Shared\{{appName}}.Shared.csproj" />
	</ItemGroup>

	<ItemGroup>
        <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.1">
          <PrivateAssets>all</PrivateAssets>
          <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Infrastructure" Version="{{version}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

</Project>
""";
        }

        private static string GetMigrationsCsProjData(string appName, DbProviderCodes dbProvider)
        {
            return $$"""
<!--
	Lightweight startup project for EF Core design-time tools (migrations).
	Using this instead of WebAPI as the startup project allows running
	"spiderly add-migration" and "spiderly update-database" while the
	backend is running — WebAPI's DLLs are locked by the running process,
	but this project builds to its own output directory.
-->
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net9.0</TargetFramework>
		<OutputType>Exe</OutputType>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
	</PropertyGroup>

	<ItemGroup>
		<ProjectReference Include="..\{{appName}}.Infrastructure\{{appName}}.Infrastructure.csproj" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.1">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="Microsoft.EntityFrameworkCore.Proxies" Version="9.0.1" />
		{{(dbProvider == DbProviderCodes.SQLServer ? "<PackageReference Include=\"Microsoft.EntityFrameworkCore.SqlServer\" Version=\"9.0.1\" />" : "<PackageReference Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"9.0.1\" />")}}
		<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.1" />
		<PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="9.0.1" />
	</ItemGroup>

	<ItemGroup>
		<None Include="..\{{appName}}.WebAPI\appsettings.json" Link="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
		<None Include="..\{{appName}}.WebAPI\appsettings.*.json" Link="%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
	</ItemGroup>

</Project>
""";
        }

        private static string GetMigrationsDbContextFactoryCsData(string appName, DbProviderCodes dbProvider)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using {{appName}}.Infrastructure;

namespace {{appName}}.Migrations
{
    public class MigrationsDbContextFactory : IDesignTimeDbContextFactory<{{appName}}ApplicationDbContext>
    {
        public {{appName}}ApplicationDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .AddJsonFile("appsettings.Development.local.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string connectionString = configuration[$"{Spiderly.Shared.Settings.ConfigurationSection}:ConnectionString"];

            DbContextOptionsBuilder<{{appName}}ApplicationDbContext> optionsBuilder = new();
            optionsBuilder.UseLazyLoadingProxies();
            {{(dbProvider == DbProviderCodes.SQLServer ? "optionsBuilder.UseSqlServer(connectionString);" : "optionsBuilder.UseNpgsql(connectionString);")}}

            return new {{appName}}ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
""";
        }

        private static string GetMigrationsProgramCsData()
        {
            return $$"""
return;
""";
        }
    }
}
