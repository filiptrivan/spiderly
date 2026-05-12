//HintName: EntityServiceDependencies.generated.cs
using Microsoft.Extensions.Localization;
using Spiderly.Security.Services;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Interfaces;
using TestApp.Business.Services;

namespace TestApp.Business.Services
{
    /// <summary>
    /// Bundles framework-level dependencies shared by all entity services.
    /// Add custom dependencies to your entity service constructor instead of modifying this class.
    /// </summary>
    public class EntityServiceDependencies
    {
        public IApplicationDbContext Context { get; }
        public ExcelService ExcelService { get; }
        public AuthorizationServiceGenerated AuthorizationService { get; }
        public IStringLocalizer Localizer { get; }
        public IServiceProvider ServiceProvider { get; }

        public EntityServiceDependencies(
            IApplicationDbContext context,
            ExcelService excelService,
            AuthorizationServiceGenerated authorizationService,
            IStringLocalizer localizer,
            IServiceProvider serviceProvider)
        {
            Context = context;
            ExcelService = excelService;
            AuthorizationService = authorizationService;
            Localizer = localizer;
            ServiceProvider = serviceProvider;
        }
    }
}