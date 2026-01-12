using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Generates an `AuthorizationServiceGenerated` class (`AuthorizationService.generated.cs`)
    /// that extends `AuthorizationService` and provides methods for declarative authorization checks
    /// based on your entity classes. This service simplifies the process of enforcing permissions
    /// before performing CRUD operations on your entities.
    /// </summary>
    [Generator]
    public class AuthorizationServicesGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = Helpers.GetClassIncrementalValuesProvider(context.SyntaxProvider, new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                });

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                });

            IncrementalValueProvider<string> jsonConfig = context.GetJsonConfig();

            var allClasses = classDeclarations.Collect()
                .Combine(referencedProjectClasses)
                .Combine(jsonConfig);

            context.RegisterImplementationSourceOutput(allClasses, static (spc, source) =>
            {
                var (classesAndReferenced, jsonContent) = source;
                Execute(classesAndReferenced.Left, classesAndReferenced.Right, jsonContent, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, string jsonConfigContent, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (Helpers.ShouldSkipGenerator(nameof(AuthorizationServicesGenerator), jsonConfigContent))
                return;

            List<SpiderlyClass> currentProjectEntities = Helpers.GetSpiderlyClasses(classes, referencedProjectEntities);
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            string result = $$"""
{{GetUsings(basePartOfNamespace)}}

namespace {{basePartOfNamespace}}.Services
{
    public class AuthorizationServiceGenerated : AuthorizationServiceBase
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;

        public AuthorizationServiceGenerated(
            IApplicationDbContext context, 
            AuthenticationService authenticationService
        )
            : base(context, authenticationService)
        {
            _context = context;
            _authenticationService = authenticationService;
        }

{{GetAuthorizeRegions(currentProjectEntities, allEntities)}}

    }
}
""";

            context.AddSource($"AuthorizationService.generated", SourceText.From(result, Encoding.UTF8));
        }

        public static string GetAuthorizeRegions(List<SpiderlyClass> currentProjectEntities, List<SpiderlyClass> allEntities)
        {
            StringBuilder sb = new();

            foreach (SpiderlyClass entity in currentProjectEntities)
            {
                if (entity.BaseType == null) // Handling many to many, maybe you should do something else in the future
                    continue;

                string entityIdType = entity.GetIdType(allEntities);

                sb.AppendLine($$"""
        #region {{entity.Name}}

{{GetAuthorizeEntityMethod(entity.Name, entity, CrudCodes.Read, $"{entityIdType}? {entity.Name.FirstCharToLower()}IdToRead")}}

{{GetAuthorizeEntityMethod(entity.Name, entity, CrudCodes.Read, $"List<{entityIdType}> {entity.Name.FirstCharToLower()}IdListToRead")}}

{{GetAuthorizeEntityMethod(entity.Name, entity, CrudCodes.Update, $"{entity.Name}DTO dto")}}

{{GetAuthorizeEntityMethod(entity.Name, entity, CrudCodes.Insert, $"{entity.Name}DTO dto")}}

{{GetAuthorizeEntityMethod(entity.Name, entity, CrudCodes.Delete, $"{entityIdType} {entity.Name.FirstCharToLower()}Id")}}

{{GetAuthorizeEntityMethod(entity.Name, entity, CrudCodes.Delete, $"List<{entityIdType}> {entity.Name.FirstCharToLower()}ListToDelete")}}

{{GetBloblAuthorizeEntityMethods(entity, entityIdType)}}

        #endregion

""");
            }

            return sb.ToString();
        }

        private static string GetBloblAuthorizeEntityMethods(SpiderlyClass entity, string entityIdType)
        {
            StringBuilder sb = new();

            foreach (SpiderlyProperty property in Helpers.GetBlobProperties(entity.Properties))
            {
                sb.AppendLine($$"""
{{GetAuthorizeEntityMethod($"{property.Name}For{entity.Name}", entity, CrudCodes.Update, $"{entityIdType} {entity.Name.FirstCharToLower()}Id")}} // Blob update

{{GetAuthorizeEntityMethod($"{property.Name}For{entity.Name}", entity, CrudCodes.Insert, $"")}} // Blob insert, the id will always be 0, so we don't need to pass it.
""");
            }

            return sb.ToString();
        }

        private static string GetAuthorizeEntityMethod(
            string authorizeEntityMethodFirstPart,
            SpiderlyClass entity,
            CrudCodes crudCode,
            string parametersBody
        )
        {
            string methodName = Helpers.GetAuthorizeEntityMethodName(authorizeEntityMethodFirstPart, crudCode);
            return GetAuthorizeMethod(methodName, parametersBody, crudCode, entity);
        }

        private static string GetAuthorizeMethod(string methodName, string parametersBody, CrudCodes permissionCodePrefix, SpiderlyClass entity)
        {
            return $$"""
        public virtual async Task {{methodName}}({{parametersBody}})
        {
            await _context.WithTransactionAsync(async () =>
            {
                await AuthorizeAndThrowAsync<User>(PermissionCodes.{{permissionCodePrefix}}{{entity.Name}});
            });
        }
""";
        }

        private static string GetUsings(string basePartOfTheNamespace)
        {
            return $$"""
using {{basePartOfTheNamespace}}.Entities;
using {{basePartOfTheNamespace}}.Enums;
using {{basePartOfTheNamespace}}.DTO;
using Azure.Storage.Blobs;
using Spiderly.Security.Services;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Helpers;
""";
        }
    }
}