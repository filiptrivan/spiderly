using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Net
{
    internal static class ServiceOneToManyGenerator
    {
        internal static List<string> GetOneToManyMethods(SpiderlyClass entity, List<SpiderlyClass> allEntityClasses)
        {
            string entityIdType = entity.GetIdType(allEntityClasses);

            List<string> result = new();

            foreach (SpiderlyProperty oneToManyProperty in entity.Properties.Where(prop => prop.Type.IsOneToManyType())) // List<Role> Roles
            {
                SpiderlyClass extractedPropertyEntity = allEntityClasses.Single(x => x.Name == Helpers.ExtractTypeFromGenericType(oneToManyProperty.Type)); // Role
                string extractedPropertyEntityIdType = extractedPropertyEntity.GetIdType(allEntityClasses); // int

                if (extractedPropertyEntity.HasM2MAttribute()) // Complex M2M
                {
                    SpiderlyProperty m2mProperty = extractedPropertyEntity.Properties
                        .SingleOrDefault(x =>
                            x.HasM2MWithManyAttribute() &&
                            x.Type.Name == entity.Name &&
                            x.Attributes.Any(x => x.Name == "M2MWithMany" && x.Value == oneToManyProperty.Name)
                        );

                    if (m2mProperty == null)
                    {
                        throw SpiderlyDiagnostics.Create(
                            SpiderlyDiagnostics.OneToManyMissingM2MWithMany,
                            (oneToManyProperty.Location ?? entity.Location)!, // Both nullable; Create() falls back to Location.None internally
                            entity.Name);
                    }

                    if (oneToManyProperty.HasComplexManyToManyReadonlyTableAttribute())
                    {
                        result.Add(GetPaginatedListForComplexM2MMethod(extractedPropertyEntity, oneToManyProperty, m2mProperty, entity, allEntityClasses));
                    }
                    else if (oneToManyProperty.HasComplexManyToManyListAttribute())
                    {
                        result.Add(GetComplexManyToManyListMethods(extractedPropertyEntity, oneToManyProperty, m2mProperty, entity, allEntityClasses));
                    }

                    continue;
                }

                string extractedPropertyEntityDisplayName = ClassAnalyzer.GetDisplayNameProperty(extractedPropertyEntity); // Name

                SpiderlyProperty manyToOneProperty = extractedPropertyEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, oneToManyProperty.Name); // Many to one property from the other side
                SpiderlyProperty? extractedEntityManyToManyProperty = Helpers.GetOppositeManyToManyProperty(oneToManyProperty, extractedPropertyEntity, entity, allEntityClasses);

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
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
        public async virtual Task<List<NamebookDTO<{{extractedPropertyEntityIdType}}>>> Get{{oneToManyProperty.Name}}NamebookListFor{{entity.Name}}({{entityIdType}} id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await _deps.Context.DbSet<{{extractedPropertyEntity.Name}}>()
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
        /// <returns>List of entity IDs</returns>
        public async virtual Task<List<{{extractedPropertyEntityIdType}}>> Get{{oneToManyProperty.Name}}IdsFor{{entity.Name}}({{entityIdType}} id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await _deps.Context.DbSet<{{extractedPropertyEntity.Name}}>()
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
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await _deps.Context.DbSet<{{extractedPropertyEntity.Name}}>()
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

            await _deps.Context.WithTransactionAsync(async () =>
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

                var listToInsert = await _deps.Context.DbSet<{{extractedPropertyEntity.Name}}>().Where(x => selectedIdsHelper.Contains(x.Id)).ToListAsync();

                entity.{{oneToManyProperty.Name}}.AddRange(listToInsert);
                await _deps.Context.SaveChangesAsync();
            });
        }

        /// <summary>
        /// Lazy loads selected IDs for many-to-many relationship with pagination support.
        /// IMPORTANT: The query must be ordered by the same field as the table data for correct results.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query (must be ordered)</param>
        /// <returns>LazyLoadSelectedIdsResultDTO containing selected IDs and total count</returns>
        public async virtual Task<LazyLoadSelectedIdsResultDTO<{{extractedPropertyEntityIdType}}>> LazyLoadSelected{{oneToManyProperty.Name}}IdsFor{{entity.Name}}(FilterDTO filterDTO, IQueryable<{{extractedPropertyEntity.Name}}> query)
        {
            LazyLoadSelectedIdsResultDTO<{{extractedPropertyEntityIdType}}> lazyLoadSelectedIdsResultDTO = new();

            query = query
                .Skip(filterDTO.First)
                .Take(filterDTO.Rows)
                .Where(x => x.{{extractedEntityManyToManyProperty.Name}}
                    .Any(x => x.Id == filterDTO.{{entityIdType.GetTableFilterAdditionalFilterPropertyName()}}));

            await _deps.Context.WithTransactionAsync(async () =>
            {
                var {{extractedPropertyEntity.Name.FirstCharToLower()}}Service = _deps.ServiceProvider.GetRequiredService<{{extractedPropertyEntity.Name}}ServiceGenerated>();
                var paginationResult = await {{extractedPropertyEntity.Name.FirstCharToLower()}}Service.GetPaginated{{extractedPropertyEntity.Name}}Result(filterDTO, query);

                lazyLoadSelectedIdsResultDTO.SelectedIds = await paginationResult.Query
                    .Select(x => x.Id)
                    .ToListAsync();

                int count = await _deps.Context.DbSet<{{extractedPropertyEntity.Name}}>()
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
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await _deps.Context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .Where(x => {{manyToOneProperty.GetForeignKeyAccessExpression(extractedPropertyEntity, entities)}} == id)
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
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
        public async virtual Task<List<NamebookDTO<{{extractedPropertyEntityIdType}}>>> Get{{oneToManyProperty.Name}}NamebookListFor{{entity.Name}}({{entity.GetIdType(entities)}} id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await _deps.Context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .AsNoTracking()
                    .Where(x => {{manyToOneProperty.GetForeignKeyAccessExpression(extractedPropertyEntity, entities)}} == id)
                    .Select(x => new NamebookDTO<{{extractedPropertyEntityIdType}}>
                    {
                        Id = x.Id,
                        DisplayName = x.{{ClassAnalyzer.GetDisplayNameProperty(extractedPropertyEntity)}},
                    })
                    .ToListAsync();
            });
        }
""";
        }

        private static string GetPaginatedListForComplexM2MMethod(SpiderlyClass listEntitty, SpiderlyProperty oneToManyProperty, SpiderlyProperty m2mProperty, SpiderlyClass entity, List<SpiderlyClass> allEntityClasses)
        {
            bool hasId = listEntitty.Properties.Any(p => p.Name == "Id");

            return $$"""
        /// <summary>
        /// Retrieves a paginated list of {{listEntitty.Name}} entities for a complex many-to-many relationship.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<{{listEntitty.Name}}>> GetPaginated{{oneToManyProperty.Name}}ResultFor{{entity.Name}}(FilterDTO filterDTO, IQueryable<{{listEntitty.Name}}> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of {{listEntitty.Name}} DTOs for a complex many-to-many relationship with blob data.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResultDTO containing {{listEntitty.Name}}DTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<{{listEntitty.Name}}DTO>> GetPaginated{{oneToManyProperty.Name}}ListFor{{entity.Name}}(FilterDTO filterDTO, IQueryable<{{listEntitty.Name}}> query)
        {
            PaginatedResult<{{listEntitty.Name}}> paginationResult = new();
            List<{{listEntitty.Name}}DTO> dtoList = null!;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginated{{oneToManyProperty.Name}}ResultFor{{entity.Name}}(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<{{listEntitty.Name}}DTO>(Mapper.{{listEntitty.Name}}ProjectToConfig())
                    .ToListAsync();

{{ServiceReadGenerator.GetPopulateDTOWithBlobPartsForDTOList(entity.Properties)}}
            });

            return new PaginatedResultDTO<{{listEntitty.Name}}DTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of {{listEntitty.Name}} entities for a complex many-to-many relationship to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> Export{{oneToManyProperty.Name}}ListToExcelFor{{entity.Name}}(FilterDTO filterDTO, IQueryable<{{listEntitty.Name}}> query, CancellationToken cancellationToken = default)
        {
            IQueryable<{{listEntitty.Name}}DTO> exportQuery = null!;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                PaginatedResult<{{listEntitty.Name}}> paginationResult = await GetPaginated{{oneToManyProperty.Name}}ResultFor{{entity.Name}}(filterDTO, query);
                int maxRows = _deps.ExcelSettings.ExcelExportMaxRows;
                exportQuery = paginationResult.Query{{(hasId ? ".OrderBy(x => x.Id)" : "")}}
                    .Take(maxRows)
                    .ProjectToType<{{listEntitty.Name}}DTO>(Mapper.{{listEntitty.Name}}ExcelProjectToConfig());
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new {{listEntitty.Name}}DTO());
            return await _deps.ExcelService.FillReportTemplateAsync(
                exportQuery.AsAsyncEnumerable(),
                excelPropertiesToExclude,
                _deps.Localizer,
                cancellationToken);
        }
""";
        }

        private static string GetComplexManyToManyListMethods(
            SpiderlyClass junctionEntity,
            SpiderlyProperty oneToManyProperty,
            SpiderlyProperty currentSideM2MProperty,
            SpiderlyClass entity,
            List<SpiderlyClass> allEntityClasses)
        {
            string entityIdType = entity.GetIdType(allEntityClasses);

            SpiderlyProperty otherSideM2MProperty = junctionEntity.Properties
                .Where(x => x.HasM2MWithManyAttribute())
                .Single(x => x != currentSideM2MProperty);

            SpiderlyClass otherSideEntity = allEntityClasses.Single(x => x.Name == otherSideM2MProperty.Type.Name);
            string otherSideEntityIdType = otherSideEntity.GetIdType(allEntityClasses);

            string currentSideFKName = $"{currentSideM2MProperty.Name}Id";
            string otherSideFKName = $"{otherSideM2MProperty.Name}Id";

            // The two FK scalars are structural, not data: the current-side FK comes from the method's
            // id parameter and the other-side FK is guarded explicitly below. Mapping them from the DTO
            // crashed on the placeholder rows the generated form posts (their FKs are null by design).
            List<SpiderlyProperty> additionalFields = junctionEntity.Properties
                .Where(p => !p.IsManyToOneType() && !p.Type.IsOneToManyType())
                .Where(p => p.Name != currentSideFKName && p.Name != otherSideFKName)
                .ToList();

            // Without data columns the update below can't tell a linked row from a placeholder
            // (both carry only FKs) and would link every row on save — a simple M2M collection
            // is the right shape for that, so fail the build instead of generating it.
            if (additionalFields.Count == 0)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ComplexManyToManyListWithoutAdditionalFields,
                    (oneToManyProperty.Location ?? entity.Location)!, // Both nullable; Create() falls back to Location.None internally
                    junctionEntity.Name,
                    entity.Name);
            }

            string allFieldsNullCondition = string.Join(" && ", additionalFields.Select(f => $"dto.{f.Name} == null"));

            List<string> requiredFieldGuards = new() { GetRequiredFieldGuard(otherSideFKName) };
            List<string> fieldAssignments = new();
            foreach (SpiderlyProperty field in additionalFields)
            {
                bool needsValueAccess = field.Type.Raw != "string" && field.Type.IsBaseDataType() && !field.Type.Raw.EndsWith("?");
                // With a single additional field the placeholder skip already guarantees it's non-null;
                // with several, a partially-filled row must 422 on missing required fields, not 500 on .Value.
                if (needsValueAccess && additionalFields.Count > 1)
                    requiredFieldGuards.Add(GetRequiredFieldGuard(field.Name));

                fieldAssignments.Add($"                    poco.{field.Name} = dto.{field.Name}{(needsValueAccess ? ".Value" : "")};");
            }

            string rowGuards = string.Join("\n\n", requiredFieldGuards);
            string additionalFieldMappings = string.Join("\n", fieldAssignments);

            return $$"""
        /// <summary>
        /// Retrieves all {{junctionEntity.Name}} DTOs for a {{entity.Name}}, including default records for {{otherSideEntity.Name}} entities without existing junction records.
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <returns>List of {{junctionEntity.Name}}DTO for all {{otherSideEntity.Name}} entities</returns>
        public async virtual Task<List<{{junctionEntity.Name}}DTO>> Get{{oneToManyProperty.Name}}For{{entity.Name}}({{entityIdType}} id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var allOtherSideEntries = await _deps.Context.DbSet<{{otherSideEntity.Name}}>()
                    .AsNoTracking()
                    .OrderBy(x => x.Id)
                    .Select(x => new { x.Id, DisplayName = x.{{ClassAnalyzer.GetDisplayNameProperty(otherSideEntity)}} })
                    .ToListAsync();

                var existingRecords = await _deps.Context.DbSet<{{junctionEntity.Name}}>()
                    .AsNoTracking()
                    .Where(x => {{currentSideM2MProperty.GetForeignKeyAccessExpression(junctionEntity, allEntityClasses)}} == id)
                    .ProjectToType<{{junctionEntity.Name}}DTO>(Mapper.{{junctionEntity.Name}}ProjectToConfig())
                    .ToListAsync();

                var result = new List<{{junctionEntity.Name}}DTO>();

                foreach (var otherSideEntry in allOtherSideEntries)
                {
                    var existing = existingRecords.FirstOrDefault(x => x.{{otherSideFKName}} == otherSideEntry.Id);
                    result.Add(existing ?? new {{junctionEntity.Name}}DTO { {{otherSideFKName}} = otherSideEntry.Id, {{otherSideM2MProperty.Name}}DisplayName = otherSideEntry.DisplayName });
                }

                return result;
            });
        }

        /// <summary>
        /// Updates all {{junctionEntity.Name}} records for a {{entity.Name}} by deleting existing records and inserting new ones.
        /// Rows whose additional columns are all null are skipped — the ComplexManyToManyList form posts a placeholder
        /// row for every {{otherSideEntity.Name}} without a record, and leaving a row blank (or blanking an existing one) means "no record".
        /// </summary>
        /// <param name="id">The ID of the {{entity.Name}} entity</param>
        /// <param name="dtos">List of {{junctionEntity.Name}}DTO to save</param>
        public async virtual Task Update{{oneToManyProperty.Name}}For{{entity.Name}}({{entityIdType}} id, List<{{junctionEntity.Name}}DTO> dtos)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await _deps.Context.DbSet<{{junctionEntity.Name}}>()
                    .Where(x => {{currentSideM2MProperty.GetForeignKeyAccessExpression(junctionEntity, allEntityClasses)}} == id)
                    .ExecuteDeleteAsync();

                var validationRules = new {{junctionEntity.Name}}DTOValidationRules();

                foreach (var dto in dtos)
                {
                    if ({{allFieldsNullCondition}})
                        continue; // Placeholder row (Get/GetDefault emit one per {{otherSideEntity.Name}} without a record) — no data means no record.

                    validationRules.ValidateAndThrow(dto);

{{rowGuards}}

                    var poco = new {{junctionEntity.Name}}();
{{additionalFieldMappings}}

                    var entry = await _deps.Context.DbSet<{{junctionEntity.Name}}>().AddAsync(poco);

                    entry.Property("{{currentSideFKName}}").CurrentValue = id;
                    entry.Property("{{otherSideFKName}}").CurrentValue = dto.{{otherSideFKName}};
                }
            });
        }

        /// <summary>
        /// Returns default junction DTOs for all {{otherSideEntity.Name}} entities (without existing records).
        /// Used when creating a new {{entity.Name}} to pre-populate the ComplexManyToManyList form.
        /// </summary>
        /// <returns>List of {{junctionEntity.Name}}DTO with {{otherSideM2MProperty.Name}}Id and {{otherSideM2MProperty.Name}}DisplayName populated</returns>
        public async virtual Task<List<{{junctionEntity.Name}}DTO>> GetDefault{{oneToManyProperty.Name}}For{{entity.Name}}()
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await _deps.Context.DbSet<{{otherSideEntity.Name}}>()
                    .AsNoTracking()
                    .OrderBy(x => x.Id)
                    .Select(x => new {{junctionEntity.Name}}DTO
                    {
                        {{otherSideFKName}} = x.Id,
                        {{otherSideM2MProperty.Name}}DisplayName = x.{{ClassAnalyzer.GetDisplayNameProperty(otherSideEntity)}},
                    })
                    .ToListAsync();
            });
        }
