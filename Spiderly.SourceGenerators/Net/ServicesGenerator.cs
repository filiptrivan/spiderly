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
        {{(shouldGenerateCloudinaryStorageService ? "private readonly CloudinaryStorageService _cloudinaryStorageService;" : "")}}
        {{(shouldGenerateS3PublicStorageService ? "private readonly S3PublicStorageService _s3PublicStorageService;" : "")}}

        public BusinessServiceGenerated(
            IApplicationDbContext context, 
            ExcelService excelService, 
            AuthorizationService authorizationService, 
            IFileManager fileManager
            {{(shouldGenerateCloudinaryStorageService ? ", CloudinaryStorageService cloudinaryStorageService" : "")}}
            {{(shouldGenerateS3PublicStorageService ? ", S3PublicStorageService s3PublicStorageService" : "")}}
        )
            : base(context)
        {
            _context = context;
            _excelService = excelService;
            _authorizationService = authorizationService;
            _fileManager = fileManager;
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

{{GetManyToManyData(entity, allEntityClasses)}}

        #endregion
""");
                }
                else
                {
                    result.Add($$"""
        #region {{entity.Name}}

        #region Read

{{GetReadBusinessServiceMethods(entity, allEntityClasses)}}

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

        private static string GetReadBusinessServiceMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            string entityIdType = entity.GetIdType(allEntities);

            return $$"""
        /// <summary>
        /// Retrieves the complete MainUIFormDTO for {{entity.Name}}, including the entity DTO and all related collections (one-to-many, many-to-many).
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>{{entity.Name}}MainUIFormDTO containing the entity DTO and related data</returns>
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

        /// <summary>
        /// Retrieves a single {{entity.Name}} entity as a DTO with blob data populated.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>{{entity.Name}}DTO with all blob properties populated</returns>
        public async virtual Task<{{entity.Name}}DTO> Get{{entity.Name}}DTO({{entityIdType}} id, bool authorize)
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

{{GetPopulateDTOWithBlobPartsForDTO(entity.Properties)}}

                return dto;
            });
        }

        /// <summary>
        /// Retrieves a paginated list of {{entity.Name}} entities.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<{{entity.Name}}>> GetPaginated{{entity.Name}}List(FilterDTO filterDTO, IQueryable<{{entity.Name}}> query)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of {{entity.Name}} DTOs with blob data populated.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>PaginatedResultDTO containing {{entity.Name}}DTO list and total record count</returns>
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

{{GetPopulateDTOWithBlobPartsForDTOList(entity.Properties)}}
            });

            return new PaginatedResultDTO<{{entity.Name}}DTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of {{entity.Name}} entities to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> Export{{entity.Name}}ListToExcel(FilterDTO filterDTO, IQueryable<{{entity.Name}}> query, bool authorize)
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
            return _excelService.FillReportTemplate<{{entity.Name}}DTO>(dtoList, paginationResult.TotalRecords, excelPropertiesToExclude, TermsGenerated.ResourceManager).ToArray();
        }

        /// <summary>
        /// Retrieves a list of {{entity.Name}} entities without pagination.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>List of {{entity.Name}} entities</returns>
        public async virtual Task<List<{{entity.Name}}>> Get{{entity.Name}}List(IQueryable<{{entity.Name}}> query, bool authorize)
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

        /// <summary>
        /// Retrieves a list of {{entity.Name}} DTOs without pagination, with blob data populated.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>List of {{entity.Name}}DTO with blob properties populated</returns>
        public async virtual Task<List<{{entity.Name}}DTO>> Get{{entity.Name}}DTOList(IQueryable<{{entity.Name}}> query, bool authorize)
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

{{GetPopulateDTOWithBlobPartsForDTOList(entity.Properties)}}

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
                SpiderlyClass extractedEntity = allEntities.SingleOrDefault(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));
                string extractedEntityIdType = extractedEntity.GetIdType(allEntities);

                if (property.HasUIOrderedOneToManyAttribute())
                {
                    result.Add($$"""
                    Ordered{{property.Name}}MainUIFormDTO = await GetOrdered{{property.Name}}For{{entity.Name}}(id, false),
""");
                }
                else if (property.IsMultiSelectControlType())
                {
                    result.Add($$"""
                    {{property.Name}}Ids = await Get{{property.Name}}IdsFor{{entity.Name}}(id, false),
""");
                }
                else if (property.IsMultiAutocompleteControlType())
                {
                    result.Add($$"""
                    {{property.Name}}NamebookDTOList = await Get{{property.Name}}NamebookListFor{{entity.Name}}(id, false),
""");
                }
            }

            return string.Join("\n", result);
        }

        private static string GetMainUIFormDTOInitializationManyToManyPropertiesAfterSave(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                SpiderlyClass extractedEntity = allEntities.SingleOrDefault(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));
                string extractedEntityIdType = extractedEntity.GetIdType(allEntities);

                if (property.IsMultiSelectControlType())
                {
                    result.Add($$"""
                    {{property.Name}}Ids = saveBodyDTO.Selected{{property.Name}}Ids,
""");
                }
                else if (property.IsMultiAutocompleteControlType())
                {
                    result.Add($$"""
                    {{property.Name}}NamebookDTOList = saveBodyDTO.Selected{{property.Name}}NamebookDTOList,
""");
                }
            }

            return string.Join("\n", result);
        }

        private static string GetPopulateDTOWithBlobPartsForDTO(List<SpiderlyProperty> propertiesEntityClass)
        {
            List<string> blobParts = GetPopulateDTOWithBlobParts(propertiesEntityClass);

            if (blobParts.Count == 0)
                return null;

            return $$"""
{{string.Join("\n", blobParts)}}
""";
        }

        private static string GetPopulateDTOWithBlobPartsForDTOList(List<SpiderlyProperty> propertiesEntityClass)
        {
            List<string> blobParts = GetPopulateDTOWithBlobParts(propertiesEntityClass);

            if (blobParts.Count == 0)
                return null;

            return $$"""
                foreach (var dto in dtoList)
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
                        dto.{{property.Name}}Data = await {{GetFileManagerServiceField(property)}}.GetFileDataAsync(dto.{{property.Name}});
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
        /// <summary>
        /// Retrieves autocomplete suggestions for the {{property.Name}} many-to-one relationship in {{entity.Name}}.
        /// </summary>
        /// <param name="limit">Maximum number of results to return</param>
        /// <param name="filter">Text filter for {{autocompleteEntityDisplayName}}</param>
        /// <param name="query">Base query for {{autocompleteEntity.Name}} entities</param>
        /// <param name="authorize">Whether to perform authorization check</param>
        /// <param name="{{entity.Name.FirstCharToLower()}}Id">Optional {{entity.Name}} ID for context-specific authorization</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
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
        /// <summary>
        /// Retrieves dropdown options for the {{property.Name}} many-to-one relationship in {{entity.Name}}.
        /// </summary>
        /// <param name="query">Base query for {{dropdownEntity.Name}} entities</param>
        /// <param name="authorize">Whether to perform authorization check</param>
        /// <param name="{{entity.Name.FirstCharToLower()}}Id">Optional {{entity.Name}} ID for context-specific authorization</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
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

        private static List<string> GetOneToManyMethods(SpiderlyClass entity, List<SpiderlyClass> allEntityClasses)
        {
            string entityIdType = entity.GetIdType(allEntityClasses);

            List<string> result = new();

            foreach (SpiderlyProperty oneToManyProperty in entity.Properties.Where(prop => prop.Type.IsOneToManyType())) // List<Role> Roles
            {
                SpiderlyClass extractedPropertyEntity = allEntityClasses.Single(x => x.Name == Helpers.ExtractTypeFromGenericType(oneToManyProperty.Type)); // Role
                string extractedPropertyEntityIdType = extractedPropertyEntity.GetIdType(allEntityClasses); // int

                if (extractedPropertyEntity.HasM2MAttribute()) // Complex M2M List
                {
                    if (oneToManyProperty.HasComplexManyToManyReadonlyTableAttribute())
                    {
                        SpiderlyProperty m2mProperty = extractedPropertyEntity.Properties
                            .SingleOrDefault(x =>
                                x.HasM2MWithManyAttribute() &&
                                x.Type == entity.Name &&
                                x.Attributes.Any(x => x.Value == oneToManyProperty.Name)
                            );

                        if (m2mProperty == null)
                            throw new Exception("You didn't specify correct M2MWithMany attribute");

                        result.Add(GetPaginatedListForComplexM2MMethod(extractedPropertyEntity, oneToManyProperty, m2mProperty, entity, allEntityClasses));
                    }

                    continue;
                }

                string extractedPropertyEntityDisplayName = Helpers.GetDisplayNameProperty(extractedPropertyEntity); // Name

                SpiderlyProperty manyToOneProperty = extractedPropertyEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, oneToManyProperty.Name); // Many to one property from the other side
                SpiderlyProperty extractedEntityManyToManyProperty = Helpers.GetOppositeManyToManyProperty(oneToManyProperty, extractedPropertyEntity, entity, allEntityClasses);

                if (manyToOneProperty != null) // One To Many
                {
                    result.Add($$"""
{{GetOneToManyNamebookListForEntity(oneToManyProperty, extractedPropertyEntity, manyToOneProperty, entity, allEntityClasses)}}

{{GetOneToManyListForEntity(oneToManyProperty, extractedPropertyEntity, manyToOneProperty, entity, allEntityClasses)}}

{{GetOrderedOneToManyMethod(oneToManyProperty, entity, allEntityClasses)}}
""");
                }
                else if (extractedEntityManyToManyProperty != null) // Simple Many To Many
                {
                    result.Add($$"""
        /// <summary>
        /// Retrieves namebook DTOs for {{extractedPropertyEntity.Name}} entities in a many-to-many relationship with {{entity.Name}}.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <param name="authorize">Whether to perform authorization check</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
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

        /// <summary>
        /// Retrieves IDs of {{extractedPropertyEntity.Name}} entities in a many-to-many relationship with {{entity.Name}}.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <param name="authorize">Whether to perform authorization check</param>
        /// <returns>List of entity IDs</returns>
        public async virtual Task<List<{{extractedPropertyEntityIdType}}>> Get{{oneToManyProperty.Name}}IdsFor{{entity.Name}}({{entityIdType}} id, bool authorize)
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
                    .Select(x => x.Id)
                    .ToListAsync();
            });
        }

        /// <summary>
        /// Retrieves {{extractedPropertyEntity.Name}} entities in a many-to-many relationship with {{entity.Name}}.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <returns>List of {{extractedPropertyEntity.Name}} entities</returns>
        public async virtual Task<List<{{extractedPropertyEntity.Name}}>> Get{{oneToManyProperty.Name}}For{{entity.Name}}({{entityIdType}} id)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .Where(x => x.{{extractedEntityManyToManyProperty.Name}}.Any(x => x.Id == id))
                    .ToListAsync();
            });
        }

        /// <summary>
        /// Updates the many-to-many relationship between {{entity.Name}} and {{extractedPropertyEntity.Name}} entities.
        /// Adds new associations and removes unselected ones.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <param name="selectedIds">List of selected {{extractedPropertyEntity.Name}} IDs</param>
        public async virtual Task Update{{oneToManyProperty.Name}}For{{entity.Name}}({{entityIdType}} id, List<{{extractedPropertyEntityIdType}}> selectedIds)
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
        /// Lazy loads selected IDs for many-to-many relationship with pagination support.
        /// IMPORTANT: The query must be ordered by the same field as the table data for correct results.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query (must be ordered)</param>
        /// <param name="authorize">Whether to perform authorization check</param>
        /// <returns>LazyLoadSelectedIdsResultDTO containing selected IDs and total count</returns>
        public async virtual Task<LazyLoadSelectedIdsResultDTO<{{extractedPropertyEntityIdType}}>> LazyLoadSelected{{oneToManyProperty.Name}}IdsFor{{entity.Name}}(FilterDTO filterDTO, IQueryable<{{extractedPropertyEntity.Name}}> query, bool authorize)
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

{{GetSimpleManyToManyUpdateWithLazyTableSelectionMethod(oneToManyProperty, entity, allEntityClasses)}}
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
        /// <summary>
        /// Retrieves all {{extractedPropertyEntity.Name}} entities related to a {{entity.Name}} via the {{oneToManyProperty.Name}} one-to-many relationship.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <returns>List of {{extractedPropertyEntity.Name}} entities</returns>
        public async virtual Task<List<{{extractedPropertyEntity.Name}}>> Get{{oneToManyProperty.Name}}For{{entity.Name}}({{entity.GetIdType(entities)}} id)
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
            string extractedPropertyEntityIdType = extractedPropertyEntity.GetIdType(entities);

            return $$"""
        /// <summary>
        /// Retrieves namebook DTOs for {{extractedPropertyEntity.Name}} entities related to a {{entity.Name}}.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <param name="authorize">Whether to perform authorization check</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
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

        private static string GetPaginatedListForComplexM2MMethod(SpiderlyClass listEntitty, SpiderlyProperty oneToManyProperty, SpiderlyProperty m2mProperty, SpiderlyClass entity, List<SpiderlyClass> allEntityClasses)
        {
            return $$"""
        /// <summary>
        /// Retrieves a paginated list of {{listEntitty.Name}} entities for a complex many-to-many relationship.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<{{listEntitty.Name}}>> GetPaginated{{oneToManyProperty.Name}}ListFor{{entity.Name}}(FilterDTO filterDTO, IQueryable<{{listEntitty.Name}}> query)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of {{listEntitty.Name}} DTOs for a complex many-to-many relationship with blob data.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <param name="authorize">Whether to perform authorization check</param>
        /// <returns>PaginatedResultDTO containing {{listEntitty.Name}}DTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<{{listEntitty.Name}}DTO>> GetPaginated{{oneToManyProperty.Name}}ListFor{{entity.Name}}(FilterDTO filterDTO, IQueryable<{{listEntitty.Name}}> query, bool authorize)
        {
            PaginatedResult<{{listEntitty.Name}}> paginationResult = new();
            List<{{listEntitty.Name}}DTO> dtoList = null;

            await _context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginated{{oneToManyProperty.Name}}ListFor{{entity.Name}}(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<{{listEntitty.Name}}DTO>(Mapper.{{listEntitty.Name}}ProjectToConfig())
                    .ToListAsync();

                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, $"dtoList.Select(x => ({entity.GetIdType(allEntityClasses)})x.{m2mProperty.Name}Id).ToList()")}}
                }

{{GetPopulateDTOWithBlobPartsForDTOList(entity.Properties)}}
            });

            return new PaginatedResultDTO<{{listEntitty.Name}}DTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of {{listEntitty.Name}} entities for a complex many-to-many relationship to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="authorize">Whether to perform authorization check</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> Export{{oneToManyProperty.Name}}ListToExcelFor{{entity.Name}}(FilterDTO filterDTO, IQueryable<{{listEntitty.Name}}> query, bool authorize)
        {
            PaginatedResult<{{listEntitty.Name}}> paginationResult = new();
            List<{{listEntitty.Name}}DTO> dtoList = null;

            await _context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginated{{oneToManyProperty.Name}}ListFor{{entity.Name}}(filterDTO, query);

                dtoList = await paginationResult.Query.ProjectToType<{{listEntitty.Name}}DTO>(Mapper.{{listEntitty.Name}}ExcelProjectToConfig()).ToListAsync();

                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, $"dtoList.Select(x => ({entity.GetIdType(allEntityClasses)})x.{m2mProperty.Name}Id).ToList()")}}
                }
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new {{listEntitty.Name}}DTO());
            return _excelService.FillReportTemplate<{{listEntitty.Name}}DTO>(dtoList, paginationResult.TotalRecords, excelPropertiesToExclude, TermsGenerated.ResourceManager).ToArray();
        }
""";
        }


        private static string GetSimpleManyToManyUpdateWithLazyTableSelectionMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            if (property.HasSimpleManyToManyTableLazyLoadAttribute() == false)
                return null;

            string entityIdType = entity.GetIdType(entities);
            SpiderlyClass extractedPropertyEntity = entities.Single(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)); // Role

            string extractedPropertyEntityIdType = extractedPropertyEntity.GetIdType(entities);

            return $$"""
        /// <summary>
        /// Updates many-to-many relationship with lazy table selection support.
        /// Handles "select all", "select none", and partial selection scenarios.
        /// </summary>
        /// <param name="query">The base query for available entities</param>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing selection state</param>
        public async virtual Task Update{{property.Name}}WithLazyTableSelectionFor{{entity.Name}}(IQueryable<{{extractedPropertyEntity.Name}}> query, {{entityIdType}} id, {{entity.Name}}SaveBodyDTO saveBodyDTO)
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
        /// <summary>
        /// Retrieves ordered child entities for a one-to-many relationship (requires [UIOrderedOneToMany] attribute).
        /// Returns complete MainUIFormDTOs ordered by OrderNumber.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <param name="authorize">Whether to perform authorization check</param>
        /// <returns>List of {{extractedPropertyEntity.Name}}MainUIFormDTO ordered by OrderNumber</returns>
        public async virtual Task<List<{{extractedPropertyEntity.Name}}MainUIFormDTO>> GetOrdered{{property.Name}}For{{entity.Name}}({{entity.GetIdType(entities)}} id, bool authorize)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    {{GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Read, "id")}}
                }

                List<{{extractedPropertyEntity.Name}}MainUIFormDTO> mainUIFormDTOList = new();

                var ids = await _context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .AsNoTracking()
                    .Where(x => x.{{manyToOneProperty.Name}}.Id == id)
                    .OrderBy(x => x.OrderNumber)
                    .Select(x => x.Id)
                    .ToListAsync();

                foreach (var id in ids)
                    mainUIFormDTOList.Add(await Get{{extractedPropertyEntity.Name}}MainUIFormDTO(id, authorize));

                return mainUIFormDTOList;
            });
        }
""";
        }

        #endregion

        #region Save

        private static string GetSavingData(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            if (entity.IsAbstract || entity.IsReadonlyObject())
                return null;

            string entityIdType = entity.GetIdType(entities);

            return $$"""
{{GetSaveAndReturnMainUIFormDTOData(entity, entities)}}

        /// <summary>
        /// Saves a {{entity.Name}} entity and returns the DTO with blob data populated.
        /// </summary>
        /// <param name="saveDTO">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved {{entity.Name}}DTO with blob properties populated</returns>
        public async virtual Task<{{entity.Name}}DTO> Save{{entity.Name}}AndReturnDTO({{entity.Name}}DTO saveDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                var poco = await Save{{entity.Name}}(saveDTO, authorizeUpdate, authorizeInsert);

                var dto = poco.Adapt<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ToDTOConfig());

{{GetPopulateDTOWithBlobPartsForDTO(entity.Properties)}}

                return dto;
            });
        }

        /// <summary>
        /// Core save method that handles both insert and update operations for {{entity.Name}}.
        /// Validates the DTO, maps to entity, handles many-to-one relationships, and manages blob deletion.
        /// </summary>
        /// <param name="dto">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved {{entity.Name}} entity</returns>
        public async virtual Task<{{entity.Name}}> Save{{entity.Name}}({{entity.Name}}DTO dto, bool authorizeUpdate, bool authorizeInsert)
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
                    await OnBefore{{entity.Name}}Insert(poco, dto);
                    await dbSet.AddAsync(poco);
                }

{{string.Join("\n", GetManyToOneInstancesForSave(entity, entities))}}

                await _context.SaveChangesAsync();

{{string.Join("\n", GetNonActiveDeleteBlobMethods(entity))}}
            });

            return poco;
        }

        /// <summary>
        /// Lifecycle hook called before the {{entity.Name}}DTO is mapped to the entity.
        /// Override this method to add custom validation or modify the DTO before mapping.
        /// </summary>
        /// <param name="{{entity.Name.FirstCharToLower()}}DTO">The DTO about to be mapped</param>
        protected virtual async Task OnBefore{{entity.Name}}IsMapped({{entity.Name}}DTO {{entity.Name.FirstCharToLower()}}DTO) { }

        /// <summary>
        /// Lifecycle hook called before updating an existing {{entity.Name}} entity.
        /// Override this method to add custom business logic during updates.
        /// </summary>
        /// <param name="{{entity.Name.FirstCharToLower()}}">The existing entity being updated</param>
        /// <param name="{{entity.Name.FirstCharToLower()}}DTO">The DTO containing new data</param>
        protected virtual async Task OnBefore{{entity.Name}}Update({{entity.Name}} {{entity.Name.FirstCharToLower()}}, {{entity.Name}}DTO {{entity.Name.FirstCharToLower()}}DTO) { }

        /// <summary>
        /// Lifecycle hook called before inserting a new {{entity.Name}} entity.
        /// Override this method to add custom business logic during inserts.
        /// </summary>
        /// <param name="{{entity.Name.FirstCharToLower()}}">The new entity being inserted</param>
        /// <param name="{{entity.Name.FirstCharToLower()}}DTO">The DTO containing the data</param>
        protected virtual async Task OnBefore{{entity.Name}}Insert({{entity.Name}} {{entity.Name.FirstCharToLower()}}, {{entity.Name}}DTO {{entity.Name.FirstCharToLower()}}DTO) { }
""";
        }

        private static string GetSaveAndReturnMainUIFormDTOData(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            return $$"""
        /// <summary>
        /// Saves a {{entity.Name}} entity and returns the complete MainUIFormDTO including all related collections.
        /// Handles insert/update logic, many-to-many relationships, and ordered one-to-many collections.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity data and related selections</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>{{entity.Name}}MainUIFormDTO with saved data and updated collections</returns>
        public virtual async Task<{{entity.Name}}MainUIFormDTO> Save{{entity.Name}}AndReturnMainUIFormDTO({{entity.Name}}SaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                await OnBeforeSave{{entity.Name}}AndReturnMainUIFormDTO(saveBodyDTO);

                var savedDTO = await Save{{entity.Name}}AndReturnDTO(saveBodyDTO.{{entity.Name}}DTO, authorizeUpdate, authorizeInsert);

                await OnAfterSave{{entity.Name}}AndReturnMainUIFormDTO(savedDTO, saveBodyDTO);

{{string.Join("\n", GetOrderedOneToManyUpdateVariables(entity, allEntities))}}
{{string.Join("\n", GetManyToManyMultiControlTypesUpdateMethods(entity, allEntities))}}
{{string.Join("\n", GetSimpleManyToManyTableLazyLoad(entity, allEntities))}}

                var result = new {{entity.Name}}MainUIFormDTO
                {
                    {{entity.Name}}DTO = savedDTO,
{{string.Join("\n", GetOrderedOneToManySaveBodyDTOVariables(entity, allEntities))}}
{{GetMainUIFormDTOInitializationManyToManyPropertiesAfterSave(entity, allEntities)}}
                };

                return result;
            });
        }

{{string.Join("\n", GetOrderedOneToManyUpdateMethods(entity, allEntities))}}
{{string.Join("\n", GetSimpleManyToManyTableLazyLoadGetAllQueryHook(entity, allEntities))}}

        /// <summary>
        /// Lifecycle hook called before saving {{entity.Name}} with MainUIFormDTO.
        /// Override this method to add custom validation or modify the SaveBodyDTO.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity and related data</param>
        protected virtual async Task OnBeforeSave{{entity.Name}}AndReturnMainUIFormDTO({{entity.Name}}SaveBodyDTO saveBodyDTO) { }

        /// <summary>
        /// Lifecycle hook called after saving {{entity.Name}} but before updating related collections.
        /// Override this method to add custom business logic after the main entity is saved.
        /// </summary>
        /// <param name="savedDTO">The saved entity DTO</param>
        /// <param name="saveBodyDTO">The original SaveBodyDTO</param>
        protected virtual async Task OnAfterSave{{entity.Name}}AndReturnMainUIFormDTO({{entity.Name}}DTO savedDTO, {{entity.Name}}SaveBodyDTO saveBodyDTO) { }
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
                var savedOrdered{{property.Name}}MainUIFormDTO = await UpdateOrdered{{property.Name}}For{{entity.Name}}(savedDTO.Id, saveBodyDTO.Ordered{{property.Name}}SaveBodyDTO);
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
                    Ordered{{property.Name}}MainUIFormDTO = savedOrdered{{property.Name}}MainUIFormDTO,
""");
            }

            return result;
        }

        private static List<string> GetOrderedOneToManyUpdateMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = allEntities.SingleOrDefault(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));

                result.Add($$"""
        /// <summary>
        /// Updates ordered child entities for a one-to-many relationship.
        /// Deletes items not in the list, updates existing items, and maintains order via OrderNumber.
        /// </summary>
        /// <param name="id">The ID of the parent {{entity.Name}} entity</param>
        /// <param name="orderedItemsDTO">List of SaveBodyDTOs in the desired order</param>
        /// <returns>List of saved {{extractedEntity.Name}}MainUIFormDTO in order</returns>
        public async virtual Task<List<{{extractedEntity.Name}}MainUIFormDTO>> UpdateOrdered{{property.Name}}For{{entity.Name}}({{entity.GetIdType(allEntities)}} id, List<{{extractedEntity.Name}}SaveBodyDTO> orderedItemsDTO)
        {
            var orderedItemIds = orderedItemsDTO.Select(x => x.{{extractedEntity.Name}}DTO.Id).ToList();

{{GetOrderedOneToManyRequiredValidation(property, entity)}}

            return await _context.WithTransactionAsync(async () =>
            {
                await _context.DbSet<{{extractedEntity.Name}}>().Where(x => x.{{extractedEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, property.Name)?.Name}}.Id == id && orderedItemIds.Contains(x.Id) == false).ExecuteDeleteAsync();

                var savedOrderedItemsDTO = new List<{{extractedEntity.Name}}MainUIFormDTO>();

                for (int i = 0; i < orderedItemsDTO.Count; i++)
                {
                    var saveBodyDTO = orderedItemsDTO[i];
                    var DTO = saveBodyDTO.{{extractedEntity.Name}}DTO;

                    DTO.{{extractedEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, property.Name)?.Name}}Id = id;
                    DTO.OrderNumber = i + 1;

                    var savedDTO = await Save{{extractedEntity.Name}}AndReturnDTO(DTO, false, false);

{{string.Join("\n", GetOrderedOneToManyUpdateVariables(extractedEntity, allEntities))}}

                    savedOrderedItemsDTO.Add(new {{extractedEntity.Name}}MainUIFormDTO
                    {
                        {{extractedEntity.Name}}DTO = savedDTO,
{{string.Join("\n", GetOrderedOneToManySaveBodyDTOVariables(extractedEntity, allEntities))}}
                    });
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
                if (property.IsMultiSelectControlType())
                {
                    result.Add($$"""
                await Update{{property.Name}}For{{entity.Name}}(savedDTO.Id, saveBodyDTO.Selected{{property.Name}}Ids);
""");
                }
                if (property.IsMultiAutocompleteControlType())
                {
                    result.Add($$"""
                await Update{{property.Name}}For{{entity.Name}}(savedDTO.Id, saveBodyDTO.Selected{{property.Name}}NamebookDTOList.Select(x => x.Id).ToList());
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
        /// <summary>
        /// Lifecycle hook to customize the query for lazy-loaded {{property.Name}} many-to-many relationship.
        /// Override this method to add filters, includes, or ordering to the base query.
        /// </summary>
        /// <param name="query">The base query for {{extractedEntity.Name}} entities</param>
        /// <returns>Modified query</returns>
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

        private static List<string> GetManyToOneInstancesForSave(SpiderlyClass entityClass, List<SpiderlyClass> allEntityClasses)
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

        private static SpiderlyClass GetClassOfManyToOneProperty(string propType, List<SpiderlyClass> allEntityClasses)
        {
            SpiderlyClass manyToOneclass = allEntityClasses.SingleOrDefault(x => x.Name == propType);

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
                await {{GetFileManagerServiceField(property)}}.DeleteNonActiveBlobs(dto.{{property.Name}}, nameof({{entity.Name}}), nameof({{entity.Name}}.{{property.Name}}), poco.Id.ToString());
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
        /// <summary>
        /// Uploads a blob/file for the {{property.Name}} property of {{entity.Name}}.
        /// Automatically optimizes images before upload. The entity ID is extracted from the filename.
        /// </summary>
        /// <param name="file">The file to upload</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>The filename in storage</returns>
        public virtual async Task<string> Upload{{property.Name}}For{{entity.Name}}(IFormFile file, bool authorizeUpdate, bool authorizeInsert)
        {
            {{entityIdType}} id = Helper.GetObjectIdFromFileName<{{entityIdType}}>(file.FileName);

            await OnBefore{{property.Name}}BlobFor{{entity.Name}}UploadIsAuthorized(file, id);

            if (id > 0 && authorizeUpdate)
            {
                {{GetAuthorizeEntityMethodCall($"{property.Name}For{entity.Name}", CrudCodes.Update, "id")}}
            }
            else if (authorizeInsert)
            {
                {{GetAuthorizeEntityMethodCall($"{property.Name}For{entity.Name}", CrudCodes.Insert, "")}}
            }

            string fileName;

            using (Stream stream = file.OpenReadStream())
            {
                byte[] byteArray = await OnBefore{{property.Name}}BlobFor{{entity.Name}}IsUploaded(stream, file, id);

                using (Stream updatedStream = new MemoryStream(byteArray))
                {
                    fileName = await {{GetFileManagerServiceField(property)}}.UploadFileAsync(file.FileName, nameof({{entity.Name}}), nameof({{entity.Name}}.{{property.Name}}), id.ToString(), updatedStream);
                }
            }

            return fileName;
        }

        /// <summary>
        /// Lifecycle hook called before blob upload is authorized.
        /// Override this to add custom validation logic before authorization.
        /// </summary>
        /// <param name="file">The file being uploaded</param>
        /// <param name="id">The entity ID</param>
        public virtual async Task OnBefore{{property.Name}}BlobFor{{entity.Name}}UploadIsAuthorized (IFormFile file, {{entityIdType}} id) { }

        /// <summary>
        /// Lifecycle hook called before blob is uploaded to storage.
        /// Default implementation optimizes images. Override to customize file processing.
        /// </summary>
        /// <param name="stream">The file stream</param>
        /// <param name="file">The form file</param>
        /// <param name="id">The entity ID</param>
        /// <returns>Processed file bytes</returns>
        public virtual async Task<byte[]> OnBefore{{property.Name}}BlobFor{{entity.Name}}IsUploaded (Stream stream, IFormFile file, {{entityIdType}} id) 
        {
            if (file.ContentType.StartsWith("image/"))
            {
                return await Helper.OptimizeImage(stream); 
            }
            else
            {
                return await Helper.ReadAllBytesAsync(stream);
            }
        }
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
        /// <summary>
        /// Lifecycle hook called before deleting a {{entity.Name}} entity.
        /// Override this to add custom validation or business logic before deletion.
        /// </summary>
        /// <param name="id">The ID of the entity being deleted</param>
        public virtual async Task OnBefore{{entity.Name}}Delete({{entityIdType}} id) { }

        /// <summary>
        /// Deletes a single {{entity.Name}} entity with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="id">The ID of the entity to delete</param>
        /// <param name="authorize">Whether to perform authorization check for Delete operation</param>
        public async virtual Task Delete{{entity.Name}}({{entityIdType}} id, bool authorize)
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
        /// <summary>
        /// Lifecycle hook called before deleting a list of {{entity.Name}} entities.
        /// Override this to add custom validation or business logic before batch deletion.
        /// </summary>
        /// <param name="listForDelete">The list of entity IDs being deleted</param>
        public virtual async Task OnBefore{{entity.Name}}ListDelete(List<{{entityIdType}}> listForDelete) { }

        /// <summary>
        /// Deletes multiple {{entity.Name}} entities with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="listForDelete_{{deleteIterator}}">The list of entity IDs to delete</param>
        /// <param name="authorize">Whether to perform authorization check for Delete operation</param>
        public async virtual Task Delete{{entity.Name}}List(List<{{entityIdType}}> listForDelete_{{deleteIterator}}, bool authorize)
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
                SpiderlyClass parentEntity = allEntities.SingleOrDefault(x => x.Name == property.EntityName);

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
                .Where(x => x.HasM2MWithManyAttribute())
                .ToList();

            if (m2mWithManyProperties.Count != 2)
                return "YouNeedToDefineTwoM2MWithManyProperties";

            SpiderlyProperty m2mWithManyProperty_1 = m2mWithManyProperties[0];
            SpiderlyAttribute m2mWithManyAttribute_1 = m2mWithManyProperty_1.Attributes.Single(x => x.Name == "M2MWithMany");

            SpiderlyProperty m2mWithManyProperty_2 = m2mWithManyProperties[1];
            SpiderlyAttribute m2mWithManyAttribute_2 = m2mWithManyProperty_2.Attributes.Single(x => x.Name == "M2MWithMany");

            if (m2mWithManyAttribute_1.Value != m2mWithManyAttribute_2.Value)
                return null; // It's simple M2M

            SpiderlyClass m2mEntity_1 = allEntityClasses.Single(x => x.Name == m2mWithManyProperty_1.Type);
            string m2mEntityIdType_1 = m2mEntity_1.GetIdType(allEntityClasses);

            SpiderlyClass m2mEntity_2 = allEntityClasses.Single(x => x.Name == m2mWithManyProperty_2.Type);
            string m2mEntityIdType_2 = m2mEntity_2.GetIdType(allEntityClasses);

            return $$"""
{{GetComplexManyToManyAdministrationMethod(m2mWithManyProperty_1, m2mWithManyProperty_2, m2mEntityIdType_1, m2mEntityIdType_2, entity)}}

{{GetComplexManyToManyAdministrationMethod(m2mWithManyProperty_2, m2mWithManyProperty_1, m2mEntityIdType_2, m2mEntityIdType_1, entity)}}
""";
        }

        public static string GetComplexManyToManyAdministrationMethod(
            SpiderlyProperty m2mWithManyProperty_1,
            SpiderlyProperty m2mWithManyProperty_2,
            string m2mEntityIdType_1,
            string m2mEntityIdType_2,
            SpiderlyClass entity
        )
        {
            return $$"""
        /// <summary>
        /// Updates a complex many-to-many relationship with additional fields in the association entity.
        /// Use this for M2M relationships that have extra properties beyond the foreign keys (e.g., OrderProduct with Quantity, Price).
        /// Validates each DTO, adds new associations, updates existing ones, and removes unselected associations.
        /// </summary>
        /// <param name="{{m2mWithManyProperty_2.Name.FirstCharToLower()}}Id">The ID of the {{m2mWithManyProperty_2.Type}} entity</param>
        /// <param name="selected{{entity.Name}}DTOList">List of {{entity.Name}}DTOs representing the associations with additional fields</param>
        public async virtual Task Update{{m2mWithManyProperty_1.Type}}ListFor{{m2mWithManyProperty_2.Type}}({{m2mEntityIdType_2}} {{m2mWithManyProperty_2.Name.FirstCharToLower()}}Id, List<{{entity.Name}}DTO> selected{{entity.Name}}DTOList)
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
using {{basePartOfTheNamespace.ReplaceEverythingAfterLast(".", ".Shared")}}.Resources;
using Microsoft.EntityFrameworkCore;
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

        private static string GetAuthorizeEntityMethodCall(string entityName, CrudCodes crudCode, string parametersBody)
        {
            string methodName = Helpers.GetAuthorizeEntityMethodName(entityName, crudCode);
            return $"await _authorizationService.{methodName}({parametersBody});";
        }

        private static string GetFileManagerServiceField(SpiderlyProperty property)
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