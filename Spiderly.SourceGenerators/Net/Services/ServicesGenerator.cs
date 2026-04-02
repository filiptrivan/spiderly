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
    /// Generates the `BusinessServiceGenerated` class (`BusinessService.generated.cs`)
    /// within the `{YourBaseNamespace}.Services` namespace. This class provides the
    /// core business logic for your entities, including CRUD operations, data retrieval,
    /// Excel export, and basic authorization checks. It serves as a base class for
    /// your custom business services.
    /// </summary>
    [Generator]
    public class ServicesGenerator : IIncrementalGenerator
    {

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var combined = PipelineFactory.CreatePipeline(context,
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities },
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities });

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
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

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            bool shouldGenerateCloudinaryStorageService = currentProjectEntities.Any(x => x.Properties.Any(x => x.HasCloudinaryPublicIdAttribute()));
            bool shouldGenerateS3PublicStorageService = currentProjectEntities.Any(x => x.Properties.Any(x => x.HasS3PublicUrlAttribute()));

            string result = $$"""
{{GetUsings(basePartOfNamespace)}}

namespace {{basePartOfNamespace}}.Services
{
    public class BusinessServiceGenerated : BusinessServiceBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ExcelService _excelService;
        private readonly AuthorizationService _authorizationService;
        private readonly IFileManager _fileManager;
        private readonly IStringLocalizer _localizer;
        {{(shouldGenerateCloudinaryStorageService ? "private readonly CloudinaryStorageService _cloudinaryStorageService;" : "")}}
        {{(shouldGenerateS3PublicStorageService ? "private readonly S3PublicStorageService _s3PublicStorageService;" : "")}}

        public BusinessServiceGenerated(
            IApplicationDbContext context,
            ExcelService excelService,
            AuthorizationService authorizationService,
            IFileManager fileManager,
            IStringLocalizer localizer
            {{(shouldGenerateCloudinaryStorageService ? ", CloudinaryStorageService cloudinaryStorageService" : "")}}
            {{(shouldGenerateS3PublicStorageService ? ", S3PublicStorageService s3PublicStorageService" : "")}}
        )
            : base(context)
        {
            _context = context;
            _excelService = excelService;
            _authorizationService = authorizationService;
            _fileManager = fileManager;
            _localizer = localizer;
            {{(shouldGenerateCloudinaryStorageService ? "_cloudinaryStorageService = cloudinaryStorageService;" : "")}}
            {{(shouldGenerateS3PublicStorageService ? "_s3PublicStorageService = s3PublicStorageService;" : "")}}
        }

{{string.Join("\n\n", GetBusinessServiceMethods(currentProjectEntities, allEntities))}}

    }
}
""";

            context.AddSource($"BusinessService.generated", SourceText.From(result, Encoding.UTF8));
        }

        private static List<string> GetBusinessServiceMethods(List<SpiderlyClass> entityClasses, List<SpiderlyClass> allEntityClasses)
        {
            List<string> result = new();

            foreach (SpiderlyClass entity in entityClasses)
            {
                if (entity.IsManyToMany())
                {
                    result.Add($$"""
        #region {{entity.Name}} - M2M

{{ServiceM2MGenerator.GetManyToManyData(entity, allEntityClasses)}}

        #endregion
""");
                }
                else
                {
                    result.Add($$"""
        #region {{entity.Name}}

        #region Read

{{ServiceReadGenerator.GetReadBusinessServiceMethods(entity, allEntityClasses)}}

        #endregion

        #region Save

{{ServiceSaveGenerator.GetSavingData(entity, allEntityClasses)}}

{{string.Join("\n\n", ServiceSaveGenerator.GetUploadBlobMethods(entity, allEntityClasses))}}

{{string.Join("\n\n", ServiceSaveGenerator.GetUploadEditorImageMethods(entity, allEntityClasses))}}

        #endregion

        #region Delete

{{string.Join("\n\n", ServiceDeleteGenerator.GetDeletingData(entity, allEntityClasses))}}

        #endregion

        #region One To Many

{{string.Join("\n\n", ServiceOneToManyGenerator.GetOneToManyMethods(entity, allEntityClasses))}}

        #endregion

        #endregion
""");
                }
            }

            return result;
        }

        #region Helpers

        private static string GetUsings(string basePartOfTheNamespace)
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
using Spiderly.Shared.Resources;
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
            return $"await _authorizationService.{methodName}({parametersBody});";
        }

        internal static string GetFileManagerServiceField(SpiderlyProperty property)
        {
            if (property.HasCloudinaryPublicIdAttribute())
                return "_cloudinaryStorageService";

            if (property.HasS3PublicUrlAttribute())
                return "_s3PublicStorageService";

            return "_fileManager";
        }

        #endregion

    }
}
