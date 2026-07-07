using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Spiderly.SourceGenerators.Net
{
    internal static class ServiceSaveGenerator
    {
        internal static string GetSavingData(SpiderlyClass entity, List<SpiderlyClass> entities)
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
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var poco = await Save{{entity.Name}}(saveDTO, authorizeUpdate, authorizeInsert);

                var dto = poco.Adapt<{{entity.Name}}DTO>(Mapper.{{entity.Name}}ToDTOConfig());

{{ServiceReadGenerator.GetPopulateDTOWithBlobPartsForDTO(entity.Properties)}}

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
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBefore{{entity.Name}}IsMapped(dto);
                DbSet<{{entity.Name}}> dbSet = _deps.Context.DbSet<{{entity.Name}}>();

                if (dto.Id > 0)
                {
                    if (authorizeUpdate)
                    {
                        {{ServicesGenerator.GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Update, "dto")}}
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
                        {{ServicesGenerator.GetAuthorizeEntityMethodCall(entity.Name, CrudCodes.Insert, "dto")}}
                    }

                    poco = dto.Adapt<{{entity.Name}}>(Mapper.{{entity.Name}}DTOToEntityConfig());
                    await OnBefore{{entity.Name}}Insert(poco, dto);
                    await dbSet.AddAsync(poco);
                }

{{string.Join("\n", GetManyToOneInstancesForSave(entity, entities))}}

                await _deps.Context.SaveChangesAsync();

{{string.Join("\n", GetMoveStagedBlobMethods(entity))}}

{{string.Join("\n", GetNonActiveDeleteBlobMethods(entity))}}

{{string.Join("\n", GetNonActiveDeleteEditorImageMethods(entity))}}
            });

            return poco;
        }

        /// <summary>
        /// Lifecycle hook called before the {{entity.Name}}DTO is mapped to the entity.
        /// Override this method to add custom validation or modify the DTO before mapping.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="{{entity.Name.FirstCharToLower()}}DTO">The DTO about to be mapped</param>
        protected virtual async Task OnBefore{{entity.Name}}IsMapped({{entity.Name}}DTO {{entity.Name.FirstCharToLower()}}DTO) { }

        /// <summary>
        /// Lifecycle hook called before updating an existing {{entity.Name}} entity.
        /// Override this method to add custom business logic during updates.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="{{entity.Name.FirstCharToLower()}}">The existing entity being updated</param>
        /// <param name="{{entity.Name.FirstCharToLower()}}DTO">The DTO containing new data</param>
        protected virtual async Task OnBefore{{entity.Name}}Update({{entity.Name}} {{entity.Name.FirstCharToLower()}}, {{entity.Name}}DTO {{entity.Name.FirstCharToLower()}}DTO) { }

        /// <summary>
        /// Lifecycle hook called before inserting a new {{entity.Name}} entity.
        /// Override this method to add custom business logic during inserts.
        /// This method runs inside a database transaction.
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
            new {{entity.Name}}SaveBodyDTOValidationRules().ValidateAndThrow(saveBodyDTO);

            return await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeSave{{entity.Name}}AndReturnMainUIFormDTO(saveBodyDTO);

                var savedDTO = await Save{{entity.Name}}AndReturnDTO(saveBodyDTO.{{entity.Name}}DTO, authorizeUpdate, authorizeInsert);

{{string.Join("\n", GetOrderedOneToManyUpdateVariables(entity, allEntities))}}
{{string.Join("\n", GetManyToManyMultiControlTypesUpdateMethods(entity, allEntities))}}
{{string.Join("\n", GetSimpleManyToManyTableLazyLoad(entity, allEntities))}}
{{string.Join("\n", GetComplexManyToManyListUpdateCalls(entity))}}

                var result = new {{entity.Name}}MainUIFormDTO
                {
                    {{entity.Name}}DTO = savedDTO,
{{string.Join("\n", GetOrderedOneToManySaveBodyDTOVariables(entity, allEntities))}}
{{ServiceReadGenerator.GetMainUIFormDTOInitializationManyToManyPropertiesAfterSave(entity, allEntities)}}
{{string.Join("\n", GetComplexManyToManyListResultProperties(entity))}}
                };

                await OnAfterSave{{entity.Name}}AndReturnMainUIFormDTO(saveBodyDTO, result);

                return result;
            });
        }

