//HintName: TagService.generated.cs
using TestApp.Business.ValidationRules;
using TestApp.Business.DataMappers;
using TestApp.Business.DTO;
using TestApp.Business.Entities;
using TestApp.Business.Enums;
using TestApp.Business.ExcelProperties;
using TestApp.Business.Filtering;
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

namespace TestApp.Business.Services
{
    /// <summary>
    /// Generated service for the Tag entity. Override lifecycle hooks
    /// by creating a <c>TagService</c> class that inherits from this class.
    /// </summary>
    public class TagServiceGenerated : ServiceBase
    {
        protected readonly EntityServiceDependencies _deps;


        public TagServiceGenerated(EntityServiceDependencies deps) : base(deps.Context, deps.Localizer)
        {
            _deps = deps;

        }

        #region Read

        /// <summary>
        /// Retrieves the complete MainUIFormDTO for Tag, including the entity DTO and all related collections (one-to-many, many-to-many).
        /// </summary>
        /// <param name="id">The ID of the Tag entity</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>TagMainUIFormDTO containing the entity DTO and related data</returns>
        public async virtual Task<TagMainUIFormDTO> GetTagMainUIFormDTO(long id, bool authorize)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeTagReadAndThrow(id);
                }

                var result = new TagMainUIFormDTO
                {
                    TagDTO = await GetTagDTO(id, false),
                };

                await OnAfterGetTagMainUIFormDTO(result);

