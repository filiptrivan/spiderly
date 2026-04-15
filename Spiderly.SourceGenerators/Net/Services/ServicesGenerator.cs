using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Generates per-entity service classes (`{Entity}Service.generated.cs`),
    /// the `EntityServiceDependencies` class, and the `EntityServiceRegistration` class
    /// within the `{YourBaseNamespace}.Services` namespace.
    /// </summary>
    [Generator]
    public class ServicesGenerator : IIncrementalGenerator
    {

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var combined = PipelineFactory.CreatePipeline(context,
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities, NamespaceExtensionCodes.Services },
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities });

            context.RegisterSafeImplementationSourceOutput(combined, static (spc, source) =>
            {
                var ((classes, referencedClasses), config) = source;
                Execute(classes, referencedClasses, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, SpiderlyConfig config, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(ServicesGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectEntities);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            List<SpiderlyClass> userEntityServices = currentProjectClasses
                .Where(x => x.Namespace.EndsWith(".Services"))
                .Where(x => x.BaseType != null && x.BaseType.EndsWith("ServiceGenerated"))
                .ToList();

            if (currentProjectEntities.Count == 0)
                return;

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            // Generate EntityServiceDependencies
            context.AddSource("EntityServiceDependencies.generated", SourceText.From(
                GetEntityServiceDependencies(basePartOfNamespace), Encoding.UTF8));

            // Generate one service file per entity
            foreach (SpiderlyClass entity in currentProjectEntities)
            {
                string entityServiceCode = GetEntityServiceClass(entity, allEntities, basePartOfNamespace);
                context.AddSource($"{entity.Name}Service.generated", SourceText.From(entityServiceCode, Encoding.UTF8));
            }

            // Generate DI registration
            context.AddSource("EntityServiceRegistration.generated", SourceText.From(
                GetEntityServiceRegistration(currentProjectEntities, userEntityServices, basePartOfNamespace), Encoding.UTF8));
        }

        #region EntityServiceDependencies

        // NOTE: EntityServiceDependencies must be source-generated (not in Spiderly.Shared) because it
        // references AuthorizationServiceGenerated, which is generated per project with entity-specific methods.
        // We intentionally use IServiceProvider here (service locator) — it keeps this class universal across
        // all entities, avoids per-entity deps classes, and sidesteps circular dependency issues without Lazy<T>.
        private static string GetEntityServiceDependencies(string basePartOfNamespace)
        {
            return $$"""
using Microsoft.Extensions.Localization;
using Spiderly.Security.Services;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Interfaces;
using {{basePartOfNamespace}}.Services;

namespace {{basePartOfNamespace}}.Services
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
        public IFileManager FileManager { get; }
        public IStringLocalizer Localizer { get; }
        public IServiceProvider ServiceProvider { get; }

        public EntityServiceDependencies(
            IApplicationDbContext context,
            ExcelService excelService,
            AuthorizationServiceGenerated authorizationService,
            IFileManager fileManager,
            IStringLocalizer localizer,
            IServiceProvider serviceProvider)
        {
            Context = context;
            ExcelService = excelService;
            AuthorizationService = authorizationService;
            FileManager = fileManager;
            Localizer = localizer;
            ServiceProvider = serviceProvider;
        }
    }
}
""";
        }

        #endregion

        #region Per-Entity Service

        private static string GetEntityServiceClass(SpiderlyClass entity, List<SpiderlyClass> allEntities, string basePartOfNamespace)
        {
            bool entityNeedsCloudinary = entity.Properties.Any(x => x.HasCloudinaryPublicIdAttribute());
            bool entityNeedsS3Public = entity.Properties.Any(x => x.HasS3PublicUrlAttribute());

            string storageFields = GetStorageFields(entityNeedsCloudinary, entityNeedsS3Public);
            string storageInit = GetStorageInit(entityNeedsCloudinary, entityNeedsS3Public);

            string methods = GetEntityServiceMethods(entity, allEntities);

            return $$"""
{{GetUsings(basePartOfNamespace)}}

namespace {{basePartOfNamespace}}.Services
{
    /// <summary>
    /// Generated service for the {{entity.Name}} entity. Override lifecycle hooks
    /// by creating a <c>{{entity.Name}}Service</c> class that inherits from this class.
    /// </summary>
    public class {{entity.Name}}ServiceGenerated : ServiceBase
    {
        protected readonly EntityServiceDependencies _deps;
{{storageFields}}

        public {{entity.Name}}ServiceGenerated(EntityServiceDependencies deps) : base(deps.Context, deps.Localizer)
        {
            _deps = deps;
{{storageInit}}
        }

{{methods}}

    }
}
""";
        }

        private static string GetStorageFields(bool needsCloudinary, bool needsS3Public)
        {
            StringBuilder sb = new();
            if (needsCloudinary)
                sb.AppendLine("        private readonly CloudinaryStorageService _cloudinaryStorageService;");
            if (needsS3Public)
                sb.AppendLine("        private readonly S3PublicStorageService _s3PublicStorageService;");
            return sb.ToString();
        }

        private static string GetStorageInit(bool needsCloudinary, bool needsS3Public)
        {
            StringBuilder sb = new();
            if (needsCloudinary)
                sb.AppendLine("            _cloudinaryStorageService = deps.ServiceProvider.GetRequiredService<CloudinaryStorageService>();");
            if (needsS3Public)
                sb.AppendLine("            _s3PublicStorageService = deps.ServiceProvider.GetRequiredService<S3PublicStorageService>();");
            return sb.ToString();
        }

        private static string GetEntityServiceMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            if (entity.IsManyToMany())
            {
                string m2mData = ServiceM2MGenerator.GetManyToManyData(entity, allEntities);
                if (m2mData == null)
                    return "";

                return $$"""
        #region M2M

{{m2mData}}

        #endregion
""";
            }

            return $$"""
        #region Read

{{ServiceReadGenerator.GetReadBusinessServiceMethods(entity, allEntities)}}

        #endregion

        #region Save

{{ServiceSaveGenerator.GetSavingData(entity, allEntities)}}

{{string.Join("\n\n", ServiceSaveGenerator.GetUploadBlobMethods(entity, allEntities))}}

{{string.Join("\n\n", ServiceSaveGenerator.GetUploadEditorImageMethods(entity, allEntities))}}

        #endregion

        #region Delete

{{string.Join("\n\n", ServiceDeleteGenerator.GetDeletingData(entity, allEntities))}}

        #endregion

        #region One To Many

{{string.Join("\n\n", ServiceOneToManyGenerator.GetOneToManyMethods(entity, allEntities))}}

        #endregion
""";
        }

        #endregion

        #region DI Registration

        private static string GetEntityServiceRegistration(List<SpiderlyClass> entities, List<SpiderlyClass> userEntityServices, string basePartOfNamespace)
        {
            StringBuilder registrations = new();

            registrations.AppendLine("            services.AddTransient(typeof(Lazy<>), typeof(LazyServiceProvider<>));");
            registrations.AppendLine("            services.AddTransient<EntityServiceDependencies>();");
            registrations.AppendLine();

            foreach (SpiderlyClass entity in entities)
            {
                string generatedTypeName = $"{entity.Name}ServiceGenerated";
                string userTypeName = $"{entity.Name}Service";

                bool hasUserOverride = userEntityServices.Any(x => x.Name == userTypeName);

                if (hasUserOverride)
                {
                    registrations.AppendLine($"            services.AddTransient<{userTypeName}>();");
                    registrations.AppendLine($"            services.AddTransient<{generatedTypeName}>(sp => sp.GetRequiredService<{userTypeName}>());");
                }
                else
                {
                    registrations.AppendLine($"            services.AddTransient<{generatedTypeName}>();");
                }
            }

            return $$"""
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Services;

namespace {{basePartOfNamespace}}.Services
{
    /// <summary>
    /// Registers all entity services in the DI container.
    /// Call <c>services.AddEntityServices()</c> in your startup configuration.
    /// </summary>
    public static class EntityServiceRegistration
    {
        public static IServiceCollection AddEntityServices(this IServiceCollection services)
        {
{{registrations}}
            return services;
        }
    }
}
""";
        }

        #endregion

        #region Helpers

        internal static string GetUsings(string basePartOfTheNamespace)
        {
            return $$"""
using {{basePartOfTheNamespace}}.ValidationRules;
using {{basePartOfTheNamespace}}.DataMappers;
using {{basePartOfTheNamespace}}.DTO;
using {{basePartOfTheNamespace}}.Entities;
using {{basePartOfTheNamespace}}.Enums;
using {{basePartOfTheNamespace}}.ExcelProperties;
using {{basePartOfTheNamespace}}.Filtering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Data;
using FluentValidation;
using Spiderly.Security.Services;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Services;
using Spiderly.Shared.Classes;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;
using Mapster;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
""";
        }

        internal static string GetAuthorizeEntityMethodCall(string entityName, CrudCodes crudCode, string parametersBody)
        {
            string methodName = Helpers.GetAuthorizeEntityMethodName(entityName, crudCode);
            return $"await _deps.AuthorizationService.{methodName}({parametersBody});";
        }

        internal static string GetFileManagerServiceField(SpiderlyProperty property)
        {
            if (property.HasCloudinaryPublicIdAttribute())
                return "_cloudinaryStorageService";

            if (property.HasS3PublicUrlAttribute())
                return "_s3PublicStorageService";

            return "_deps.FileManager";
        }

        #endregion

    }
}