{{string.Join("\n", GetOrderedOneToManyUpdateMethods(entity, allEntities))}}
{{string.Join("\n", GetSimpleManyToManyTableLazyLoadGetAllQueryHook(entity, allEntities))}}

        /// <summary>
        /// Lifecycle hook called before saving {{entity.Name}} with MainUIFormDTO.
        /// Override this method to add custom validation or modify the SaveBodyDTO.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity and related data</param>
        protected virtual async Task OnBeforeSave{{entity.Name}}AndReturnMainUIFormDTO({{entity.Name}}SaveBodyDTO saveBodyDTO) { }

        /// <summary>
        /// Lifecycle hook called after saving {{entity.Name}} and after updating related collections.
        /// Override this method to add custom business logic after the main entity is saved.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The original SaveBodyDTO</param>
        /// <param name="mainUIFormDTO">The save result and DTO sent to the UI</param>
        protected virtual async Task OnAfterSave{{entity.Name}}AndReturnMainUIFormDTO({{entity.Name}}SaveBodyDTO saveBodyDTO, {{entity.Name}}MainUIFormDTO mainUIFormDTO) { }
""";
        }

        #region Ordered One To Many

        private static List<string> GetOrderedOneToManyUpdateVariables(SpiderlyClass entity, List<SpiderlyClass> entities, string servicePrefix = "")
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

                result.Add($$"""
                var savedOrdered{{property.Name}}MainUIFormDTO = await {{servicePrefix}}UpdateOrdered{{property.Name}}For{{entity.Name}}(savedDTO.Id, saveBodyDTO.Ordered{{property.Name}}SaveBodyDTO);
