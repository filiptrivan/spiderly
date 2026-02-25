using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Generates base API controller classes (`{YourAppName}BaseControllers.generated.cs`)
    /// within the `{YourBaseNamespace}.Controllers` namespace. These base controllers provide
    /// generic CRUD endpoints for your entities, leveraging corresponding business services.
    /// </summary>
    [Generator]
    public class ControllerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => Helpers.IsSyntaxTargetForGenerationEveryClass(s),
                    transform: static (ctx, _) => Helpers.GetSemanticTargetForGenerationEveryClass(ctx))
                .Where(static c => c is not null);

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.Services
                });

            IncrementalValueProvider<string> callingProjectDirectory = context.GetCallingPath();
            IncrementalValueProvider<string> jsonConfig = context.GetJsonConfig();

            var combined = classDeclarations.Collect()
                .Combine(referencedProjectClasses)
                .Combine(callingProjectDirectory)
                .Combine(jsonConfig);

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (classesAndEntitiesAndPath, jsonContent) = source;
                var (classesAndEntities, callingPath) = classesAndEntitiesAndPath;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, callingPath, jsonContent, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntitiesAndServices, string callingProjectDirectory, string jsonConfigContent, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (Helpers.ShouldSkipGenerator(nameof(ControllerGenerator), jsonConfigContent))
                return;

            if (callingProjectDirectory.Contains(".WebAPI") == false)
                return;

            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectEntitiesAndServices);

            List<SpiderlyClass> customControllers = currentProjectClasses
                .Where(x => x.Namespace.EndsWith(".Controllers"))
                .ToList();

            List<SpiderlyClass> allEntities = referencedProjectEntitiesAndServices
                .Where(x => x.Namespace.EndsWith($".{NamespaceExtensionCodes.Entities}"))
                .ToList();

            string namespaceValue = currentProjectClasses[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);
            string appName = namespaceValue.Split('.')[0]; // eg. PlayertyLoyals

            string result = $$"""
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using System.Data;
using Spiderly.Infrastructure;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.DTO;
using {{appName}}.Shared.Resources;
using {{appName}}.Business.Services;
{{string.Join("\n", Helpers.GetEntityClassesUsings(allEntities))}}
{{string.Join("\n", Helpers.GetDTOClassesUsings(allEntities))}}

namespace {{basePartOfNamespace}}.Controllers
{
{{string.Join("\n\n", GetControllerClasses(allEntities, customControllers))}}
}
""";

            context.AddSource($"BaseControllers.generated", SourceText.From(result, Encoding.UTF8));
        }

        public static List<string> GetControllerClasses(List<SpiderlyClass> allEntities, List<SpiderlyClass> customControllers)
        {
            List<string> result = new();

            foreach (IGrouping<string, SpiderlyClass> groupedControllerEntities in allEntities.GroupBy(x => x.ControllerName))
            {
                result.Add($$"""
{{GetControllerAttributes(groupedControllerEntities, customControllers)}}
    public class {{groupedControllerEntities.Key}}BaseController : SpiderlyBaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly BusinessService _businessService;

        public {{groupedControllerEntities.Key}}BaseController(
            IApplicationDbContext context, 
            BusinessService businessService
        )
        {
            _context = context;
            _businessService = businessService;
        }

{{string.Join("\n\n", GetControllerMethods(groupedControllerEntities.ToList(), allEntities))}}

    }
""");
            }

            return result;
        }

        private static string GetControllerAttributes(IGrouping<string, SpiderlyClass> groupedControllerEntities, List<SpiderlyClass> customControllers)
        {
            if (customControllers.Any(x => x.BaseType == $"{groupedControllerEntities.Key}BaseController"))
                return null;

            return $$"""
    [ApiController]
    [Route("/api/{{groupedControllerEntities.Key}}/[action]")]
""";
        }

        private static List<string> GetControllerMethods(List<SpiderlyClass> groupedControllerEntities, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyClass controllerEntity in groupedControllerEntities)
            {
                if (controllerEntity.IsManyToMany()) // TODO FT: Do something with M2M entities
                    continue;

                string referencedProjectEntityClassIdType = controllerEntity.GetIdType(allEntities);

                result.Add($$"""
        #region {{controllerEntity.Name}}

        #region Read

        [HttpPost]
        [AuthGuard]
        public virtual async Task<PaginatedResultDTO<{{controllerEntity.Name}}DTO>> GetPaginated{{controllerEntity.Name}}List(FilterDTO filterDTO)
        {
            return await _businessService.GetPaginated{{controllerEntity.Name}}List(filterDTO, _context.DbSet<{{controllerEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
        }

        [HttpPost]
        [AuthGuard]
        public virtual async Task<IActionResult> Export{{controllerEntity.Name}}ListToExcel(FilterDTO filterDTO)
        {
            byte[] fileContent = await _businessService.Export{{controllerEntity.Name}}ListToExcel(filterDTO, _context.DbSet<{{controllerEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
            return File(
                fileContent, 
                Spiderly.Shared.SettingsProvider.Current.ExcelContentType,
                Uri.EscapeDataString($"{TermsGenerated.GetExcelTranslation("{{controllerEntity.Name}}ExcelExportName", "{{controllerEntity.Name}}List")}.xlsx")
            );
        }

        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<{{controllerEntity.Name}}DTO>> Get{{controllerEntity.Name}}List()
        {
            return await _businessService.Get{{controllerEntity.Name}}DTOList(_context.DbSet<{{controllerEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
        }

        [HttpGet]
        [AuthGuard]
        public virtual async Task<{{controllerEntity.Name}}MainUIFormDTO> Get{{controllerEntity.Name}}MainUIFormDTO({{referencedProjectEntityClassIdType}} id)
        {
            return await _businessService.Get{{controllerEntity.Name}}MainUIFormDTO(id, {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
        }

        [HttpGet]
        [AuthGuard]
        public virtual async Task<{{controllerEntity.Name}}DTO> Get{{controllerEntity.Name}}({{referencedProjectEntityClassIdType}} id)
        {
            return await _businessService.Get{{controllerEntity.Name}}DTO(id, {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
        }

{{GetManyToOneReadMethods(controllerEntity, allEntities)}}

{{string.Join("\n\n", GetOrderedOneToManyControllerMethods(controllerEntity, allEntities))}}

{{string.Join("\n\n", GetManyToManyControllerMethods(controllerEntity, allEntities))}}

{{string.Join("\n\n", GetComplexManyToManyListControllerMethods(controllerEntity, allEntities))}}

        #endregion

        #region Save

{{GetSaveControllerMethods(controllerEntity)}}

{{string.Join("\n\n", GetUploadBlobControllerMethods(controllerEntity, allEntities))}}

{{string.Join("\n\n", GetUploadEditorImageControllerMethods(controllerEntity, allEntities))}}

        #endregion

        #region Delete

{{GetDeleteControllerMethods(controllerEntity, allEntities)}}

        #endregion

        #endregion
""");
            }

            return result;
        }

        #region Many To One

        private static string GetManyToOneReadMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            StringBuilder sb = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.ShouldGenerateAutocompleteControllerMethod())
                {
                    sb.Append($$"""
{{GetAutocompleteMethod(property, entity, allEntities)}}

""");
                }

                if (property.ShouldGenerateDropdownControllerMethod())
                {
                    sb.Append($$"""
{{GetDropdownMethod(property, entity, allEntities)}}

""");
                }
            }

            return sb.ToString();
        }

        private static string GetAutocompleteMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            SpiderlyClass manyToOneEntity = allEntities.SingleOrDefault(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));

            if (manyToOneEntity == null)
                throw new Exception($"Property where problem occurred: Name: {property.Name}, Type: {property.Type}, Parent entity name: {property.EntityName}");

            string manyToOneEntityIdType = manyToOneEntity.GetIdType(allEntities);
            string manyToOneDisplayName = Helpers.GetDisplayNameProperty(manyToOneEntity);

            return $$"""
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<NamebookDTO<{{manyToOneEntityIdType}}>>> Get{{property.Name}}AutocompleteListFor{{entity.Name}}(int limit, string filter, {{entity.GetIdType(allEntities)}}? {{entity.Name.FirstCharToLower()}}Id)
        {
            return await _businessService.Get{{property.Name}}AutocompleteListFor{{entity.Name}}(
                limit,
                filter,
                _context.DbSet<{{manyToOneEntity.Name}}>(),
                {{Helpers.GetShouldAuthorizeEntityString(entity)}},
                {{entity.Name.FirstCharToLower()}}Id
            );
        }
""";
        }

        private static string GetDropdownMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            SpiderlyClass manyToOneEntity = allEntities.SingleOrDefault(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));

            if (manyToOneEntity == null)
                throw new Exception($"Property where problem occurred: Name: {property.Name}, Type: {property.Type}, Parent entity name: {property.EntityName}");

            string manyToOneEntityIdType = manyToOneEntity.GetIdType(allEntities);
            string manyToOneDisplayName = Helpers.GetDisplayNameProperty(manyToOneEntity);

            return $$"""
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<NamebookDTO<{{manyToOneEntityIdType}}>>> Get{{property.Name}}DropdownListFor{{entity.Name}}({{entity.GetIdType(allEntities)}}? {{entity.Name.FirstCharToLower()}}Id)
        {
            return await _businessService.Get{{property.Name}}DropdownListFor{{entity.Name}}(
                _context.DbSet<{{manyToOneEntity.Name}}>(), 
                {{Helpers.GetShouldAuthorizeEntityString(entity)}},
                {{entity.Name.FirstCharToLower()}}Id
            );
        }
""";
        }

        #endregion

        #region Many To Many

        private static List<string> GetManyToManyControllerMethods(SpiderlyClass referencedProjectEntityClass, List<SpiderlyClass> referencedProjectEntities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in referencedProjectEntityClass.Properties)
            {
                if (property.IsMultiSelectControlType() ||
                    property.IsMultiAutocompleteControlType())
                {
                    result.Add(GetManyToManySelectedEntitiesControllerMethod(property, referencedProjectEntityClass, referencedProjectEntities));
                }
                else if (property.HasSimpleManyToManyTableLazyLoadAttribute())
                {
                    result.Add(GetSimpleManyToManyTableLazyLoadControllerMethod(property, referencedProjectEntityClass, referencedProjectEntities));
                }
                else if (property.HasComplexManyToManyReadonlyTableAttribute())
                {
                    result.Add(GetComplexManyToManyReadonlyTableControllerMethod(property, referencedProjectEntityClass, referencedProjectEntities));
                }
            }

            return result;
        }

        private static string GetSimpleManyToManyTableLazyLoadControllerMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            SpiderlyClass extractedEntity = entities.SingleOrDefault(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));
            string extractedEntityIdType = extractedEntity.GetIdType(entities);

            return $$"""
        [HttpPost]
        [AuthGuard]
        public virtual async Task<PaginatedResultDTO<{{extractedEntity.Name}}DTO>> GetPaginated{{property.Name}}ListFor{{entity.Name}}(FilterDTO filterDTO)
        {
            return await _businessService.GetPaginated{{extractedEntity.Name}}List(filterDTO, _context.DbSet<{{extractedEntity.Name}}>().OrderBy(x => x.Id), false);
        }

        [HttpPost]
        [AuthGuard]
        public virtual async Task<IActionResult> Export{{property.Name}}ListToExcelFor{{entity.Name}}(FilterDTO filterDTO)
        {
            byte[] fileContent = await _businessService.Export{{extractedEntity.Name}}ListToExcel(filterDTO, _context.DbSet<{{extractedEntity.Name}}>(), false);
            return File(
                fileContent, 
                Spiderly.Shared.SettingsProvider.Current.ExcelContentType,
                Uri.EscapeDataString($"{TermsGenerated.GetExcelTranslation("{{extractedEntity.Name}}ExcelExportName", "{{extractedEntity.Name}}List")}.xlsx")
            );
        }

        [HttpPost]
        [AuthGuard]
        public virtual async Task<LazyLoadSelectedIdsResultDTO<{{extractedEntityIdType}}>> LazyLoadSelected{{property.Name}}IdsFor{{entity.Name}}(FilterDTO filterDTO)
        {
            return await _businessService.LazyLoadSelected{{property.Name}}IdsFor{{entity.Name}}(filterDTO, _context.DbSet<{{extractedEntity.Name}}>().OrderBy(x => x.Id), {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }
""";
        }

        private static string GetComplexManyToManyReadonlyTableControllerMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            SpiderlyClass extractedEntity = entities.SingleOrDefault(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));

            return $$"""
        [HttpPost]
        [AuthGuard]
        public virtual async Task<PaginatedResultDTO<{{extractedEntity.Name}}DTO>> GetPaginated{{property.Name}}ListFor{{entity.Name}}(FilterDTO filterDTO)
        {
            return await _businessService.GetPaginated{{property.Name}}ListFor{{entity.Name}}(filterDTO, _context.DbSet<{{extractedEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }

        [HttpPost]
        [AuthGuard]
        public virtual async Task<IActionResult> Export{{property.Name}}ListToExcelFor{{entity.Name}}(FilterDTO filterDTO)
        {
            byte[] fileContent = await _businessService.Export{{property.Name}}ListToExcelFor{{entity.Name}}(filterDTO, _context.DbSet<{{extractedEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(entity)}});
            return File(
                fileContent, 
                Spiderly.Shared.SettingsProvider.Current.ExcelContentType,
                Uri.EscapeDataString($"{TermsGenerated.GetExcelTranslation("{{extractedEntity.Name}}ExcelExportName", "{{extractedEntity.Name}}List")}.xlsx")
            );
        }
""";
        }

        private static string GetManyToManySelectedEntitiesControllerMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

            return $$"""
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<NamebookDTO<{{extractedEntity.GetIdType(entities)}}>>> Get{{property.Name}}NamebookListFor{{entity.Name}}({{entity.GetIdType(entities)}} id)
        {
            return await _businessService.Get{{property.Name}}NamebookListFor{{entity.Name}}(id, false);
        }
""";
        }

        #endregion

        #region One To Many

        private static List<string> GetOrderedOneToManyControllerMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            List<SpiderlyProperty> uiOrderedOneToManyProperties = Helpers.GetUIOrderedOneToManyProperties(entity);

            foreach (SpiderlyProperty property in uiOrderedOneToManyProperties)
            {
                result.Add($$"""
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<{{Helpers.ExtractTypeFromGenericType(property.Type)}}MainUIFormDTO>> GetOrdered{{property.Name}}For{{entity.Name}}({{entity.GetIdType(entities)}} id)
        {
            return await _businessService.GetOrdered{{property.Name}}For{{entity.Name}}(id, {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }
""");
            }

            return result;
        }

        #endregion

        #region Complex Many To Many List

        private static List<string> GetComplexManyToManyListControllerMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetComplexManyToManyListProperties())
            {
                SpiderlyClass junctionEntity = allEntities.Single(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));

                result.Add($$"""
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<{{junctionEntity.Name}}DTO>> GetDefault{{property.Name}}For{{entity.Name}}()
        {
            return await _businessService.GetDefault{{property.Name}}For{{entity.Name}}();
        }
""");
            }

            return result;
        }

        #endregion

        #region Delete

        private static string GetDeleteControllerMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            if (entity.IsReadonlyObject())
                return null;

            return $$"""
        [HttpDelete]
        [AuthGuard]
        public virtual async Task Delete{{entity.Name}}({{entity.GetIdType(entities)}} id)
        {
            await _businessService.Delete{{entity.Name}}(id, {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }
""";
        }

        #endregion

        #region Save

        private static string GetSaveControllerMethods(SpiderlyClass entity)
        {
            if (entity.IsReadonlyObject())
                return null;

            return $$"""
        [HttpPut]
        [AuthGuard]
        public virtual async Task<{{entity.Name}}MainUIFormDTO> Save{{entity.Name}}({{entity.Name}}SaveBodyDTO saveBodyDTO)
        {
            return await _businessService.Save{{entity.Name}}AndReturnMainUIFormDTO(saveBodyDTO, {{Helpers.GetShouldAuthorizeEntityString(entity)}}, {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }
""";
        }

        private static List<string> GetUploadBlobControllerMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            List<SpiderlyProperty> blobProperies = Helpers.GetBlobProperties(entity.Properties);

            foreach (SpiderlyProperty property in blobProperies)
            {
                result.Add($$"""
        // FT: You can't upload and delete on every request because you can delete the old image for the user when he refreshes the page
        [HttpPost]
        [AuthGuard]
        public virtual async Task<string> Upload{{property.Name}}For{{entity.Name}}([FromForm] IFormFile file) // FT: It doesn't work without interface
        {
            return await _businessService.Upload{{property.Name}}For{{entity.Name}}(file, {{Helpers.GetShouldAuthorizeEntityString(entity)}}, {{Helpers.GetShouldAuthorizeEntityString(entity)}}); // TODO: Make authorization in business service with override
        }
"""
);
            }

            return result;
        }

        private static List<string> GetUploadEditorImageControllerMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            List<SpiderlyProperty> editorProperties = entity.Properties
                .Where(x => x.IsEditorControlType() && x.HasS3PublicUrlAttribute())
                .ToList();

            foreach (SpiderlyProperty property in editorProperties)
            {
                result.Add($$"""
        [HttpPost]
        [AuthGuard]
        public virtual async Task<string> Upload{{property.Name}}ImageFor{{entity.Name}}([FromForm] IFormFile file)
        {
            return await _businessService.Upload{{property.Name}}ImageFor{{entity.Name}}(file, {{Helpers.GetShouldAuthorizeEntityString(entity)}}, {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }
""");
            }

            return result;
        }

        #endregion
    }
}
