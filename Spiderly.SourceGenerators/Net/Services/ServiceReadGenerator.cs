using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Net
{
    internal static class ServiceReadGenerator
    {
        internal static string GetReadBusinessServiceMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            string entityIdType = entity.GetIdType(allEntities);

            return $$"""
        /// <summary>
        /// Retrieves the complete MainUIFormDTO for {{entity.Name}}, including the entity DTO and all related collections (one-to-many, many-to-many).
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <returns>{{entity.Name}}MainUIFormDTO containing the entity DTO and related data</returns>
        public async virtual Task<{{entity.Name}}MainUIFormDTO> Get{{entity.Name}}MainUIFormDTO({{entityIdType}} id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = new {{entity.Name}}MainUIFormDTO
                {
{{GetMainUIFormDTOInitializationProperties(entity, allEntities)}}
                };

                await OnAfterGet{{entity.Name}}MainUIFormDTO(result);

                return result;
            });
        }

        /// <summary>
        /// Lifecycle hook called after retrieving {{entity.Name}} MainUIFormDTO.
        /// Override this method to enrich the MainUIFormDTO with additional data (e.g., computed fields, extra queries).
        /// This method runs inside a database transaction.
        /// </summary>
        /// <example>
        /// protected override async Task OnAfterGet{{entity.Name}}MainUIFormDTO({{entity.Name}}MainUIFormDTO mainUIFormDTO)
        /// {
        ///     mainUIFormDTO.CustomProperty = await _deps.Context.DbSet&lt;OtherEntity&gt;().Where(x => x.{{entity.Name}}Id == mainUIFormDTO.{{entity.Name}}DTO.Id).CountAsync();
        /// }
        /// </example>
        /// <param name="mainUIFormDTO">The MainUIFormDTO that was just constructed with entity and related data</param>
        protected virtual async Task OnAfterGet{{entity.Name}}MainUIFormDTO({{entity.Name}}MainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Retrieves a single {{entity.Name}} entity as a DTO with blob data populated.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <returns>{{entity.Name}}DTO with all blob properties populated</returns>
        public async virtual Task<{{entity.Name}}DTO> Get{{entity.Name}}DTO({{entityIdType}} id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dto = await _deps.Context.DbSet<{{entity.Name}}>()
                    .AsNoTracking()
                    .Where(x => x.Id == id).ProjectToType<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ProjectToConfig())
                    .SingleOrDefaultAsync();

                if (dto == null)
                    throw new BusinessException(_deps.Localizer["EntityDoesNotExistInDatabase"]);

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
        public async virtual Task<PaginatedResult<{{entity.Name}}>> GetPaginated{{entity.Name}}Result(FilterDTO filterDTO, IQueryable<{{entity.Name}}> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of {{entity.Name}} DTOs with blob data populated.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResultDTO containing {{entity.Name}}DTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<{{entity.Name}}DTO>> GetPaginated{{entity.Name}}List(FilterDTO filterDTO, IQueryable<{{entity.Name}}> query)
        {
            PaginatedResult<{{entity.Name}}> paginationResult = new();
            List<{{entity.Name}}DTO> dtoList = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginated{{entity.Name}}Result(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ProjectToConfig())
                    .ToListAsync();

{{GetPopulateDTOWithBlobPartsForDTOList(entity.Properties)}}
            });

            return new PaginatedResultDTO<{{entity.Name}}DTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of {{entity.Name}} entities to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> Export{{entity.Name}}ListToExcel(FilterDTO filterDTO, IQueryable<{{entity.Name}}> query, CancellationToken cancellationToken = default)
        {
            IQueryable<{{entity.Name}}DTO> exportQuery = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                PaginatedResult<{{entity.Name}}> paginationResult = await GetPaginated{{entity.Name}}Result(filterDTO, query);
                int maxRows = _deps.ExcelSettings.ExcelExportMaxRows;
                exportQuery = paginationResult.Query
                    .OrderBy(x => x.Id)
                    .Take(maxRows)
                    .ProjectToType<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ExcelProjectToConfig());
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new {{entity.Name}}DTO());
            return await _deps.ExcelService.FillReportTemplateAsync(
                exportQuery.AsAsyncEnumerable(),
                excelPropertiesToExclude,
                _deps.Localizer,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves a list of {{entity.Name}} entities without pagination.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of {{entity.Name}} entities</returns>
        public async virtual Task<List<{{entity.Name}}>> Get{{entity.Name}}List(IQueryable<{{entity.Name}}> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = await query
                    .ToListAsync();

                return result;
            });
        }

        /// <summary>
        /// Retrieves a list of {{entity.Name}} DTOs without pagination, with blob data populated.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of {{entity.Name}}DTO with blob properties populated</returns>
        public async virtual Task<List<{{entity.Name}}DTO>> Get{{entity.Name}}DTOList(IQueryable<{{entity.Name}}> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dtoList = await query
                    .AsNoTracking()
                    .ProjectToType<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ToDTOConfig())
                    .ToListAsync();

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
                    {{entity.Name}}DTO = await Get{{entity.Name}}DTO(id),
""");

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x =>
                    x.HasUIOrderedOneToManyAttribute() ||
                    x.IsMultiSelectControlType() ||
                    x.IsMultiAutocompleteControlType() ||
                    x.HasComplexManyToManyListAttribute()
                )
            )
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, allEntities);
                string extractedEntityIdType = extractedEntity.GetIdType(allEntities);

                if (property.HasUIOrderedOneToManyAttribute())
                {
                    result.Add($$"""
                    Ordered{{property.Name}}MainUIFormDTO = await GetOrdered{{property.Name}}For{{entity.Name}}(id),
""");
                }
                else if (property.IsMultiSelectControlType())
                {
                    result.Add($$"""
                    {{property.Name}}Ids = await Get{{property.Name}}IdsFor{{entity.Name}}(id),
""");
                }
                else if (property.IsMultiAutocompleteControlType())
                {
                    result.Add($$"""
                    {{property.Name}}NamebookDTOList = await Get{{property.Name}}NamebookListFor{{entity.Name}}(id),
""");
                }
                else if (property.HasComplexManyToManyListAttribute())
                {
                    result.Add($$"""
                    {{property.Name}} = await Get{{property.Name}}For{{entity.Name}}(id),
""");
                }
            }

            return string.Join("\n", result);
        }

        internal static string GetMainUIFormDTOInitializationManyToManyPropertiesAfterSave(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x =>
                    x.IsMultiSelectControlType() ||
                    x.IsMultiAutocompleteControlType()
                )
            )
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, allEntities);
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

        internal static string GetPopulateDTOWithBlobPartsForDTO(List<SpiderlyProperty> propertiesEntityClass)
        {
            List<string> blobParts = GetPopulateDTOWithBlobParts(propertiesEntityClass);

            if (blobParts.Count == 0)
                return null;

            return $$"""
{{string.Join("\n", blobParts)}}
""";
        }

        internal static string GetPopulateDTOWithBlobPartsForDTOList(List<SpiderlyProperty> propertiesEntityClass)
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
                if (property.IsPublicUrl())
                {
                    // Public URLs: pass through directly — the frontend loads from CDN.
                    blobParts.Add($$"""
                    if (!string.IsNullOrEmpty(dto.{{property.Name}}))
                    {
                        dto.{{property.Name}}Data = dto.{{property.Name}};
                    }
""");
                }
                else
                {
                    // TODO: For private S3 storage, generate presigned URLs instead of downloading + base64-encoding.
                    // Private storage: download and base64-encode.
                    blobParts.Add($$"""
                    if (!string.IsNullOrEmpty(dto.{{property.Name}}))
                    {
                        dto.{{property.Name}}Data = await {{ServicesGenerator.GetFileManagerServiceField(property)}}.GetFileDataAsync(dto.{{property.Name}});
                    }
""");
                }
            }

            return blobParts;
        }

        #region Many To One

        internal static string GetManyToOneReadMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            StringBuilder sb = new();

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x =>
                    x.ShouldGenerateAutocompleteControllerMethod() ||
                    x.ShouldGenerateDropdownControllerMethod()
                )
            )
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
            SpiderlyClass autocompleteEntity = allEntities.Single(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));
            string autocompleteEntityIdType = autocompleteEntity.GetIdType(allEntities);
            string autocompleteEntityDisplayName = ClassAnalyzer.GetDisplayNameProperty(autocompleteEntity);

            return $$"""
        /// <summary>
        /// Retrieves autocomplete suggestions for the {{property.Name}} many-to-one relationship in {{entity.Name}}.
        /// </summary>
        /// <param name="limit">Maximum number of results to return</param>
        /// <param name="filter">Text filter for {{autocompleteEntityDisplayName}}</param>
        /// <param name="query">Base query for {{autocompleteEntity.Name}} entities</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
        public async virtual Task<List<NamebookDTO<{{autocompleteEntityIdType}}>>> Get{{property.Name}}AutocompleteListFor{{entity.Name}}(
            int limit,
            string filter,
            IQueryable<{{autocompleteEntity.Name}}> query
        )
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                if (!string.IsNullOrEmpty(filter))
                    query = query.Where(x => x.{{autocompleteEntityDisplayName}}.ToLower().Contains(filter.ToLower()));

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
            SpiderlyClass dropdownEntity = allEntities.Single(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));
            string dropdownEntityIdType = dropdownEntity.GetIdType(allEntities);
            string dropdownDisplayName = ClassAnalyzer.GetDisplayNameProperty(dropdownEntity);

            return $$"""
        /// <summary>
        /// Retrieves dropdown options for the {{property.Name}} many-to-one relationship in {{entity.Name}}.
        /// </summary>
        /// <param name="query">Base query for {{dropdownEntity.Name}} entities</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
        public async virtual Task<List<NamebookDTO<{{dropdownEntityIdType}}>>> Get{{property.Name}}DropdownListFor{{entity.Name}}(
            IQueryable<{{dropdownEntity.Name}}> query
        )
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
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
    }
}