""";
        }

        /// <summary>
        /// Emits the two-line "throw 422 when the posted DTO property is null" guard used inside the
        /// complex M2M update loop — one emission site so the exception shape can't drift per field.
        /// </summary>
        private static string GetRequiredFieldGuard(string propertyName) => $$"""
                    if (dto.{{propertyName}} == null)
                        throw new SpiderlyValidationException(new Dictionary<string, string[]> { ["{{propertyName}}"] = new[] { "{{propertyName}} is required." } });
""";

        private static string? GetSimpleManyToManyUpdateWithLazyTableSelectionMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
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
            await _deps.Context.WithTransactionAsync(async () =>
            {
                List<{{extractedPropertyEntityIdType}}> listToInsert = null!;

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

        private static string? GetOrderedOneToManyMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
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
        /// <returns>List of {{extractedPropertyEntity.Name}}MainUIFormDTO ordered by OrderNumber</returns>
        public async virtual Task<List<{{extractedPropertyEntity.Name}}MainUIFormDTO>> GetOrdered{{property.Name}}For{{entity.Name}}({{entity.GetIdType(entities)}} id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                List<{{extractedPropertyEntity.Name}}MainUIFormDTO> mainUIFormDTOList = new();

                var ids = await _deps.Context.DbSet<{{extractedPropertyEntity.Name}}>()
                    .AsNoTracking()
                    .Where(x => {{manyToOneProperty.GetForeignKeyAccessExpression(extractedPropertyEntity, entities)}} == id)
                    .OrderBy(x => x.OrderNumber)
                    .Select(x => x.Id)
                    .ToListAsync();

                var {{extractedPropertyEntity.Name.FirstCharToLower()}}Service = _deps.ServiceProvider.GetRequiredService<{{extractedPropertyEntity.Name}}ServiceGenerated>();
                foreach (var id in ids)
                    mainUIFormDTOList.Add(await {{extractedPropertyEntity.Name.FirstCharToLower()}}Service.Get{{extractedPropertyEntity.Name}}MainUIFormDTO(id));

                return mainUIFormDTOList;
            });
        }
""";
        }
    }
}
