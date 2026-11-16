using Spiderly.Shared.Classes;
using static Spiderly.Shared.Helpers.Scaffold.TemplateComments;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The <c>{AppName}.Business</c> project: the entities, the services that override generated ones, the
    /// permission codes and the mapper.
    /// </summary>
    internal static class BusinessProjectTemplate
    {
        internal static SpiderlyFolder Folder(string appName, string spiderlyVersion, bool isRunningFromNuget) => new()
        {
            Name = $"{appName}.Business",
            ChildFolders =
            {
                new SpiderlyFolder
                {
                    Name = "DataMappers",
                    Files = new List<SpiderlyFile>
                    {
                        new SpiderlyFile { Name = "MapsterMapper.cs", Data = GetMapsterMapperCsData(appName) },
                    }
                },
                new SpiderlyFolder
                {
                    Name = "Entities",
                    Files = EntitiesTemplate.Files(appName)
                },
                new SpiderlyFolder
                {
                    Name = "Enums",
                    Files =
                    {
                        new SpiderlyFile { Name = "PermissionCodes.cs", Data = GetPermissionCodesCsData(appName) },
                    }
                },
                new SpiderlyFolder
                {
                    Name = "Services",
                    Files =
                    {
                        new SpiderlyFile { Name = $"AuthorizationService.cs", Data = GetAuthorizationServiceCsData(appName) },
                        new SpiderlyFile { Name = $"SecurityService.cs", Data = GetSecurityServiceCsData(appName) },
                        new SpiderlyFile { Name = $"OutboxMessageService.cs", Data = GetOutboxMessageServiceCsData(appName) },
                    }
                },
            },
            Files =
            {
                new SpiderlyFile { Name = $"{appName}.Business.csproj", Data = GetBusinessCsProjData(appName, spiderlyVersion, isRunningFromNuget) },
                new SpiderlyFile { Name = $"Settings.cs", Data = GetBusinessSettingsCsData(appName) },
            }
        };

        private static string GetPermissionCodesCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes;

namespace {{appName}}.Business.Enums
{
    [SpiderlyEnum]
    public static partial class PermissionCodes
    {

    }
}
""";
        }

        private static string GetOutboxMessageServiceCsData(string appName)
        {
            return $$"""
using {{appName}}.Business.Entities;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Classes;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Extensions;

namespace {{appName}}.Business.Services
{
    /// <summary>
    /// Default-filters the outbox admin table to pending rows (DispatchedAt IS NULL) so the page surfaces
    /// stuck/failing events without an explicit filter. To inspect dispatched history, add an explicit
    /// DispatchedAt filter in the table UI — its presence in the FilterDTO suppresses this default.
    /// </summary>
    [SpiderlyService]
    public class OutboxMessageService : OutboxMessageServiceGenerated
    {
        public OutboxMessageService(EntityServiceDependencies deps) : base(deps) { }

        public override async Task<PaginatedResult<OutboxMessage>> GetPaginatedOutboxMessageResult(
            FilterDTO filterDTO, IQueryable<OutboxMessage> query)
        {
            bool hasExplicitDispatchedFilter = filterDTO.Filters != null
                && filterDTO.Filters.ContainsKey(nameof(OutboxMessage.DispatchedAt).FirstCharToLower());

            if (!hasExplicitDispatchedFilter)
                query = query.Where(x => x.DispatchedAt == null);

            return await base.GetPaginatedOutboxMessageResult(filterDTO, query);
        }
    }
}
""";
        }

        private static string GetBusinessSettingsCsData(string appName)
        {
            return $$"""
namespace {{appName}}.Business
{
    public class Settings
    {
        public const string ConfigurationSection = "AppSettings:{{appName}}.Business";
    }
}
""";
        }

        private static string GetBusinessCsProjData(string appName, string version, bool isRunningFromNuget)
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
{{XmlCommented($$"""
    <ProjectReference Include="..\..\..\spiderly\Spiderly.Security\Spiderly.Security.csproj" />
    <ProjectReference Include="..\..\..\spiderly\Spiderly.SourceGenerators\Spiderly.SourceGenerators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
""", isRunningFromNuget)}}
    <ProjectReference Include="..\{{appName}}.Shared\{{appName}}.Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Folder Include="DataMappers\" />
    <Folder Include="Entities\" />
    <Folder Include="Enums\" />
    <Folder Include="Services\" />
  </ItemGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

	<ItemGroup>
{{XmlCommented($$"""
        <PackageReference Include="Spiderly.Security" Version="{{version}}" />
        <PackageReference Include="Spiderly.SourceGenerators" Version="{{version}}" />
""", !isRunningFromNuget)}}
	</ItemGroup>

</Project>
""";
        }

        private static string GetAuthorizationServiceCsData(string appName)
        {
            return $$"""
using Microsoft.Extensions.Localization;
using {{appName}}.Business.DTO;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Enums;
using Spiderly.Security.Services;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;

namespace {{appName}}.Business.Services
{
    [SpiderlyService]
    public class AuthorizationService : AuthorizationServiceGenerated
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;
        private readonly SecurityService<User, UserExternalLogin> _securityService;

        public AuthorizationService(
            IApplicationDbContext context,
            AuthenticationService authenticationService,
            SecurityService<User, UserExternalLogin> securityService,
            IStringLocalizer localizer,
            IPrincipalRegistry principalRegistry
        )
            : base(context, authenticationService, localizer, principalRegistry)
        {
            _context = context;
            _authenticationService = authenticationService;
            _securityService = securityService;
        }

        #region User

        public override async Task AuthorizeUserUpdateAndThrow(UserDTO userDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                User user = await GetInstanceAsync<User, long>(userDTO.Id, null);

                if (user.Email != userDTO.Email)
                    throw new SecurityViolationException($"No one can change {nameof(userDTO.Email)} from the main UI form.");

                bool hasAdminUpdatePermission = await IsAuthorizedAsync(PermissionCodes.UpdateUser);
                if (hasAdminUpdatePermission)
                    return;

                long currentUserId = _authenticationService.GetCurrentUserId();
                if (currentUserId != userDTO.Id)
                    throw new SecurityViolationException($"User without admin update permission which is not current user tryed to update user.");

                if (userDTO.IsDisabled != user.IsDisabled)
                    throw new SecurityViolationException($"User without admin update permission tryed to change {nameof(userDTO.IsDisabled)}.");
            });
        }

        public override async Task AuthorizeUserInsertAndThrow(UserDTO userDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                bool hasAdminInsertPermission = await IsAuthorizedAsync(PermissionCodes.InsertUser);
                if (hasAdminInsertPermission)
                    return;

                throw new SecurityViolationException("User without admin insert permission tryed to add new user.");
            });
        }

        #endregion

    }
}
""";
        }

        private static string GetSecurityServiceCsData(string appName)
        {
            return $$"""
using {{appName}}.Business.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Spiderly.Security;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.ExternalAuth;
using Spiderly.Shared.Interfaces;

namespace {{appName}}.Business.Services
{
    public class SecurityService<TUser, TUserExternalLogin> : SecurityServiceBase<TUser, TUserExternalLogin>
        where TUser : class, IUser, new()
        where TUserExternalLogin : class, IUserExternalLogin, new()
    {
        private readonly IApplicationDbContext _context;

        public SecurityService(
            IApplicationDbContext context,
            IJwtAuthManager jwtAuthManagerService,
            IEmailingService emailingService,
            AuthenticationService authenticationService,
            IWebHostEnvironment environment,
            IStringLocalizer localizer,
            IOptions<AuthPolicyOptions> authPolicyOptions,
            IExternalAuthProviderRegistry externalAuthProviderRegistry,
            ExternalAuthCodeFlow externalAuthCodeFlow,
            IDataProtectionProvider dataProtectionProvider,
            IOptions<Spiderly.Shared.Settings> sharedSettings,
            TimeProvider timeProvider
        )
            : base(context, jwtAuthManagerService, emailingService, authenticationService, environment, localizer, authPolicyOptions, externalAuthProviderRegistry, externalAuthCodeFlow, dataProtectionProvider, sharedSettings, timeProvider)
        {
            _context = context;
        }

        /// <summary>
        /// Assigns admin role to the first user in the app. 
        /// This is a performance bottleneck.
        /// Delete this method once the first user has admin permissions.
        /// </summary>
        public override async Task OnAfterLogin(AuthResultDTO authResultDTO)
        {
            bool isFirstUserEver = await _context.DbSet<User>().CountAsync() == 1;
            if (isFirstUserEver)
            {
                Role? adminRole = await _context.DbSet<Role>().FirstOrDefaultAsync(x => x.Name == "Admin");
                if (adminRole != null)
                {
                    User? user = await _context.DbSet<User>().FirstOrDefaultAsync(x => x.Id == authResultDTO.UserId);
                    if (user != null && !user.Roles.Any())
                    {
                        user.Roles.Add(adminRole);
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }

    }
}
""";
        }

        private static string GetMapsterMapperCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes;

namespace {{appName}}.Business.DataMappers
{
    [SpiderlyDataMapper]
    public static partial class Mapper
    {

    }
}
""";
        }
    }
}
