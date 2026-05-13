using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            var combined = PipelineFactory.CreatePipelineWithCallingPath(context,
                new List<ClassCategoryCodes> { ClassCategoryCodes.Controllers },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO, ClassCategoryCodes.Services });

            var combinedWithEnums = combined.Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var (combinedSource, enumNames) = source;
                var (classesAndEntitiesAndPath, config) = combinedSource;
                var (classesAndEntities, callingPath) = classesAndEntitiesAndPath;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, enumNames, callingPath, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntitiesAndServices, ImmutableArray<string> spiderlyEnumNames, string callingProjectDirectory, SpiderlyConfig config, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(ControllerGenerator)))
                return;

            if (callingProjectDirectory.Contains(".WebAPI") == false)
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectEntitiesAndServices, spiderlyEnumNames);

            List<SpiderlyClass> customControllers = currentProjectClasses
                .Where(x => x.HasSpiderlyControllerAttribute())
                .ToList();

            List<SpiderlyClass> allEntities = referencedProjectEntitiesAndServices
                .Where(x => x.HasSpiderlyEntityAttribute())
                .ToList();

            List<SpiderlyClass> allDTOs = referencedProjectEntitiesAndServices
                .Where(x => x.HasSpiderlyDTOAttribute())
                .ToList();

            string namespaceValue = currentProjectClasses[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);
            string appName = Helpers.GetAppName(namespaceValue);

            string result = $$"""
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Data;
using Spiderly.Infrastructure;
using Spiderly.Shared.Constants;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Localization;
using Spiderly.Shared.DTO;
using {{appName}}.Business.Services;
{{string.Join("\n", ReferencedAssemblyAnalyzer.GetClassesUsings(allEntities))}}
{{string.Join("\n", ReferencedAssemblyAnalyzer.GetClassesUsings(allDTOs))}}

namespace {{basePartOfNamespace}}.Controllers
{
{{string.Join("\n\n", GetControllerClasses(allEntities, customControllers, config, context))}}
}
""";

            context.AddSource($"BaseControllers.generated", SourceText.From(result, Encoding.UTF8));
        }

        // NOTE: We intentionally use IServiceProvider here (service locator pattern).
        // Each controller resolves its entity services via GetRequiredService<T>() at runtime.
        // This keeps every generated base controller with the same simple constructor signature,
        // avoids computing per-controller service dependency graphs in the generator,
        // and means user-written controllers don't break when entity relationships change.
        // We can introduce breaking changes freely, so we can always refactor later if needed.
        public static List<string> GetControllerClasses(List<SpiderlyClass> allEntities, List<SpiderlyClass> customControllers, SpiderlyConfig config, SourceProductionContext context)
        {
            string routePrefix = config.Api.RoutePrefix;
            List<string> result = new();

            foreach (IGrouping<string, SpiderlyClass> groupedControllerEntities in allEntities.GroupBy(x => x.ControllerName))
            {
                result.Add($$"""
{{GetControllerAttributes(groupedControllerEntities, customControllers, routePrefix)}}
    public class {{groupedControllerEntities.Key}}BaseController : SpiderlyBaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly IServiceProvider _serviceProvider;
        private readonly IStringLocalizer _localizer;

        public {{groupedControllerEntities.Key}}BaseController(
            IApplicationDbContext context,
            IServiceProvider serviceProvider,
            IStringLocalizer localizer
        )
        {
            _context = context;
            _serviceProvider = serviceProvider;
            _localizer = localizer;
        }

{{string.Join("\n\n", GetControllerMethods(groupedControllerEntities.ToList(), allEntities, context))}}

    }
""");
            }

            return result;
        }

        private static string GetControllerAttributes(IGrouping<string, SpiderlyClass> groupedControllerEntities, List<SpiderlyClass> customControllers, string routePrefix)
        {
            if (customControllers.Any(x => x.BaseType == $"{groupedControllerEntities.Key}BaseController"))
                return null;

            return $$"""
    [ApiController]
    [Route("{{routePrefix}}/{{groupedControllerEntities.Key}}/[action]")]
""";
        }

        private static List<string> GetControllerMethods(List<SpiderlyClass> groupedControllerEntities, List<SpiderlyClass> allEntities, SourceProductionContext context)
        {
            List<string> result = new();

            foreach (SpiderlyClass controllerEntity in groupedControllerEntities)
            {
                if (controllerEntity.IsManyToMany()) // TODO FT: Do something with M2M entities
                    continue;

                string entityRegion;
                try
                {
                    string referencedProjectEntityClassIdType = controllerEntity.GetIdType(allEntities);
                    string entityServiceField = $"_serviceProvider.GetRequiredService<{controllerEntity.Name}ServiceGenerated>()";

                    entityRegion = $$"""
        #region {{controllerEntity.Name}}

        #region Read

        /// <summary>
        /// Returns a paginated list of {{controllerEntity.Name}} records.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        public virtual async Task<PaginatedResultDTO<{{controllerEntity.Name}}DTO>> GetPaginated{{controllerEntity.Name}}List(FilterDTO filterDTO)
        {
            return await {{entityServiceField}}.GetPaginated{{controllerEntity.Name}}List(filterDTO, _context.DbSet<{{controllerEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
        }

        /// <summary>
        /// Exports {{controllerEntity.Name}} records to an Excel file.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        public virtual async Task<IActionResult> Export{{controllerEntity.Name}}ListToExcel(FilterDTO filterDTO)
        {
            byte[] fileContent = await {{entityServiceField}}.Export{{controllerEntity.Name}}ListToExcel(filterDTO, _context.DbSet<{{controllerEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
            return File(
                fileContent,
                Spiderly.Shared.SettingsProvider.Current.ExcelContentType,
                Uri.EscapeDataString($"{_localizer.GetExcelTranslation("{{controllerEntity.Name}}ExcelExportName", "{{controllerEntity.Name}}List")}.xlsx")
            );
        }

        /// <summary>
        /// Returns all {{controllerEntity.Name}} records (non-paginated).
        /// </summary>
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<{{controllerEntity.Name}}DTO>> Get{{controllerEntity.Name}}List()
        {
            return await {{entityServiceField}}.Get{{controllerEntity.Name}}DTOList(_context.DbSet<{{controllerEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
        }

        /// <summary>
        /// Returns the full form data for a single {{controllerEntity.Name}}, including related entities.
        /// </summary>
        [HttpGet]
        [AuthGuard]
        public virtual async Task<{{controllerEntity.Name}}MainUIFormDTO> Get{{controllerEntity.Name}}MainUIFormDTO({{referencedProjectEntityClassIdType}} id)
        {
            return await {{entityServiceField}}.Get{{controllerEntity.Name}}MainUIFormDTO(id, {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
        }

        /// <summary>
        /// Returns a single {{controllerEntity.Name}} by ID.
        /// </summary>
        [HttpGet]
        [AuthGuard]
        public virtual async Task<{{controllerEntity.Name}}DTO> Get{{controllerEntity.Name}}({{referencedProjectEntityClassIdType}} id)
        {
            return await {{entityServiceField}}.Get{{controllerEntity.Name}}DTO(id, {{Helpers.GetShouldAuthorizeEntityString(controllerEntity)}});
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
""";
                }
                catch (SpiderlyGenerationException ex)
                {
                    context.ReportDiagnostic(ex.Diagnostic);
                    continue;
                }

                result.Add(entityRegion);
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
            SpiderlyClass manyToOneEntity = Helpers.GetEntityByPropertyType(property, allEntities);

            if (manyToOneEntity == null)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ControllerPropertyTypeUnresolvable,
                    property.Location ?? entity.Location,
                    property.EntityName, property.Name, property.Type);
            }

            string manyToOneEntityIdType = manyToOneEntity.GetIdType(allEntities);
            string manyToOneDisplayName = ClassAnalyzer.GetDisplayNameProperty(manyToOneEntity);

            return $$"""
        /// <summary>
        /// Returns autocomplete options for {{property.Name}} on {{entity.Name}}.
        /// </summary>
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<NamebookDTO<{{manyToOneEntityIdType}}>>> Get{{property.Name}}AutocompleteListFor{{entity.Name}}(int limit, string filter, {{entity.GetIdType(allEntities)}}? {{entity.Name.FirstCharToLower()}}Id)
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().Get{{property.Name}}AutocompleteListFor{{entity.Name}}(
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
            SpiderlyClass manyToOneEntity = Helpers.GetEntityByPropertyType(property, allEntities);

            if (manyToOneEntity == null)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ControllerPropertyTypeUnresolvable,
                    property.Location ?? entity.Location,
                    property.EntityName, property.Name, property.Type);
            }

            string manyToOneEntityIdType = manyToOneEntity.GetIdType(allEntities);
            string manyToOneDisplayName = ClassAnalyzer.GetDisplayNameProperty(manyToOneEntity);

            return $$"""
        /// <summary>
        /// Returns dropdown options for {{property.Name}} on {{entity.Name}}.
        /// </summary>
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<NamebookDTO<{{manyToOneEntityIdType}}>>> Get{{property.Name}}DropdownListFor{{entity.Name}}({{entity.GetIdType(allEntities)}}? {{entity.Name.FirstCharToLower()}}Id)
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().Get{{property.Name}}DropdownListFor{{entity.Name}}(
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
            SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);
            string extractedEntityIdType = extractedEntity.GetIdType(entities);

            return $$"""
        /// <summary>
        /// Returns a paginated list of {{property.Name}} for {{entity.Name}}.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        public virtual async Task<PaginatedResultDTO<{{extractedEntity.Name}}DTO>> GetPaginated{{property.Name}}ListFor{{entity.Name}}(FilterDTO filterDTO)
        {
            return await _serviceProvider.GetRequiredService<{{extractedEntity.Name}}ServiceGenerated>().GetPaginated{{extractedEntity.Name}}List(filterDTO, _context.DbSet<{{extractedEntity.Name}}>().OrderBy(x => x.Id), false);
        }

        /// <summary>
        /// Exports {{property.Name}} for {{entity.Name}} to an Excel file.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        public virtual async Task<IActionResult> Export{{property.Name}}ListToExcelFor{{entity.Name}}(FilterDTO filterDTO)
        {
            byte[] fileContent = await _serviceProvider.GetRequiredService<{{extractedEntity.Name}}ServiceGenerated>().Export{{extractedEntity.Name}}ListToExcel(filterDTO, _context.DbSet<{{extractedEntity.Name}}>(), false);
            return File(
                fileContent,
                Spiderly.Shared.SettingsProvider.Current.ExcelContentType,
                Uri.EscapeDataString($"{_localizer.GetExcelTranslation("{{extractedEntity.Name}}ExcelExportName", "{{extractedEntity.Name}}List")}.xlsx")
            );
        }

        /// <summary>
        /// Returns selected {{property.Name}} IDs for {{entity.Name}} with lazy loading support.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        public virtual async Task<LazyLoadSelectedIdsResultDTO<{{extractedEntityIdType}}>> LazyLoadSelected{{property.Name}}IdsFor{{entity.Name}}(FilterDTO filterDTO)
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().LazyLoadSelected{{property.Name}}IdsFor{{entity.Name}}(filterDTO, _context.DbSet<{{extractedEntity.Name}}>().OrderBy(x => x.Id), {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }
""";
        }

        private static string GetComplexManyToManyReadonlyTableControllerMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

            return $$"""
        /// <summary>
        /// Returns a paginated list of {{property.Name}} for {{entity.Name}}.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        public virtual async Task<PaginatedResultDTO<{{extractedEntity.Name}}DTO>> GetPaginated{{property.Name}}ListFor{{entity.Name}}(FilterDTO filterDTO)
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().GetPaginated{{property.Name}}ListFor{{entity.Name}}(filterDTO, _context.DbSet<{{extractedEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }

        /// <summary>
        /// Exports {{property.Name}} for {{entity.Name}} to an Excel file.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        public virtual async Task<IActionResult> Export{{property.Name}}ListToExcelFor{{entity.Name}}(FilterDTO filterDTO)
        {
            byte[] fileContent = await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().Export{{property.Name}}ListToExcelFor{{entity.Name}}(filterDTO, _context.DbSet<{{extractedEntity.Name}}>(), {{Helpers.GetShouldAuthorizeEntityString(entity)}});
            return File(
                fileContent,
                Spiderly.Shared.SettingsProvider.Current.ExcelContentType,
                Uri.EscapeDataString($"{_localizer.GetExcelTranslation("{{extractedEntity.Name}}ExcelExportName", "{{extractedEntity.Name}}List")}.xlsx")
            );
        }
""";
        }

        private static string GetManyToManySelectedEntitiesControllerMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

            return $$"""
        /// <summary>
        /// Returns selected {{property.Name}} for {{entity.Name}}.
        /// </summary>
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<NamebookDTO<{{extractedEntity.GetIdType(entities)}}>>> Get{{property.Name}}NamebookListFor{{entity.Name}}({{entity.GetIdType(entities)}} id)
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().Get{{property.Name}}NamebookListFor{{entity.Name}}(id, false);
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
        /// <summary>
        /// Returns ordered {{property.Name}} for {{entity.Name}}.
        /// </summary>
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<{{Helpers.ExtractTypeFromGenericType(property.Type)}}MainUIFormDTO>> GetOrdered{{property.Name}}For{{entity.Name}}({{entity.GetIdType(entities)}} id)
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().GetOrdered{{property.Name}}For{{entity.Name}}(id, {{Helpers.GetShouldAuthorizeEntityString(entity)}});
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
        /// <summary>
        /// Returns default {{property.Name}} for {{entity.Name}}.
        /// </summary>
        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<{{junctionEntity.Name}}DTO>> GetDefault{{property.Name}}For{{entity.Name}}()
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().GetDefault{{property.Name}}For{{entity.Name}}();
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

            string entityIdType = entity.GetIdType(entities);

            return $$"""
        /// <summary>
        /// Deletes a {{entity.Name}} by ID.
        /// </summary>
        [HttpDelete]
        [AuthGuard]
        public virtual async Task Delete{{entity.Name}}({{entityIdType}} id)
        {
            await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().Delete{{entity.Name}}(id, {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }

        /// <summary>
        /// Deletes multiple {{entity.Name}} entities by their IDs.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        public virtual async Task Delete{{entity.Name}}List([FromBody] List<{{entityIdType}}> ids)
        {
            await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().Delete{{entity.Name}}List(ids, {{Helpers.GetShouldAuthorizeEntityString(entity)}});
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
        /// <summary>
        /// Creates or updates a {{entity.Name}}.
        /// </summary>
        [HttpPut]
        [AuthGuard]
        public virtual async Task<{{entity.Name}}MainUIFormDTO> Save{{entity.Name}}({{entity.Name}}SaveBodyDTO saveBodyDTO)
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().Save{{entity.Name}}AndReturnMainUIFormDTO(saveBodyDTO, {{Helpers.GetShouldAuthorizeEntityString(entity)}}, {{Helpers.GetShouldAuthorizeEntityString(entity)}});
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
        /// <summary>
        /// Uploads a {{property.Name}} file for {{entity.Name}}.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        [EnableRateLimiting(SpiderlyRateLimitPolicies.BlobUpload)]
        public virtual async Task<string> Upload{{property.Name}}For{{entity.Name}}([FromForm] IFormFile file) // FT: It doesn't work without interface
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().Upload{{property.Name}}For{{entity.Name}}(file, {{Helpers.GetShouldAuthorizeEntityString(entity)}}, {{Helpers.GetShouldAuthorizeEntityString(entity)}}); // TODO: Make authorization in business service with override
        }
"""
);
            }

            return result;
        }

        private static List<string> GetUploadEditorImageControllerMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            List<SpiderlyProperty> editorProperties = Helpers.GetEditorImageProperties(entity.Properties);

            foreach (SpiderlyProperty property in editorProperties)
            {
                result.Add($$"""
        /// <summary>
        /// Uploads a {{property.Name}} image for {{entity.Name}}.
        /// </summary>
        [HttpPost]
        [AuthGuard]
        [EnableRateLimiting(SpiderlyRateLimitPolicies.BlobUpload)]
        public virtual async Task<EditorImageUploadResultDTO> Upload{{property.Name}}ImageFor{{entity.Name}}([FromForm] IFormFile file)
        {
            return await _serviceProvider.GetRequiredService<{{entity.Name}}ServiceGenerated>().Upload{{property.Name}}ImageFor{{entity.Name}}(file, {{Helpers.GetShouldAuthorizeEntityString(entity)}}, {{Helpers.GetShouldAuthorizeEntityString(entity)}});
        }
""");
            }

            return result;
        }

        #endregion
    }
}
