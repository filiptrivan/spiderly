using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Collections.Immutable;
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
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.Services },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities });

            var combinedWithEnums = combined
                .Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider))
                .Combine(PipelineFactory.GetNullableContextProvider(context));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var ((combinedSource, enumNames), nullableContext) = source;
                var ((classes, referencedClasses), config) = combinedSource;
                Execute(classes, referencedClasses, enumNames, config, nullableContext, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, ImmutableArray<string> spiderlyEnumNames, SpiderlyConfig config, NullableContextOptions nullableContext, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(ServicesGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectEntities, spiderlyEnumNames);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            List<SpiderlyClass> userEntityServices = currentProjectClasses
                .Where(x => x.HasSpiderlyServiceAttribute())
                .ToList();

            if (currentProjectEntities.Count == 0)
                return;

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            // Generate EntityServiceDependencies
            context.AddSpiderlyCSharpSource("EntityServiceDependencies.generated",
                GetEntityServiceDependencies(basePartOfNamespace), nullableContext);

            // Generate one service file per entity
            foreach (SpiderlyClass entity in currentProjectEntities)
            {
                string entityServiceCode = GetEntityServiceClass(entity, allEntities, basePartOfNamespace);
                context.AddSpiderlyCSharpSource($"{entity.Name}Service.generated", entityServiceCode, nullableContext);
            }

            // Generate DI registration
            context.AddSpiderlyCSharpSource("EntityServiceRegistration.generated",
                GetEntityServiceRegistration(currentProjectEntities, userEntityServices, basePartOfNamespace), nullableContext);
        }

        #region EntityServiceDependencies

        // NOTE: EntityServiceDependencies must be source-generated (not in Spiderly.Shared) because it
        // references AuthorizationServiceGenerated, which is generated per project with entity-specific methods.
        // We intentionally use IServiceProvider here (service locator) — it keeps this class universal across
        // all entities, avoids per-entity deps classes, and sidesteps circular dependency issues without Lazy<T>.
        // Storage adapters are not in this bundle: per-blob-property attributes ([DiskStorage] / [S3PublicStorage]
        // / [S3PrivateStorage] / custom StorageAttribute subclass) drive direct GetRequiredService<TConcrete>
        // calls in each generated entity service, so there is no global IFileManager slot.
        private static string GetEntityServiceDependencies(string basePartOfNamespace)
        {
            return $$"""
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Spiderly.Security.Services;
using Spiderly.Shared;
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
        public IStringLocalizer Localizer { get; }
        public IServiceProvider ServiceProvider { get; }
        public ExcelOptions ExcelSettings { get; }

        public EntityServiceDependencies(
            IApplicationDbContext context,
            ExcelService excelService,
            AuthorizationServiceGenerated authorizationService,
            IStringLocalizer localizer,
            IServiceProvider serviceProvider,
            IOptions<ExcelOptions> excelOptions)
        {
            Context = context;
            ExcelService = excelService;
            AuthorizationService = authorizationService;
            Localizer = localizer;
            ServiceProvider = serviceProvider;
            ExcelSettings = excelOptions.Value;
        }
    }
}
""";
        }

        #endregion

        #region Per-Entity Service

        private static string GetEntityServiceClass(SpiderlyClass entity, List<SpiderlyClass> allEntities, string basePartOfNamespace)
        {
            bool entityNeedsS3Public = entity.Properties.Any(x => x.HasS3PublicStorageAttribute());
            bool entityNeedsS3Private = entity.Properties.Any(x => x.HasS3PrivateStorageAttribute());
            bool entityNeedsDisk = entity.Properties.Any(x => x.HasDiskStorageAttribute());

            string storageFields = GetStorageFields(entityNeedsS3Public, entityNeedsS3Private, entityNeedsDisk);
            string storageInit = GetStorageInit(entityNeedsS3Public, entityNeedsS3Private, entityNeedsDisk);

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

        private static string GetStorageFields(bool needsS3Public, bool needsS3Private, bool needsDisk)
        {
            StringBuilder sb = new();
            if (needsS3Public)
                sb.AppendLine("        private readonly S3PublicStorageService _s3PublicStorageService;");
            if (needsS3Private)
                sb.AppendLine("        private readonly S3PrivateStorageService _s3PrivateStorageService;");
            if (needsDisk)
                sb.AppendLine("        private readonly DiskStorageService _diskStorageService;");
            return sb.ToString();
        }

        private static string GetStorageInit(bool needsS3Public, bool needsS3Private, bool needsDisk)
        {
            StringBuilder sb = new();
            if (needsS3Public)
                sb.AppendLine("            _s3PublicStorageService = deps.ServiceProvider.GetRequiredService<S3PublicStorageService>();");
            if (needsS3Private)
                sb.AppendLine("            _s3PrivateStorageService = deps.ServiceProvider.GetRequiredService<S3PrivateStorageService>();");
            if (needsDisk)
                sb.AppendLine("            _diskStorageService = deps.ServiceProvider.GetRequiredService<DiskStorageService>();");
            return sb.ToString();
        }

        private static string GetEntityServiceMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            if (entity.IsManyToMany())
            {
                string? m2mData = ServiceM2MGenerator.GetManyToManyData(entity, allEntities);
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

            // EntityServiceDependencies injects AuthorizationServiceGenerated directly, so it must be registered.
            // Mirror the per-entity pattern below: forward the generated base to the user's AuthorizationService
            // subclass when present (so its overrides apply), otherwise register the generated base directly. This
            // is wired here — rather than left to the consumer's startup — so a forgotten registration can't 403
            // every permission-gated endpoint or fail DI validation at boot.
            if (userEntityServices.Any(x => x.Name == "AuthorizationService"))
            {
                registrations.AppendLine("            services.AddTransient<AuthorizationService>();");
                registrations.AppendLine("            services.AddTransient<AuthorizationServiceGenerated>(sp => sp.GetRequiredService<AuthorizationService>());");
            }
            else
            {
                registrations.AppendLine("            services.AddTransient<AuthorizationServiceGenerated>();");
            }
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
            if (property.HasS3PublicStorageAttribute())
                return "_s3PublicStorageService";

            if (property.HasS3PrivateStorageAttribute())
                return "_s3PrivateStorageService";

            if (property.HasDiskStorageAttribute())
                return "_diskStorageService";

            // Should be unreachable: only properties with a recognized [*Storage] attribute
            // reach this dispatcher (IsBlob() gates upstream callers). If the entity carries
            // a custom StorageAttribute subclass that isn't one of the three built-ins, the
            // current generator can't resolve a field for it — emit an obvious compile error.
            return $"/* SPIDERLY: unrecognized storage attribute on {property.Name}; the source generator only auto-resolves [DiskStorage], [S3PublicStorage], [S3PrivateStorage]. Custom adapters must inject directly into hand-written services. */";
        }

        #endregion

    }
}