                return result;
            });
        }

        /// <summary>
        /// Lifecycle hook called after retrieving Tag MainUIFormDTO.
        /// Override this method to enrich the MainUIFormDTO with additional data (e.g., computed fields, extra queries).
        /// This method runs inside a database transaction.
        /// </summary>
        /// <example>
        /// protected override async Task OnAfterGetTagMainUIFormDTO(TagMainUIFormDTO mainUIFormDTO)
        /// {
        ///     mainUIFormDTO.CustomProperty = await _deps.Context.DbSet&lt;OtherEntity&gt;().Where(x => x.TagId == mainUIFormDTO.TagDTO.Id).CountAsync();
        /// }
        /// </example>
        /// <param name="mainUIFormDTO">The MainUIFormDTO that was just constructed with entity and related data</param>
        protected virtual async Task OnAfterGetTagMainUIFormDTO(TagMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Retrieves a single Tag entity as a DTO with blob data populated.
        /// </summary>
        /// <param name="id">The ID of the Tag entity</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>TagDTO with all blob properties populated</returns>
        public async virtual Task<TagDTO> GetTagDTO(long id, bool authorize)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeTagReadAndThrow(id);
                }

                var dto = await _deps.Context.DbSet<Tag>()
                    .AsNoTracking()
                    .Where(x => x.Id == id).ProjectToType<TagDTO>(Mapper.TagProjectToConfig())
                    .SingleOrDefaultAsync();

                if (dto == null)
                    throw new BusinessException(_deps.Localizer["EntityDoesNotExistInDatabase"]);



                return dto;
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Tag entities.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<Tag>> GetPaginatedTagList(FilterDTO filterDTO, IQueryable<Tag> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Tag DTOs with blob data populated.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>PaginatedResultDTO containing TagDTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<TagDTO>> GetPaginatedTagList(FilterDTO filterDTO, IQueryable<Tag> query, bool authorize)
        {
            PaginatedResult<Tag> paginationResult = new();
            List<TagDTO> dtoList = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginatedTagList(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<TagDTO>(Mapper.TagProjectToConfig())
                    .ToListAsync();

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeTagReadAndThrow(dtoList.Select(x => x.Id).ToList());
                }


            });

            return new PaginatedResultDTO<TagDTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of Tag entities to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> ExportTagListToExcel(FilterDTO filterDTO, IQueryable<Tag> query, bool authorize, CancellationToken cancellationToken = default)
        {
            IQueryable<TagDTO> exportQuery = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                PaginatedResult<Tag> paginationResult = await GetPaginatedTagList(filterDTO, query);
                int maxRows = Spiderly.Shared.SettingsProvider.Current.ExcelExportMaxRows;
                exportQuery = paginationResult.Query
                    .OrderBy(x => x.Id)
                    .Take(maxRows)
                    .ProjectToType<TagDTO>(Mapper.TagExcelProjectToConfig());
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new TagDTO());
            return await _deps.ExcelService.FillReportTemplateAsync(
                exportQuery.AsAsyncEnumerable(),
                excelPropertiesToExclude,
                _deps.Localizer,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves a list of Tag entities without pagination.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>List of Tag entities</returns>
        public async virtual Task<List<Tag>> GetTagList(IQueryable<Tag> query, bool authorize)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = await query
                    .ToListAsync();

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeTagReadAndThrow(result.Select(x => x.Id).ToList());
                }

                return result;
            });
        }

        /// <summary>
        /// Retrieves a list of Tag DTOs without pagination, with blob data populated.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>List of TagDTO with blob properties populated</returns>
        public async virtual Task<List<TagDTO>> GetTagDTOList(IQueryable<Tag> query, bool authorize)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dtoList = await query
                    .AsNoTracking()
                    .ProjectToType<TagDTO>(Mapper.TagToDTOConfig())
                    .ToListAsync();

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeTagReadAndThrow(dtoList.Select(x => x.Id).ToList());
                }



                return dtoList;
            });
        }



        #endregion

        #region Save

        /// <summary>
        /// Saves a Tag entity and returns the complete MainUIFormDTO including all related collections.
        /// Handles insert/update logic, many-to-many relationships, and ordered one-to-many collections.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity data and related selections</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>TagMainUIFormDTO with saved data and updated collections</returns>
        public virtual async Task<TagMainUIFormDTO> SaveTagAndReturnMainUIFormDTO(TagSaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            new TagSaveBodyDTOValidationRules().ValidateAndThrow(saveBodyDTO);

            return await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeSaveTagAndReturnMainUIFormDTO(saveBodyDTO);

                var savedDTO = await SaveTagAndReturnDTO(saveBodyDTO.TagDTO, authorizeUpdate, authorizeInsert);






                var result = new TagMainUIFormDTO
                {
                    TagDTO = savedDTO,



                };

                await OnAfterSaveTagAndReturnMainUIFormDTO(saveBodyDTO, result);

                return result;
            });
        }




        /// <summary>
        /// Lifecycle hook called before saving Tag with MainUIFormDTO.
        /// Override this method to add custom validation or modify the SaveBodyDTO.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity and related data</param>
        protected virtual async Task OnBeforeSaveTagAndReturnMainUIFormDTO(TagSaveBodyDTO saveBodyDTO) { }

        /// <summary>
        /// Lifecycle hook called after saving Tag and after updating related collections.
        /// Override this method to add custom business logic after the main entity is saved.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The original SaveBodyDTO</param>
        /// <param name="mainUIFormDTO">The save result and DTO sent to the UI</param>
        protected virtual async Task OnAfterSaveTagAndReturnMainUIFormDTO(TagSaveBodyDTO saveBodyDTO, TagMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Saves a Tag entity and returns the DTO with blob data populated.
        /// </summary>
        /// <param name="saveDTO">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved TagDTO with blob properties populated</returns>
        public async virtual Task<TagDTO> SaveTagAndReturnDTO(TagDTO saveDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var poco = await SaveTag(saveDTO, authorizeUpdate, authorizeInsert);

                var dto = poco.Adapt<TagDTO>(Mapper.TagToDTOConfig());



                return dto;
            });
        }

        /// <summary>
        /// Core save method that handles both insert and update operations for Tag.
        /// Validates the DTO, maps to entity, handles many-to-one relationships, and manages blob deletion.
        /// </summary>
        /// <param name="dto">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved Tag entity</returns>
        public async virtual Task<Tag> SaveTag(TagDTO dto, bool authorizeUpdate, bool authorizeInsert)
        {
            TagDTOValidationRules validationRules = new TagDTOValidationRules();
            validationRules.ValidateAndThrow(dto);

            Tag poco = null;
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeTagIsMapped(dto);
                DbSet<Tag> dbSet = _deps.Context.DbSet<Tag>();

                if (dto.Id > 0)
                {
                    if (authorizeUpdate)
                    {
                        await _deps.AuthorizationService.AuthorizeTagUpdateAndThrow(dto);
                    }

                    poco = await GetInstanceAsync<Tag, long>(dto.Id, dto.Version);
                    await OnBeforeTagUpdate(poco, dto);
                    dto.Adapt(poco, Mapper.TagDTOToEntityConfig());
                    dbSet.Update(poco);
                }
                else
                {
                    if (authorizeInsert)
                    {
                        await _deps.AuthorizationService.AuthorizeTagInsertAndThrow(dto);
                    }

                    poco = dto.Adapt<Tag>(Mapper.TagDTOToEntityConfig());
                    await OnBeforeTagInsert(poco, dto);
                    await dbSet.AddAsync(poco);
                }



                await _deps.Context.SaveChangesAsync();






            });

            return poco;
        }

        /// <summary>
        /// Lifecycle hook called before the TagDTO is mapped to the entity.
        /// Override this method to add custom validation or modify the DTO before mapping.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="tagDTO">The DTO about to be mapped</param>
        protected virtual async Task OnBeforeTagIsMapped(TagDTO tagDTO) { }

        /// <summary>
        /// Lifecycle hook called before updating an existing Tag entity.
        /// Override this method to add custom business logic during updates.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="tag">The existing entity being updated</param>
        /// <param name="tagDTO">The DTO containing new data</param>
        protected virtual async Task OnBeforeTagUpdate(Tag tag, TagDTO tagDTO) { }

        /// <summary>
        /// Lifecycle hook called before inserting a new Tag entity.
        /// Override this method to add custom business logic during inserts.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="tag">The new entity being inserted</param>
        /// <param name="tagDTO">The DTO containing the data</param>
        protected virtual async Task OnBeforeTagInsert(Tag tag, TagDTO tagDTO) { }





        #endregion

        #region Delete

        /// <summary>
        /// Per-id variant of the pre-delete hook. By default forwards to
        /// <see cref="OnBeforeTagListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity being deleted</param>
        public virtual Task OnBeforeTagDelete(long id) =>
            OnBeforeTagListDelete(id.StructToList());

        /// <summary>
        /// Deletes a single Tag entity with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="id">The ID of the entity to delete</param>
        /// <param name="authorize">Whether to perform authorization check for Delete operation</param>
        public async virtual Task DeleteTag(long id, bool authorize)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeTagDelete(id);

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeTagDeleteAndThrow(id);
                }

                List<long> listForDelete_1 = id.StructToList();



                await DeleteEntityAsync<Tag, long>(id);
            });
        }

        /// <summary>
        /// Lifecycle hook called before deleting a list of Tag entities.
        /// Override this to add custom validation or business logic before batch deletion.
        /// </summary>
        /// <param name="listForDelete">The list of entity IDs being deleted</param>
        public virtual async Task OnBeforeTagListDelete(List<long> listForDelete) { }

        /// <summary>
        /// Deletes multiple Tag entities with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="listForDelete_1">The list of entity IDs to delete</param>
        /// <param name="authorize">Whether to perform authorization check for Delete operation</param>
        public async virtual Task DeleteTagList(List<long> listForDelete_1, bool authorize)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeTagListDelete(listForDelete_1);

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeTagDeleteAndThrow(listForDelete_1);
                }



                await DeleteEntitiesAsync<Tag, long>(listForDelete_1);
            });
        }

        #endregion

        #region One To Many



        #endregion

    }
}