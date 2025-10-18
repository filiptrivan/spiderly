using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.Diagnostics;
using System.Reflection;

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

            var allClasses = classDeclarations.Collect()
                .Combine(referencedProjectClasses);

            context.RegisterImplementationSourceOutput(allClasses, static (spc, source) => Execute(source.Left, source.Right, spc));
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, SourceProductionContext context)
        {
            if (classes.Count <= 1)
                return;

            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectEntities);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);
            string projectName = Helpers.GetProjectName(namespaceValue);

            bool isSecurityProject = projectName == "Security";

            string result = $$"""
{{GetUsings(basePartOfNamespace, projectName)}}

namespace {{basePartOfNamespace}}.Services
{
    {{(isSecurityProject ? $"public class BusinessServiceGenerated<TUser> : BusinessServiceBase where TUser : class, IUser, new()" : $"public class BusinessServiceGenerated : BusinessServiceBase")}}
    {
        private readonly IApplicationDbContext _context;
        private readonly ExcelService _excelService;
        {{(isSecurityProject ? "private readonly AuthorizationBusinessService<TUser> _authorizationService;" : "private readonly AuthorizationBusinessService _authorizationService;")}}
        private readonly IFileManager _fileManager;

        public BusinessServiceGenerated(
            IApplicationDbContext context, 
            ExcelService excelService, 
            {{(isSecurityProject ? "AuthorizationBusinessService<TUser> authorizationService" : "AuthorizationBusinessService authorizationService")}}, 
            IFileManager fileManager
        )
            : base(context)
        {
            _context = context;
            _excelService = excelService;
            _authorizationService = authorizationService;
            _fileManager = fileManager;
        }

{{string.Join("\n\n", GetBusinessServiceMethods(currentProjectEntities, allEntities, projectName))}}

    }
}
""";

            context.AddSource($"BusinessService.generated", SourceText.From(result, Encoding.UTF8));
        }

        private static List<string> GetBusinessServiceMethods(List<SpiderlyClass> entityClasses, List<SpiderlyClass> allEntityClasses, string projectName)
        {
            List<string> result = new();

            foreach (SpiderlyClass entity in entityClasses)
            {
                if (entity.IsManyToMany())
                {
                    result.Add($$"""
        #region {{entity.Name}} - M2M

{{GetManyToManyData(entity, allEntityClasses)}}

        #endregion
""");
                }
                else
                {
                    result.Add($$"""
        #region {{entity.Name}}

        #region Read

{{GetReadBusinessServiceMethods(entity, allEntityClasses, projectName)}}

        #endregion

        #region Save

{{GetSavingData(entity, allEntityClasses)}}

{{string.Join("\n\n", GetUploadBlobMethods(entity, allEntityClasses))}}
        
        #endregion

        #region Delete

{{string.Join("\n\n", GetDeletingData(entity, allEntityClasses))}}

        #endregion

        #region One To Many

{{string.Join("\n\n", GetOneToManyMethods(entity, allEntityClasses))}}

        #endregion

        #endregion
""");
                }
            }

            return result;
        }

        #region Read

        private static string GetReadBusinessServiceMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities, string projectName)
        {
            string entityIdType = entity.GetIdType(allEntities);

            return $$"""
        public async virtual Task<{{entity.Name}}MainUIFormDTO> Get{{entity.Name}}MainUIFormDTO({{entityIdType}} id, bool authorize)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, "id")}}
                }

                return new {{entity.Name}}MainUIFormDTO
                {
{{GetMainUIFormDTOInitializationProperties(entity, allEntities)}}
                };
            });
        }

        public async Task<{{entity.Name}}DTO> Get{{entity.Name}}DTO({{entityIdType}} id, bool authorize)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, "id")}}
                }

                var dto = await _context.DbSet<{{entity.Name}}>()
                    .AsNoTracking()
                    .Where(x => x.Id == id).ProjectToType<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ProjectToConfig())
                    .SingleOrDefaultAsync();

                if (dto == null)
                    throw new BusinessException(SharedTerms.EntityDoesNotExistInDatabase);

{{GetPopulateDTOWithBlobPartsForDTO(entity, entity.Properties)}}

                return dto;
            });
        }

        public async Task<PaginatedResult<{{entity.Name}}>> GetPaginated{{entity.Name}}List(FilterDTO filterDTO, IQueryable<{{entity.Name}}> query)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        public async virtual Task<PaginatedResultDTO<{{entity.Name}}DTO>> GetPaginated{{entity.Name}}List(FilterDTO filterDTO, IQueryable<{{entity.Name}}> query, bool authorize)
        {
            PaginatedResult<{{entity.Name}}> paginationResult = new();
            List<{{entity.Name}}DTO> dtoList = null;

            await _context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginated{{entity.Name}}List(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ProjectToConfig())
                    .ToListAsync();

                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, "dtoList.Select(x => x.Id).ToList()")}}
                }

{{GetPopulateDTOWithBlobPartsForDTOList(entity, entity.Properties)}}
            });

            return new PaginatedResultDTO<{{entity.Name}}DTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        public async Task<byte[]> Export{{entity.Name}}ListToExcel(FilterDTO filterDTO, IQueryable<{{entity.Name}}> query, bool authorize)
        {
            PaginatedResult<{{entity.Name}}> paginationResult = new();
            List<{{entity.Name}}DTO> dtoList = null;

            await _context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginated{{entity.Name}}List(filterDTO, query);

                dtoList = await paginationResult.Query.ProjectToType<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ExcelProjectToConfig()).ToListAsync();

                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, "dtoList.Select(x => x.Id).ToList()")}}
                }
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new {{entity.Name}}DTO());
            return _excelService.FillReportTemplate<{{entity.Name}}DTO>(dtoList, paginationResult.TotalRecords, excelPropertiesToExclude, {{GetTermsClassName(projectName)}}.ResourceManager).ToArray();
        }

        public async Task<List<{{entity.Name}}>> Get{{entity.Name}}List(IQueryable<{{entity.Name}}> query, bool authorize)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                var result = await query
                    .ToListAsync();

                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, "result.Select(x => x.Id).ToList()")}}
                }

                return result;
            });
        }

        public async Task<List<{{entity.Name}}DTO>> Get{{entity.Name}}DTOList(IQueryable<{{entity.Name}}> query, bool authorize)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                var dtoList = await query
                    .AsNoTracking()
                    .ProjectToType<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ToDTOConfig())
                    .ToListAsync();

                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, "dtoList.Select(x => x.Id).ToList()")}}
                }

{{GetPopulateDTOWithBlobPartsForDTOList(entity, entity.Properties)}}

                return dtoList;
            });
        }

{{GetManyToOneReadMethods(entity, allEntities)}}
""";
        }

        private static string GetMainUIFormDTOInitializationProperties(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            result.Add($$"""
                    {{entity.Name}}DTO = await Get{{entity.Name}}DTO(id, false),
""");

            foreach (SpiderlyProperty property in entity.Properties)
            {
                SpiderlyClass extractedEntity = allEntities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();
                string extractedEntityIdType = extractedEntity.GetIdType(allEntities);

                if (property.HasUIOrderedOneToManyAttribute())
                {
                    result.Add($$"""
                    Ordered{{property.Name}}DTO = await GetOrdered{{property.Name}}For{{entity.Name}}(id, false),
""");
                }
                else if (
                    property.IsMultiSelectControlType() ||
                    property.IsMultiAutocompleteControlType())
                {
                    result.Add($$"""
                    {{property.Name}}NamebookDTOList = await Get{{property.Name}}NamebookListFor{{entity.Name}}(id, false),
""");
                }
            }

            return string.Join("\n", result);
        }

        private static string GetTermsClassName(string projectName)
        {
            return projectName == "Security" ? "SharedTerms" : "TermsGenerated";
        }

        private static string GetPopulateDTOWithBlobPartsForDTO(SpiderlyClass entityClass, List<SpiderlyProperty> propertiesEntityClass)
        {
            List<string> blobParts = GetPopulateDTOWithBlobParts(propertiesEntityClass);

            if (blobParts.Count == 0)
                return null;

            return $$"""
                {{string.Join("\n", blobParts)}}
""";
        }

        private static string GetPopulateDTOWithBlobPartsForDTOList(SpiderlyClass entityClass, List<SpiderlyProperty> propertiesEntityClass)
        {
            List<string> blobParts = GetPopulateDTOWithBlobParts(propertiesEntityClass);

            if (blobParts.Count == 0)
                return null;

            return $$"""
                foreach ({{entityClass.Name}}DTO dto in dtoList)
                {
                    {{string.Join("\n", blobParts)}}
                }
""";
        }

        private static List<string> GetPopulateDTOWithBlobParts(List<SpiderlyProperty> propertiesEntityClass)
        {
            List<string> blobParts = new();

            List<SpiderlyProperty> blobProperies = Helpers.GetBlobProperties(propertiesEntityClass);

            foreach (SpiderlyProperty property in blobProperies)
            {
                blobParts.Add($$"""
                    if (!string.IsNullOrEmpty(dto.{{property.Name}}))
                    {
                        dto.{{property.Name}}Data = await _fileManager.GetFileDataAsync(dto.{{property.Name}});
                    }
""");
            }

            return blobParts;
        }

        #region Many To One

        public static string GetManyToOneReadMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
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
            SpiderlyClass autocompleteEntity = allEntities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).Single();
            string autocompleteEntityIdType = autocompleteEntity.GetIdType(allEntities);
            string autocompleteEntityDisplayName = Helpers.GetDisplayNameProperty(autocompleteEntity);

            return $$"""
        public async virtual Task<List<NamebookDTO<{{autocompleteEntityIdType}}>>> Get{{property.Name}}AutocompleteListFor{{entity.Name}}(
            int limit, 
            string filter, 
            IQueryable<{{autocompleteEntity.Name}}> query, 
            bool authorize,
            {{entity.GetIdType(allEntities)}}? {{entity.Name.FirstCharToLower()}}Id = null
        )
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, $"{entity.Name.FirstCharToLower()}Id")}}
                }

                if (!string.IsNullOrEmpty(filter))
                    query = query.Where(x => x.{{autocompleteEntityDisplayName}}.Contains(filter));

                var result = await query
                    .AsNoTracking()
                    .Take(limit)
                    .Select(x => new NamebookDTO<{{autocompleteEntityIdType}}>
                    {
                        Id = x.Id,
                        DisplayName = x.{{autocompleteEntityDisplayName}},
                    })
                    .ToListAsync();

                return result;
            });
        }
""";
        }

        private static string GetDropdownMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            SpiderlyClass dropdownEntity = allEntities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).Single();
            string dropdownEntityIdType = dropdownEntity.GetIdType(allEntities);
            string dropdownDisplayName = Helpers.GetDisplayNameProperty(dropdownEntity);

            return $$"""
        public async virtual Task<List<NamebookDTO<{{dropdownEntityIdType}}>>> Get{{property.Name}}DropdownListFor{{entity.Name}}(
            IQueryable<{{dropdownEntity.Name}}> query, 
            bool authorize,
            {{entity.GetIdType(allEntities)}}? {{entity.Name.FirstCharToLower()}}Id = null
        )
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, $"{entity.Name.FirstCharToLower()}Id")}}
                }

                var result = await query
                    .AsNoTracking()
                    .Select(x => new NamebookDTO<{{dropdownEntityIdType}}>
                    {
                        Id = x.Id,
                        DisplayName = x.{{dropdownDisplayName}},
                    })
                    .ToListAsync();

                return result;
            });
        }
""";
        }

        #endregion

        #endregion

        #region One To Many

        static List<string> GetOneToManyMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            string entityIdType = entity.GetIdType(entities);

            List<string> result = new();

            foreach (SpiderlyProperty oneToManyProperty in entity.Properties.Where(prop => prop.Type.IsOneToManyType())) // List<Role> Roles
            {
                SpiderlyClass extractedPropertyEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(oneToManyProperty.Type)).Single(); // Role

                string extractedPropertyEntityIdType = extractedPropertyEntity.GetIdType(entities); // int

                if (extractedPropertyEntityIdType == null) // M2M List, maybe do something else in the future.
                    continue;

                string extractedPropertyEntityDisplayName = Helpers.GetDisplayNameProperty(extractedPropertyEntity); // Name

                SpiderlyProperty extractedEntityManyToManyProperty = Helpers.GetOppositeManyToManyProperty(oneToManyProperty, extractedPropertyEntity, entity, entities);
                SpiderlyProperty manyToOneProperty = extractedPropertyEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, oneToManyProperty.Name);

                if (manyToOneProperty != null)
                {
                    result.Add($$"""
{{GetOneToManyNamebookListForEntity(oneToManyProperty, extractedPropertyEntity, manyToOneProperty, entity, entities)}}

{{GetOneToManyListForEntity(oneToManyProperty, extractedPropertyEntity, manyToOneProperty, entity, entities)}}

{{GetOrderedOneToManyMethod(oneToManyProperty, entity, entities)}}
""");
                }
                else if (extractedEntityManyToManyProperty != null)
                {
                    result.Add($$"""
        public async virtual Task<List<NamebookDTO<{{extractedPropertyEntityIdType}}>>> Get{{oneToManyProperty.Name}}NamebookListFor{{entity.Name}}({{entityIdType}} id, bool authorize)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, "id")}}
                }

                return await _context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .AsNoTracking()
                    .Where(x => x.{{extractedEntityManyToManyProperty.Name}}.Any(x => x.Id == id))
                    .Select(x => new NamebookDTO<{{extractedPropertyEntityIdType}}>
                    {
                        Id = x.Id,
                        DisplayName = x.{{extractedPropertyEntityDisplayName}},
                    })
                    .ToListAsync();
            });
        }

        public async Task<List<{{extractedPropertyEntity.Name}}>> Get{{oneToManyProperty.Name}}For{{entity.Name}}({{entityIdType}} id)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .Where(x => x.{{extractedEntityManyToManyProperty.Name}}.Any(x => x.Id == id))
                    .ToListAsync();
            });
        }

        public async Task Update{{oneToManyProperty.Name}}For{{entity.Name}}({{entityIdType}} id, List<{{extractedPropertyEntityIdType}}> selectedIds)
        {
            if (selectedIds == null)
                return;

            List<{{extractedPropertyEntityIdType}}> selectedIdsHelper = selectedIds.ToList();

            await _context.WithTransactionAsync(async () =>
            {
                // FT: Not doing authorization here, because we can not figure out here if we are updating while inserting object (eg. User), or updating object, we will always get the id which is not 0 here.

                {{((entity.IsBusinessObject() || entity.IsReadonlyObject() == false)
                ? $"var entity = await GetInstanceAsync<{entity.Name}, {entityIdType}>(id, null); // FT: Version will always be checked before or after this method"
                : $"var entity = await GetInstanceAsync<{entity.Name}, {entityIdType}>(id);"
                )}}
                
                foreach ({{extractedPropertyEntity.Name}} item in entity.{{oneToManyProperty.Name}}.ToList())
                {
                    if (selectedIdsHelper.Contains(item.Id))
                        selectedIdsHelper.Remove(item.Id);
                    else
                        entity.{{oneToManyProperty.Name}}.Remove(item);
                }

                var listToInsert = await _context.DbSet<{{extractedPropertyEntity.Name}}>().Where(x => selectedIdsHelper.Contains(x.Id)).ToListAsync();

                entity.{{oneToManyProperty.Name}}.AddRange(listToInsert);
                await _context.SaveChangesAsync();
            });
        }

        /// <summary>
        /// It's mandatory to pass queryable ordered by the same field as the table data
        /// </summary>
        public async Task<LazyLoadSelectedIdsResultDTO<{{extractedPropertyEntityIdType}}>> LazyLoadSelected{{oneToManyProperty.Name}}IdsFor{{entity.Name}}(FilterDTO filterDTO, IQueryable<{{extractedPropertyEntity.Name}}> query, bool authorize)
        {
            LazyLoadSelectedIdsResultDTO<{{extractedPropertyEntityIdType}}> lazyLoadSelectedIdsResultDTO = new();

            query = query
                .Skip(filterDTO.First)
                .Take(filterDTO.Rows)
                .Where(x => x.{{extractedEntityManyToManyProperty.Name}}
                    .Any(x => x.Id == filterDTO.{{entityIdType.GetTableFilterAdditionalFilterPropertyName()}}));

            await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, $"({entityIdType})filterDTO.{entityIdType.GetTableFilterAdditionalFilterPropertyName()}")}}
                }

                var paginationResult = await GetPaginated{{extractedPropertyEntity.Name}}List(filterDTO, query);

                lazyLoadSelectedIdsResultDTO.SelectedIds = await paginationResult.Query
                    .Select(x => x.Id)
                    .ToListAsync();

                int count = await _context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .Where(x => x.{{extractedEntityManyToManyProperty.Name}}
                        .Any(x => x.Id == filterDTO.{{entityIdType.GetTableFilterAdditionalFilterPropertyName()}}))
                    .CountAsync();

                lazyLoadSelectedIdsResultDTO.TotalRecordsSelected = count;
            });

            return lazyLoadSelectedIdsResultDTO;
        }

{{GetSimpleManyToManyUpdateWithLazyTableSelectionMethod(oneToManyProperty, entity, entities)}}
""");
                }
                else if (extractedPropertyEntity == null)
                {
                    result.Add("Invalid entity class, you can't have List<Entity> without List<AssociationEntity> or AssociationEntity on the other side."); // He can (User/Role example, many to many on the one side)
                }

            }

            return result;
        }

        private static string GetOneToManyListForEntity(SpiderlyProperty oneToManyProperty, SpiderlyClass extractedPropertyEntity, SpiderlyProperty manyToOneProperty, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            return $$"""
        public async Task<List<{{extractedPropertyEntity.Name}}>> Get{{oneToManyProperty.Name}}For{{entity.Name}}({{entity.GetIdType(entities)}} id)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .Where(x => x.{{manyToOneProperty.Name}}.Id == id)
                    .ToListAsync();
            });
        }
""";
        }

        private static string GetOneToManyNamebookListForEntity(SpiderlyProperty oneToManyProperty, SpiderlyClass extractedPropertyEntity, SpiderlyProperty manyToOneProperty, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            string extractedPropertyEntityIdType = extractedPropertyEntity.GetIdType(entities); // int

            return $$"""
        public async virtual Task<List<NamebookDTO<{{extractedPropertyEntityIdType}}>>> Get{{oneToManyProperty.Name}}NamebookListFor{{entity.Name}}({{entity.GetIdType(entities)}} id, bool authorize)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, "id")}}
                }

                return await _context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .AsNoTracking()
                    .Where(x => x.{{manyToOneProperty.Name}}.Id == id)
                    .Select(x => new NamebookDTO<{{extractedPropertyEntityIdType}}>
                    {
                        Id = x.Id,
                        DisplayName = x.{{Helpers.GetDisplayNameProperty(extractedPropertyEntity)}},
                    })
                    .ToListAsync();
            });
        }
""";
        }

        private static string GetSimpleManyToManyUpdateWithLazyTableSelectionMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            if (property.HasSimpleManyToManyTableLazyLoadAttribute() == false)
                return null;

            string entityIdType = entity.GetIdType(entities);
            SpiderlyClass extractedPropertyEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).Single(); // Role

            string extractedPropertyEntityIdType = extractedPropertyEntity.GetIdType(entities); // int

            return $$"""
        public async Task Update{{property.Name}}WithLazyTableSelectionFor{{entity.Name}}(IQueryable<{{extractedPropertyEntity.Name}}> query, {{entityIdType}} id, {{entity.Name}}SaveBodyDTO saveBodyDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                List<{{extractedPropertyEntityIdType}}> listToInsert = null;

                if (saveBodyDTO.AreAll{{property.Name}}Selected == true)
                {
                    listToInsert = await query.Where(x => saveBodyDTO.Unselected{{property.Name}}Ids.Contains(x.Id) == false).Select(x => x.Id).ToListAsync();
                }
                else if (saveBodyDTO.AreAll{{property.Name}}Selected == false)
                {
                    listToInsert = await query.Where(x => saveBodyDTO.Selected{{property.Name}}Ids.Contains(x.Id) == true).Select(x => x.Id).ToListAsync();
                }
                else if (saveBodyDTO.AreAll{{property.Name}}Selected == null)
                {
                    {{((entity.IsBusinessObject() || entity.IsReadonlyObject() == false)
                    ? $"var entity = await GetInstanceAsync<{entity.Name}, {entityIdType}>(id, null); // FT: Version will always be checked before or after this method"
                    : $"var entity = await GetInstanceAsync<{entity.Name}, {entityIdType}>(id);"
                    )}}

                    var alreadySelected = entity.{{property.Name}} == null ? new List<{{extractedPropertyEntityIdType}}>() : entity.{{property.Name}}.Select(x => x.Id).ToList();

                    listToInsert = alreadySelected
                        .Union(saveBodyDTO.Selected{{property.Name}}Ids)
                        .Except(saveBodyDTO.Unselected{{property.Name}}Ids)
                        .ToList();
                }

                await Update{{property.Name}}For{{entity.Name}}(id, listToInsert);
            });
        }
""";
        }

        private static string GetOrderedOneToManyMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            if (property.HasUIOrderedOneToManyAttribute() == false)
                return null;

            SpiderlyClass extractedPropertyEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).Single();
            SpiderlyProperty manyToOneProperty = extractedPropertyEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, property.Name);

            return $$"""
        public async Task<List<{{extractedPropertyEntity.Name}}DTO>> GetOrdered{{property.Name}}For{{entity.Name}}({{entity.GetIdType(entities)}} id, bool authorize)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                var query = _context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .Where(x => x.{{manyToOneProperty.Name}}.Id == id)
                    .OrderBy(x => x.OrderNumber);

                return await Get{{extractedPropertyEntity.Name}}DTOList(query, authorize);
            });
        }
""";
        }

        #endregion

        #region Save

        static string GetSavingData(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            if (entity.IsAbstract || entity.IsReadonlyObject())
                return null;

            string entityIdType = entity.GetIdType(entities);

            return $$"""
{{GetSaveAndReturnSaveBodyDTOData(entity, entities)}}

        public async Task<{{entity.Name}}DTO> Save{{entity.Name}}AndReturnDTO({{entity.Name}}DTO dto, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                var poco = await Save{{entity.Name}}(dto, authorizeUpdate, authorizeInsert);

                return poco.Adapt<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ToDTOConfig());
            });
        }

        public async Task<{{entity.Name}}> Save{{entity.Name}}({{entity.Name}}DTO dto, bool authorizeUpdate, bool authorizeInsert)
        {
            {{entity.Name}}DTOValidationRules validationRules = new {{entity.Name}}DTOValidationRules();
            validationRules.ValidateAndThrow(dto);

            {{entity.Name}} poco = null;
            await _context.WithTransactionAsync(async () =>
            {
                await OnBefore{{entity.Name}}IsMapped(dto);
                DbSet<{{entity.Name}}> dbSet = _context.DbSet<{{entity.Name}}>();

                if (dto.Id > 0)
                {
                    if (authorizeUpdate)
                    {
                        {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Update, "dto")}}
                    }

                    poco = await GetInstanceAsync<{{entity.Name}}, {{entityIdType}}>(dto.Id, dto.Version);
                    await OnBefore{{entity.Name}}Update(poco, dto);
                    dto.Adapt(poco, Mapper.{{entity.Name}}DTOToEntityConfig());
                    dbSet.Update(poco);
                }
                else
                {
                    if (authorizeInsert)
                    {
                        {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Insert, "dto")}}
                    }

                    poco = dto.Adapt<{{entity.Name}}>(Mapper.{{entity.Name}}DTOToEntityConfig());
                    await dbSet.AddAsync(poco);
                }

{{string.Join("\n", GetManyToOneInstancesForSave(entity, entities))}}

                await _context.SaveChangesAsync();

{{string.Join("\n", GetNonActiveDeleteBlobMethods(entity))}}
            });

            return poco;
        }

        protected virtual async Task OnBefore{{entity.Name}}IsMapped({{entity.Name}}DTO {{entity.Name.FirstCharToLower()}}DTO) { }

        protected virtual async Task OnBefore{{entity.Name}}Update({{entity.Name}} {{entity.Name.FirstCharToLower()}}, {{entity.Name}}DTO {{entity.Name.FirstCharToLower()}}DTO) { }
""";
        }

        private static string GetSaveAndReturnSaveBodyDTOData(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            return $$"""
        public virtual async Task<{{entity.Name}}SaveBodyDTO> Save{{entity.Name}}AndReturnSaveBodyDTO({{entity.Name}}SaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                await OnBeforeSave{{entity.Name}}AndReturnSaveBodyDTO(saveBodyDTO);

                var savedDTO = await Save{{entity.Name}}AndReturnDTO(saveBodyDTO.{{entity.Name}}DTO, authorizeUpdate, authorizeInsert);

                await OnAfterSave{{entity.Name}}AndReturnSaveBodyDTO(savedDTO, saveBodyDTO);

{{string.Join("\n", GetOrderedOneToManyUpdateVariables(entity, entities))}}
{{string.Join("\n", GetManyToManyMultiControlTypesUpdateMethods(entity, entities))}}
{{string.Join("\n", GetSimpleManyToManyTableLazyLoad(entity, entities))}}

                var result = new {{entity.Name}}SaveBodyDTO
                {
                    {{entity.Name}}DTO = savedDTO,
{{string.Join(",\n", GetOrderedOneToManySaveBodyDTOVariables(entity, entities))}}
                };

                return result;
            });
        }

{{string.Join("\n", GetOrderedOneToManyUpdateMethods(entity, entities))}}
{{string.Join("\n", GetSimpleManyToManyTableLazyLoadGetAllQueryHook(entity, entities))}}

        protected virtual async Task OnBeforeSave{{entity.Name}}AndReturnSaveBodyDTO({{entity.Name}}SaveBodyDTO saveBodyDTO) { }

        protected virtual async Task OnAfterSave{{entity.Name}}AndReturnSaveBodyDTO({{entity.Name}}DTO savedDTO, {{entity.Name}}SaveBodyDTO saveBodyDTO) { }
""";
        }

        #region Ordered One To Many

        private static List<string> GetOrderedOneToManyUpdateVariables(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                result.Add($$"""
                var saved{{property.Name}}DTO = await UpdateOrdered{{property.Name}}For{{entity.Name}}(savedDTO.Id, saveBodyDTO.{{property.Name}}DTO);
""");
            }

            return result;
        }

        private static List<string> GetOrderedOneToManySaveBodyDTOVariables(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                result.Add($$"""
                    {{property.Name}}DTO = saved{{property.Name}}DTO
""");
            }

            return result;
        }

        private static List<string> GetOrderedOneToManyUpdateMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                result.Add($$"""
        public async Task<List<{{extractedEntity.Name}}DTO>> UpdateOrdered{{property.Name}}For{{entity.Name}}({{entity.GetIdType(entities)}} id, List<{{extractedEntity.Name}}DTO> orderedItemsDTO)
        {
            var orderedItemIds = orderedItemsDTO.Select(x => x.Id).ToList();

{{GetOrderedOneToManyRequiredValidation(property, entity)}}

            return await _context.WithTransactionAsync(async () =>
            {
                await _context.DbSet<{{extractedEntity.Name}}>().Where(x => x.{{extractedEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, property.Name)?.Name}}.Id == id && orderedItemIds.Contains(x.Id) == false).ExecuteDeleteAsync();

                var savedOrderedItemsDTO = new List<{{extractedEntity.Name}}DTO>();

                for (int i = 0; i < orderedItemsDTO.Count; i++)
                {
                    orderedItemsDTO[i].{{extractedEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, property.Name)?.Name}}Id = id;
                    orderedItemsDTO[i].OrderNumber = i + 1;
                    savedOrderedItemsDTO.Add(await Save{{extractedEntity.Name}}AndReturnDTO(orderedItemsDTO[i], false, false));
                }

                return savedOrderedItemsDTO;
            });
        } 
""");
            }

            return result;
        }

        private static string GetOrderedOneToManyRequiredValidation(SpiderlyProperty property, SpiderlyClass entity)
        {
            if (property.HasRequiredAttribute())
            {
                return $$"""
            if (orderedItemIds.Count == 0)
                throw new HackerException("The ordered {{property.Name}} for {{entity.Name}} can't be empty.");
""";

            }

            return null;
        }

        #endregion

        #region Many To Many

        private static List<string> GetManyToManyMultiControlTypesUpdateMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.HasExcludeServiceMethodsFromGenerationAttribute() == false))
            {
                if (property.IsMultiSelectControlType() ||
                    property.IsMultiAutocompleteControlType())
                {
                    result.Add($$"""
                await Update{{property.Name}}For{{entity.Name}}(savedDTO.Id, saveBodyDTO.Selected{{property.Name}}Ids);
""");
                }
            }

            return result;
        }

        private static List<string> GetSimpleManyToManyTableLazyLoad(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.HasSimpleManyToManyTableLazyLoadAttribute())
                {
                    SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                    result.Add($$"""
                var all{{property.Name}}Query = await GetAll{{property.Name}}QueryFor{{entity.Name}}(_context.DbSet<{{extractedEntity.Name}}>());
                var {{property.Name.FirstCharToLower()}}PaginatedResult = await GetPaginated{{extractedEntity.Name}}List(saveBodyDTO.{{property.Name}}TableFilter, all{{property.Name}}Query);
                await Update{{property.Name}}WithLazyTableSelectionFor{{entity.Name}}({{property.Name.FirstCharToLower()}}PaginatedResult.Query, savedDTO.Id, saveBodyDTO);
""");
                }
            }

            return result;
        }

        private static List<string> GetSimpleManyToManyTableLazyLoadGetAllQueryHook(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.HasSimpleManyToManyTableLazyLoadAttribute())
                {
                    SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                    result.Add($$"""
        protected virtual async Task<IQueryable<{{extractedEntity.Name}}>> GetAll{{property.Name}}QueryFor{{entity.Name}}(IQueryable<{{extractedEntity.Name}}> query)
        {
            return query;
        }
""");
                }
            }

            return result;
        }

        #endregion

        static List<string> GetManyToOneInstancesForSave(SpiderlyClass entityClass, List<SpiderlyClass> allEntityClasses)
        {
            List<string> result = new();

            List<SpiderlyProperty> properties = entityClass.Properties
                .Where(prop => prop.Type.IsManyToOneType())
                .ToList();

            foreach (SpiderlyProperty prop in properties)
            {
                SpiderlyClass classOfManyToOneProperty = GetClassOfManyToOneProperty(prop.Type, allEntityClasses);

                if (classOfManyToOneProperty == null)
                    continue;

                if (classOfManyToOneProperty.IsBusinessObject() || classOfManyToOneProperty.IsReadonlyObject() == false)
                {
                    result.Add($$"""
                if (dto.{{prop.Name}}Id > 0)
                {
                    poco.{{prop.Name}} = await GetInstanceAsync<{{prop.Type}}, {{classOfManyToOneProperty.GetIdType(allEntityClasses)}}>(dto.{{prop.Name}}Id.Value, null);
                }
                else
                {
                    var _ = poco.{{prop.Name}}; // HACK
                    poco.{{prop.Name}} = null;
                }
""");
                }
                else
                {
                    result.Add($$"""
                if (dto.{{prop.Name}}Id > 0)
                {
                    poco.{{prop.Name}} = await GetInstanceAsync<{{prop.Type}}, {{classOfManyToOneProperty.GetIdType(allEntityClasses)}}>(dto.{{prop.Name}}Id.Value);
                }
                else
                {
                    var _ = poco.{{prop.Name}}; // HACK
                    poco.{{prop.Name}} = null;
                }
""");
                }
            }

            return result;
        }

        static SpiderlyClass GetClassOfManyToOneProperty(string propType, List<SpiderlyClass> allEntityClasses)
        {
            SpiderlyClass manyToOneclass = allEntityClasses.Where(x => x.Name == propType).SingleOrDefault();

            if (manyToOneclass == null)
                return null;

            return manyToOneclass;
        }

        private static List<string> GetNonActiveDeleteBlobMethods(SpiderlyClass entity)
        {
            List<string> result = new();

            List<SpiderlyProperty> blobProperies = Helpers.GetBlobProperties(entity.Properties);

            foreach (SpiderlyProperty property in blobProperies)
            {
                result.Add($$"""
                await _fileManager.DeleteNonActiveBlobs(dto.{{property.Name}}, nameof({{entity.Name}}), nameof({{entity.Name}}.{{property.Name}}), poco.Id.ToString());
""");
            }

            return result;
        }

        private static List<string> GetUploadBlobMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            string entityIdType = entity.GetIdType(allEntities);

            List<SpiderlyProperty> blobProperies = Helpers.GetBlobProperties(entity.Properties);

            foreach (SpiderlyProperty property in blobProperies)
            {
                result.Add($$"""
        public async Task<string> Upload{{property.Name}}For{{entity.Name}}(IFormFile file, bool authorizeUpdate, bool authorizeInsert) // FT: It doesn't work without interface
        {
            {{entityIdType}} {{entity.Name.FirstCharToLower()}}Id = Helper.GetObjectIdFromFileName<{{entityIdType}}>(file.FileName);

            OnBefore{{property.Name}}BlobFor{{entity.Name}}IsUploaded(file, {{entity.Name.FirstCharToLower()}}Id); // Validate

            if ({{entity.Name.FirstCharToLower()}}Id > 0 && authorizeUpdate)
            {
                {{GetAuthorizeEntityMethodCall($"{property.Name}For{entity.Name}", CrudCodes.Update, $"{entity.Name.FirstCharToLower()}Id")}}
            }
            else if (authorizeInsert)
            {
                {{GetAuthorizeEntityMethodCall($"{property.Name}For{entity.Name}", CrudCodes.Insert, "")}}
            }

            using Stream stream = file.OpenReadStream();

            string fileName = await _fileManager.UploadFileAsync(file.FileName, nameof({{entity.Name}}), nameof({{entity.Name}}.{{property.Name}}), {{entity.Name.FirstCharToLower()}}Id.ToString(), stream);

            return fileName;
        }

        public virtual async Task OnBefore{{property.Name}}BlobFor{{entity.Name}}IsUploaded (IFormFile file, {{entityIdType}} {{entity.Name.FirstCharToLower()}}Id) { }
"""
);
            }

            return result;
        }

        #endregion

        #region Delete

        private static List<string> GetDeletingData(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            if (entity.IsAbstract || entity.IsReadonlyObject())
                return new List<string>();

            List<string> result = new();

            result.Add(GetDeleteEntityData(entity, allEntities));

            result.Add(GetDeleteEntityListData(entity, allEntities));

            return result;
        }

        private static string GetDeleteEntityData(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            string entityIdType = entity.GetIdType(allEntities);
            int deleteIterator = 1;

            return $$"""
        public virtual async Task OnBefore{{entity.Name}}Delete({{entityIdType}} id) { }

        public async Task Delete{{entity.Name}}({{entityIdType}} id, bool authorize)
        {
            await _context.WithTransactionAsync(async () =>
            {
                await OnBefore{{entity.Name}}Delete(id);

                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Delete, "id")}}
                }

                List<{{entityIdType}}> listForDelete_{{deleteIterator}} = id.StructToList();

{{string.Join("\n\n", GetManyToOneDeleteQueries(entity, allEntities, "listForDelete", deleteIterator))}}

                await DeleteEntityAsync<{{entity.Name}}, {{entityIdType}}>(id);
            });
        }
""";
        }

        private static string GetDeleteEntityListData(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            string entityIdType = entity.GetIdType(allEntities);
            int deleteIterator = 1;

            return $$"""
        public virtual async Task OnBefore{{entity.Name}}ListDelete(List<{{entityIdType}}> listForDelete) { }

        public async Task Delete{{entity.Name}}List(List<{{entityIdType}}> listForDelete_{{deleteIterator}}, bool authorize)
        {
            await _context.WithTransactionAsync(async () =>
            {
                await OnBefore{{entity.Name}}ListDelete(listForDelete_{{deleteIterator}});

                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Delete, $"listForDelete_{deleteIterator}")}}
                }

{{string.Join("\n\n", GetManyToOneDeleteQueries(entity, allEntities, "listForDelete", deleteIterator))}}

                await DeleteEntitiesAsync<{{entity.Name}}, {{entityIdType}}>(listForDelete_{{deleteIterator}});
            });
        }
"""; 
        }

        private static List<string> GetManyToOneDeleteQueries(SpiderlyClass entity, List<SpiderlyClass> allEntities, string listForDeleteVariableName, int deleteIterator)
        {
            if (deleteIterator > 5000)
                return new List<string> { "You made cascade delete infinite loop." };

            List<string> result = new();

            List<SpiderlyProperty> cascadeDeleteProperties = Helpers.GetCascadeDeleteProperties(entity.Name, allEntities);

            foreach (SpiderlyProperty property in cascadeDeleteProperties)
            {
                SpiderlyClass parentEntity = allEntities.Where(x => x.Name == property.EntityName).SingleOrDefault();

                if (parentEntity.IsManyToMany())
                {
                    result.Add($$"""
                await _context.DbSet<{{parentEntity.Name}}>()
                    .Where(x => {{listForDeleteVariableName}}_{{deleteIterator}}.Contains(x.{{property.Name}}.Id))
                    .ExecuteDeleteAsync();
""");

                    continue; // FT: Continue because M2M could never be required
                }
                else
                {
                    result.Add($$"""
                var {{parentEntity.Name.FirstCharToLower()}}ListForDeleteBecause{{property.Name}}_{{deleteIterator + 1}} = await _context.DbSet<{{parentEntity.Name}}>()
                    .AsNoTracking()
                    .Where(x => {{listForDeleteVariableName}}_{{deleteIterator}}.Contains(x.{{property.Name}}.Id))
                    .Select(x => x.Id)
                    .ToListAsync();
""");

                }

                result.AddRange(GetManyToOneDeleteQueries(parentEntity, allEntities, $"{parentEntity.Name.FirstCharToLower()}ListForDeleteBecause{property.Name}", deleteIterator + 1));

                result.Add($$"""
                await _context.DbSet<{{parentEntity.Name}}>()
                    .Where(x => {{parentEntity.Name.FirstCharToLower()}}ListForDeleteBecause{{property.Name}}_{{deleteIterator + 1}}.Contains(x.Id))
                    .ExecuteDeleteAsync();
""");
            }

            return result;
        }

        #endregion

        #region M2M

        private static string GetManyToManyData(SpiderlyClass entity, List<SpiderlyClass> allEntityClasses)
        {
            if (entity.Properties.Count == Settings.NumberOfPropertiesWithoutAdditionalManyToManyProperties)
                return null;

            List<SpiderlyProperty> properties = entity.Properties;

            List<SpiderlyProperty> m2mWithManyProperties = properties
                .Where(x => x.Attributes.Any(x => x.Name == "M2MWithMany"))
                .ToList();

            if (m2mWithManyProperties.Count != 2)
                return "YouNeedToDefineTwoM2MWithManyProperties";

            SpiderlyProperty m2mWithManyProperty_1 = m2mWithManyProperties[0];
            SpiderlyAttribute m2mWithManyAttribute_1 = m2mWithManyProperty_1.Attributes.Where(x => x.Name == "M2MWithMany").Single();

            SpiderlyProperty m2mWithManyProperty_2 = properties[1];
            SpiderlyAttribute m2mWithManyAttribute_2 = m2mWithManyProperty_2.Attributes.Where(x => x.Name == "M2MWithMany").Single();

            if (m2mWithManyAttribute_1.Value != m2mWithManyAttribute_2.Value)
                return null;

            SpiderlyClass m2mEntity_1 = allEntityClasses.Where(x => x.Name == m2mWithManyProperty_1.Type).Single();
            string m2mEntityIdType_1 = m2mEntity_1.GetIdType(allEntityClasses);

            SpiderlyClass m2mEntity_2 = allEntityClasses.Where(x => x.Name == m2mWithManyProperty_2.Type).Single();
            string m2mEntityIdType_2 = m2mEntity_2.GetIdType(allEntityClasses);

            return $$"""
{{GetAdditionalFieldsManyToManyAdministrationMethod(m2mWithManyProperty_1, m2mWithManyProperty_2, m2mEntityIdType_1, m2mEntityIdType_2, entity)}}

{{GetAdditionalFieldsManyToManyAdministrationMethod(m2mWithManyProperty_2, m2mWithManyProperty_1, m2mEntityIdType_2, m2mEntityIdType_1, entity)}}
""";
        }

        public static string GetAdditionalFieldsManyToManyAdministrationMethod(
            SpiderlyProperty m2mWithManyProperty_1,
            SpiderlyProperty m2mWithManyProperty_2,
            string m2mEntityIdType_1,
            string m2mEntityIdType_2,
            SpiderlyClass entity
        )
        {
            return $$"""
        /// <summary>
        /// Call this method when you have additional fields in M2M association
        /// </summary>
        public async Task Update{{m2mWithManyProperty_1.Type}}ListFor{{m2mWithManyProperty_2.Type}}({{m2mEntityIdType_2}} {{m2mWithManyProperty_2.Name.FirstCharToLower()}}Id, List<{{entity.Name}}DTO> selected{{entity.Name}}DTOList)
        {
            if (selected{{entity.Name}}DTOList == null)
                return;

            List<{{entity.Name}}DTO> selectedDTOListHelper = selected{{entity.Name}}DTOList.ToList();

            await _context.WithTransactionAsync(async () =>
            {
                // Not doing authorization here, because we can not figure out here if we are updating while inserting object (eg. User), or updating object, we will always get the id which is not 0 here.

                var dbSet = _context.DbSet<{{entity.Name}}>();
                var {{entity.Name.FirstCharToLower()}}List = await dbSet.Where(x => x.{{m2mWithManyProperty_2.Name}}.Id == {{m2mWithManyProperty_2.Name.FirstCharToLower()}}Id).ToListAsync();

                foreach ({{entity.Name}}DTO selected{{entity.Name}}DTO in selectedDTOListHelper)
                {
                    var validationRules = new {{entity.Name}}DTOValidationRules();
                    DefaultValidatorExtensions.ValidateAndThrow(validationRules, selected{{entity.Name}}DTO);

                    var {{entity.Name.FirstCharToLower()}} = {{entity.Name.FirstCharToLower()}}List.Where(x => x.{{m2mWithManyProperty_1.Name}}.Id == selected{{entity.Name}}DTO.{{m2mWithManyProperty_1.Name}}Id).SingleOrDefault();

                    if ({{entity.Name.FirstCharToLower()}} == null)
                    {
                        {{entity.Name.FirstCharToLower()}} = TypeAdapter.Adapt<{{entity.Name}}>(selected{{entity.Name}}DTO, Mapper.{{entity.Name}}DTOToEntityConfig());
                        {{entity.Name.FirstCharToLower()}}.{{m2mWithManyProperty_2.Name}} = await GetInstanceAsync<{{m2mWithManyProperty_2.Type}}, {{m2mEntityIdType_2}}>({{m2mWithManyProperty_2.Name.FirstCharToLower()}}Id, null);
                        {{entity.Name.FirstCharToLower()}}.{{m2mWithManyProperty_1.Name}} = await GetInstanceAsync<{{m2mWithManyProperty_1.Type}}, {{m2mEntityIdType_1}}>(selected{{entity.Name}}DTO.{{m2mWithManyProperty_1.Name}}Id.Value, null);
                        dbSet.Add({{entity.Name.FirstCharToLower()}});
                    }
                    else
                    {
                        selected{{entity.Name}}DTO.Adapt({{entity.Name.FirstCharToLower()}}, Mapper.{{entity.Name}}DTOToEntityConfig());
                        dbSet.Update({{entity.Name.FirstCharToLower()}});

                        {{entity.Name.FirstCharToLower()}}List.Remove({{entity.Name.FirstCharToLower()}});
                    }
                }

                dbSet.RemoveRange({{entity.Name.FirstCharToLower()}}List);

                await _context.SaveChangesAsync();
            });
        }
""";
        }

        #endregion

        #region Helpers

        private static string GetUsings(string basePartOfTheNamespace, string projectName)
        {
            return $$"""
using {{basePartOfTheNamespace}}.ValidationRules;
using {{basePartOfTheNamespace}}.DataMappers;
using {{basePartOfTheNamespace}}.DTO;
using {{basePartOfTheNamespace}}.Entities;
using {{basePartOfTheNamespace}}.Enums;
using {{basePartOfTheNamespace}}.ExcelProperties;
using {{basePartOfTheNamespace}}.Filtering;
{{(projectName == "Security" ? "" : $"using {basePartOfTheNamespace.ReplaceEverythingAfterLast(".", ".Shared")}.Resources;")}}
using Microsoft.EntityFrameworkCore;
using System.Data;
using FluentValidation;
using Spiderly.Security.Services;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Entities;
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

        private static string GetAuthorizeEntityMethodCall(string entityName, CrudCodes crudCode, string parametersBody)
        {
            string methodName = Helpers.GetAuthorizeEntityMethodName(entityName, crudCode);
            return $"await _authorizationService.{methodName}({parametersBody});";
        }

        #endregion

    }
}