""");
            }

            return result;
        }

        private static List<string> GetOrderedOneToManySaveBodyDTOVariables(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

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
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, allEntities);

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

            return await _deps.Context.WithTransactionAsync(async () =>
            {
                await _deps.Context.DbSet<{{extractedEntity.Name}}>().Where(x => {{extractedEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, property.Name).GetForeignKeyAccessExpression(extractedEntity, allEntities)}} == id && orderedItemIds.Contains(x.Id) == false).ExecuteDeleteAsync();

                var childService = _deps.ServiceProvider.GetRequiredService<{{extractedEntity.Name}}ServiceGenerated>();
                var savedOrderedItemsDTO = new List<{{extractedEntity.Name}}MainUIFormDTO>();

                for (int i = 0; i < orderedItemsDTO.Count; i++)
                {
                    var saveBodyDTO = orderedItemsDTO[i];
                    var DTO = saveBodyDTO.{{extractedEntity.Name}}DTO;

                    DTO.{{extractedEntity.GetManyToOnePropertyWithManyAttribute(entity.Name, property.Name)?.Name}}Id = id;
                    DTO.OrderNumber = i + 1;

                    var savedDTO = await childService.Save{{extractedEntity.Name}}AndReturnDTO(DTO, false, false);

{{string.Join("\n", GetOrderedOneToManyUpdateVariables(extractedEntity, allEntities, "childService."))}}
{{string.Join("\n", GetComplexManyToManyListUpdateCalls(extractedEntity, "saveBodyDTO", "childService."))}}

                    savedOrderedItemsDTO.Add(new {{extractedEntity.Name}}MainUIFormDTO
                    {
                        {{extractedEntity.Name}}DTO = savedDTO,
{{string.Join("\n", GetOrderedOneToManySaveBodyDTOVariables(extractedEntity, allEntities))}}
{{string.Join("\n", GetComplexManyToManyListResultProperties(extractedEntity, "childService."))}}
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
                throw new SecurityViolationException("The ordered {{property.Name}} for {{entity.Name}} can't be empty.");
""";

            }

            return null;
        }

        #endregion

        #region Many To Many

        private static List<string> GetManyToManyMultiControlTypesUpdateMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x => x.HasExcludeServiceMethodsFromGenerationAttribute() == false)
            )
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

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x => x.HasSimpleManyToManyTableLazyLoadAttribute())
            )
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

                result.Add($$"""
                var all{{property.Name}}Query = await GetAll{{property.Name}}QueryFor{{entity.Name}}(_deps.Context.DbSet<{{extractedEntity.Name}}>());
                var {{property.Name.FirstCharToLower()}}Service = _deps.ServiceProvider.GetRequiredService<{{extractedEntity.Name}}ServiceGenerated>();
                var {{property.Name.FirstCharToLower()}}PaginatedResult = await {{property.Name.FirstCharToLower()}}Service.GetPaginated{{extractedEntity.Name}}Result(saveBodyDTO.{{property.Name}}TableFilter, all{{property.Name}}Query);
                await Update{{property.Name}}WithLazyTableSelectionFor{{entity.Name}}({{property.Name.FirstCharToLower()}}PaginatedResult.Query, savedDTO.Id, saveBodyDTO);
""");

            }

            return result;
        }

        private static List<string> GetSimpleManyToManyTableLazyLoadGetAllQueryHook(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x => x.HasSimpleManyToManyTableLazyLoadAttribute())
            )
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

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

            return result;
        }

        private static List<string> GetComplexManyToManyListUpdateCalls(SpiderlyClass entity, string saveBodyDTOVariable = null, string servicePrefix = "")
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetComplexManyToManyListProperties())
            {
                string saveBodyAccess = saveBodyDTOVariable ?? "saveBodyDTO";

                result.Add($$"""
                await {{servicePrefix}}Update{{property.Name}}For{{entity.Name}}(savedDTO.Id, {{saveBodyAccess}}.{{property.Name}});
""");
            }

            return result;
        }

        private static List<string> GetComplexManyToManyListResultProperties(SpiderlyClass entity, string servicePrefix = "")
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetComplexManyToManyListProperties())
            {
                result.Add($$"""
                    {{property.Name}} = await {{servicePrefix}}Get{{property.Name}}For{{entity.Name}}(savedDTO.Id),
""");
            }

            return result;
        }

        #endregion

        /// <summary>
        /// Emits one <c>FindAsync</c> + navigation-attach block per M2O relationship.
        ///
        /// This is intentionally not optimized into a direct scalar-FK assignment, even when the
        /// entity declares an explicit FK property. Two reasons to keep the load:
        ///   1. <c>Save{Entity}AndReturnDTO</c> maps the saved entity back through
        ///      <c>{Entity}ToDTOConfig</c>, which reads <c>src.Nav.{DisplayName}</c> to populate
        ///      <c>{Nav}DisplayName</c> fields on the response DTO. Skipping the load leaves those
        ///      fields null — the admin grids and autocomplete chips would render empty.
        ///   2. The generated CRUD path is an admin-panel path. Volume is low and latency is not
        ///      user-facing, so the readability of "load parent, assign nav" wins over shaving a
        ///      roundtrip. Hot paths (storefront order placement, bulk sync) are hand-written —
        ///      those can assign the explicit FK scalar directly and skip this detour.
        ///
        /// If you ever need to change this, also rework <c>Save{Entity}AndReturnDTO</c> to do a
        /// single post-save <c>Include</c>-query for the navs referenced by DisplayName mappings.
        /// </summary>
        private static List<string> GetManyToOneInstancesForSave(SpiderlyClass entityClass, List<SpiderlyClass> allEntityClasses)
        {
            List<string> result = new();

            // Hydrate each FK-bearing reference nav (M2O + 1-1 dependent) from dto.{Nav}Id. Critical for a
            // shadow-FK 1-1 dependent (no {Nav}Id scalar) — otherwise the FK is dropped on insert/update.
            List<SpiderlyProperty> properties = entityClass.Properties
                .Where(prop => prop.IsForeignKeyReferenceNav())
                .ToList();

            foreach (SpiderlyProperty prop in properties)
            {
                SpiderlyClass classOfManyToOneProperty = GetClassOfManyToOneProperty(prop.Type.Raw, allEntityClasses);

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
                await {{ServicesGenerator.GetFileManagerServiceField(property)}}.DeleteNonActiveBlobs(dto.{{property.Name}}, nameof({{entity.Name}}), nameof({{entity.Name}}.{{property.Name}}), poco.Id.ToString());
""");
            }

            return result;
        }

        /// <summary>
        /// Emits code that — once the entity has a real id — moves blobs uploaded into the
        /// `_tmp/` staging prefix to their permanent entity-scoped path. No-op for blobs
        /// already at their permanent path (the move method short-circuits). The trailing
        /// SaveChanges is guarded by a flag so the common no-staged-upload path doesn't
        /// incur a pointless round-trip.
        /// </summary>
        private static List<string> GetMoveStagedBlobMethods(SpiderlyClass entity)
        {
            List<string> result = new();

            List<SpiderlyProperty> blobProperties = Helpers.GetBlobProperties(entity.Properties);

            if (blobProperties.Count == 0)
                return result;

            result.Add("                bool anyBlobMoved = false;");

            foreach (SpiderlyProperty property in blobProperties)
            {
                result.Add($$"""
                if (!string.IsNullOrEmpty(poco.{{property.Name}}))
                {
                    string moved{{property.Name}} = await {{ServicesGenerator.GetFileManagerServiceField(property)}}.MoveBlobToEntityPathAsync(poco.{{property.Name}}, nameof({{entity.Name}}), nameof({{entity.Name}}.{{property.Name}}), poco.Id.ToString());
                    if (moved{{property.Name}} != poco.{{property.Name}})
                    {
                        poco.{{property.Name}} = moved{{property.Name}};
                        dto.{{property.Name}} = moved{{property.Name}};
                        anyBlobMoved = true;
                    }
                }
""");
            }

            result.Add("                if (anyBlobMoved) await _deps.Context.SaveChangesAsync();");

            return result;
        }

        private static List<string> GetNonActiveDeleteEditorImageMethods(SpiderlyClass entity)
        {
            List<string> result = new();

            List<SpiderlyProperty> editorProperties = Helpers.GetEditorImageProperties(entity.Properties);

            foreach (SpiderlyProperty property in editorProperties)
            {
                result.Add($$"""
                List<string> active{{property.Name}}ImageUrls = Helper.ExtractImageUrlsFromHtml(dto.{{property.Name}});
                await _s3PublicStorageService.DeleteNonActiveEditorImages(active{{property.Name}}ImageUrls, nameof({{entity.Name}}), nameof({{entity.Name}}.{{property.Name}}) + "Image", poco.Id.ToString());
""");
            }

            return result;
        }

        internal static List<string> GetUploadBlobMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
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
                {{ServicesGenerator.GetAuthorizeEntityMethodCall($"{property.Name}For{entity.Name}", CrudCodes.Update, "id")}}
            }
            else if (authorizeInsert)
            {
                {{ServicesGenerator.GetAuthorizeEntityMethodCall($"{property.Name}For{entity.Name}", CrudCodes.Insert, "")}}
            }
{{GetFileSizeValidation(property)}}
{{GetFileTypeValidation(property, entity)}}
            string fileName;

            using (Stream stream = file.OpenReadStream())
            {
                byte[] byteArray = await OnBefore{{property.Name}}BlobFor{{entity.Name}}IsUploaded(stream, file, id);

                using (Stream updatedStream = new MemoryStream(byteArray))
                {
                    fileName = await {{ServicesGenerator.GetFileManagerServiceField(property)}}.UploadFileAsync(file.FileName, nameof({{entity.Name}}), nameof({{entity.Name}}.{{property.Name}}), id.ToString(), updatedStream);
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
        /// Default implementation validates and optimizes images. Override to customize file processing.
        /// </summary>
        /// <param name="stream">The file stream</param>
        /// <param name="file">The form file</param>
        /// <param name="id">The entity ID</param>
        /// <returns>Processed file bytes</returns>
        public virtual async Task<byte[]> OnBefore{{property.Name}}BlobFor{{entity.Name}}IsUploaded (Stream stream, IFormFile file, {{entityIdType}} id)
        {
            if (Helper.IsOptimizableImage(file.ContentType)) // rasters only — SVG can't go through ImageSharp, it passes through raw
            {
                await ValidateImageFor{{property.Name}}Of{{entity.Name}}(stream, file, id);
                stream.Position = 0;
                return await OptimizeImageFor{{property.Name}}Of{{entity.Name}}(stream, file, id);
            }
            else
            {
                return await Helper.ReadAllBytesAsync(stream);
            }
        }

        /// <summary>
        /// Validates image dimensions and other constraints for the {{property.Name}} property.
        /// Override to customize validation logic (e.g., different dimension requirements, aspect ratio checks).
        /// </summary>
        /// <param name="stream">The image stream</param>
        /// <param name="file">The form file</param>
        /// <param name="id">The entity ID</param>
        public virtual async Task ValidateImageFor{{property.Name}}Of{{entity.Name}}(Stream stream, IFormFile file, {{entityIdType}} id)
        {
{{GetImageDimensionsValidation(property)}}
        }

        /// <summary>
        /// Optimizes the image for the {{property.Name}} property.
        /// Override to customize optimization (e.g., different quality, resizing, format conversion).
        /// </summary>
        /// <param name="stream">The image stream</param>
        /// <param name="file">The form file</param>
        /// <param name="id">The entity ID</param>
        /// <returns>Optimized image bytes</returns>
        public virtual async Task<byte[]> OptimizeImageFor{{property.Name}}Of{{entity.Name}}(Stream stream, IFormFile file, {{entityIdType}} id)
        {
            return await Helper.OptimizeImage(stream);
        }
"""
);
            }

            return result;
        }

        private static string GetImageDimensionsValidation(SpiderlyProperty property)
        {
            int imageWidth = property.GetImageWidth();
            int imageHeight = property.GetImageHeight();

            if (imageWidth == 0 && imageHeight == 0)
                return "";

            return $"""
            await Helper.ValidateImageDimensions(stream, width: {imageWidth}, height: {imageHeight}, _deps.Localizer);
""";
        }

        private static string GetFileSizeValidation(SpiderlyProperty property)
        {
            int maxFileSize = property.GetMaxFileSize();

            if (maxFileSize == 0)
                maxFileSize = 20_000_000; // 20 MB default when [MaxFileSize] not specified

            return $"""

            Helper.ValidateFileSize(file.Length, {maxFileSize}, _deps.Localizer);
""";
        }

        /// <summary>
        /// Emits server-side MIME + magic-byte validation. MIME types are taken from
        /// <c>[AcceptedFileTypes]</c>, filtered to actual MIME strings — extensions like
        /// ".pdf" are stripped. The attribute is mandatory on every blob property:
        /// missing or extension-only values raise <see cref="SpiderlyDiagnostics.BlobPropertyMissingAcceptedFileTypes"/>
        /// (<c>SPIDERLY014</c>) so the build fails until an explicit whitelist is declared.
        /// </summary>
        private static string GetFileTypeValidation(SpiderlyProperty property, SpiderlyClass entity)
        {
            List<string> attributeValues = property.GetAcceptedFileTypes();
            List<string> mimeTypes = attributeValues?
                .Where(v => v.Contains('/'))
                .ToList();

            if (mimeTypes == null || mimeTypes.Count == 0)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.BlobPropertyMissingAcceptedFileTypes,
                    property.Location ?? entity.Location,
                    entity.Name, property.Name);
            }

            string joined = string.Join(", ", mimeTypes.Select(t => $"\"{t}\""));
            string allowedMimeTypesExpression = $"new[] {{ {joined} }}";

            return $$"""

            using (Stream signatureStream = file.OpenReadStream())
            {
                await Helper.ValidateFileSignature(signatureStream, file.ContentType, {{allowedMimeTypesExpression}}, _deps.Localizer);
            }
""";
        }

        internal static List<string> GetUploadEditorImageMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            string entityIdType = entity.GetIdType(allEntities);

            List<SpiderlyProperty> editorProperties = Helpers.GetEditorImageProperties(entity.Properties);

            foreach (SpiderlyProperty property in editorProperties)
            {
                result.Add($$"""
        public virtual async Task<EditorImageUploadResultDTO> Upload{{property.Name}}ImageFor{{entity.Name}}(IFormFile file, bool authorizeUpdate, bool authorizeInsert)
        {
            {{entityIdType}} id = Helper.GetObjectIdFromFileName<{{entityIdType}}>(file.FileName);

            if (id > 0 && authorizeUpdate)
            {
                {{ServicesGenerator.GetAuthorizeEntityMethodCall($"{property.Name}ImageFor{entity.Name}", CrudCodes.Update, "id")}}
            }
            else if (authorizeInsert)
            {
                {{ServicesGenerator.GetAuthorizeEntityMethodCall($"{property.Name}ImageFor{entity.Name}", CrudCodes.Insert, "")}}
            }
{{GetFileSizeValidation(property)}}
{{GetFileTypeValidation(property, entity)}}
            string imageUrl;
            int imageWidth;
            int imageHeight;

            using (Stream stream = file.OpenReadStream())
            {
                byte[] byteArray;

                if (Helper.IsOptimizableImage(file.ContentType))
                {
                    (byteArray, imageWidth, imageHeight) = await Helper.OptimizeImageWithDimensions(stream);
                }
                else
                {
                    // SVG: no ImageSharp decode — upload as-is; intrinsic size best-effort (0,0 when unknown)
                    (imageWidth, imageHeight) = Helper.GetSvgDimensions(stream);
                    byteArray = await Helper.ReadAllBytesAsync(stream);
                }

                using (Stream updatedStream = new MemoryStream(byteArray))
                {
                    imageUrl = await _s3PublicStorageService.UploadFileAsync(file.FileName, nameof({{entity.Name}}), nameof({{entity.Name}}.{{property.Name}}) + "Image", id.ToString(), updatedStream);
                }
            }

            return new EditorImageUploadResultDTO
            {
                Url = imageUrl,
                Width = imageWidth,
                Height = imageHeight,
            };
        }
""");
            }

            return result;
        }
    }
}
