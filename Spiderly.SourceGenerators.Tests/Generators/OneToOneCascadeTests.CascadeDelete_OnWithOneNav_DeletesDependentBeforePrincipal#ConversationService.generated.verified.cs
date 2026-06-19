//HintName: ConversationService.generated.cs
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
    /// Generated service for the Conversation entity. Override lifecycle hooks
    /// by creating a <c>ConversationService</c> class that inherits from this class.
    /// </summary>
    public class ConversationServiceGenerated : ServiceBase
    {
        protected readonly EntityServiceDependencies _deps;


        public ConversationServiceGenerated(EntityServiceDependencies deps) : base(deps.Context, deps.Localizer)
        {
            _deps = deps;

        }

        #region Read

        /// <summary>
        /// Retrieves the complete MainUIFormDTO for Conversation, including the entity DTO and all related collections (one-to-many, many-to-many).
        /// </summary>
        /// <param name="id">The ID of the Conversation entity</param>
        /// <returns>ConversationMainUIFormDTO containing the entity DTO and related data</returns>
        public async virtual Task<ConversationMainUIFormDTO> GetConversationMainUIFormDTO(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = new ConversationMainUIFormDTO
                {
                    ConversationDTO = await GetConversationDTO(id),
                };

                await OnAfterGetConversationMainUIFormDTO(result);

                return result;
            });
        }

        /// <summary>
        /// Lifecycle hook called after retrieving Conversation MainUIFormDTO.
        /// Override this method to enrich the MainUIFormDTO with additional data (e.g., computed fields, extra queries).
        /// This method runs inside a database transaction.
        /// </summary>
        /// <example>
        /// protected override async Task OnAfterGetConversationMainUIFormDTO(ConversationMainUIFormDTO mainUIFormDTO)
        /// {
        ///     mainUIFormDTO.CustomProperty = await _deps.Context.DbSet&lt;OtherEntity&gt;().Where(x => x.ConversationId == mainUIFormDTO.ConversationDTO.Id).CountAsync();
        /// }
        /// </example>
        /// <param name="mainUIFormDTO">The MainUIFormDTO that was just constructed with entity and related data</param>
        protected virtual async Task OnAfterGetConversationMainUIFormDTO(ConversationMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Retrieves a single Conversation entity as a DTO with blob data populated.
        /// </summary>
        /// <param name="id">The ID of the Conversation entity</param>
        /// <returns>ConversationDTO with all blob properties populated</returns>
        public async virtual Task<ConversationDTO> GetConversationDTO(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dto = await _deps.Context.DbSet<Conversation>()
                    .AsNoTracking()
                    .Where(x => x.Id == id).ProjectToType<ConversationDTO>(Mapper.ConversationProjectToConfig())
                    .SingleOrDefaultAsync();

                if (dto == null)
                    throw new BusinessException(_deps.Localizer["EntityDoesNotExistInDatabase"]);



                return dto;
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Conversation entities.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<Conversation>> GetPaginatedConversationResult(FilterDTO filterDTO, IQueryable<Conversation> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Conversation DTOs with blob data populated.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResultDTO containing ConversationDTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<ConversationDTO>> GetPaginatedConversationList(FilterDTO filterDTO, IQueryable<Conversation> query)
        {
            PaginatedResult<Conversation> paginationResult = new();
            List<ConversationDTO> dtoList = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginatedConversationResult(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<ConversationDTO>(Mapper.ConversationProjectToConfig())
                    .ToListAsync();


            });

            return new PaginatedResultDTO<ConversationDTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of Conversation entities to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> ExportConversationListToExcel(FilterDTO filterDTO, IQueryable<Conversation> query, CancellationToken cancellationToken = default)
        {
            IQueryable<ConversationDTO> exportQuery = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                PaginatedResult<Conversation> paginationResult = await GetPaginatedConversationResult(filterDTO, query);
                int maxRows = _deps.ExcelSettings.ExcelExportMaxRows;
                exportQuery = paginationResult.Query
                    .OrderBy(x => x.Id)
                    .Take(maxRows)
                    .ProjectToType<ConversationDTO>(Mapper.ConversationExcelProjectToConfig());
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new ConversationDTO());
            return await _deps.ExcelService.FillReportTemplateAsync(
                exportQuery.AsAsyncEnumerable(),
                excelPropertiesToExclude,
                _deps.Localizer,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves a list of Conversation entities without pagination.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of Conversation entities</returns>
        public async virtual Task<List<Conversation>> GetConversationList(IQueryable<Conversation> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = await query
                    .ToListAsync();

                return result;
            });
        }

        /// <summary>
        /// Retrieves a list of Conversation DTOs without pagination, with blob data populated.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of ConversationDTO with blob properties populated</returns>
        public async virtual Task<List<ConversationDTO>> GetConversationDTOList(IQueryable<Conversation> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dtoList = await query
                    .AsNoTracking()
                    .ProjectToType<ConversationDTO>(Mapper.ConversationToDTOConfig())
                    .ToListAsync();



                return dtoList;
            });
        }

        /// <summary>
        /// Retrieves autocomplete suggestions for the OwningTaskItem many-to-one relationship in Conversation.
        /// </summary>
        /// <param name="limit">Maximum number of results to return</param>
        /// <param name="filter">Text filter for Title</param>
        /// <param name="query">Base query for TaskItem entities</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
        public async virtual Task<List<NamebookDTO<long>>> GetOwningTaskItemAutocompleteListForConversation(
            int limit,
            string filter,
            IQueryable<TaskItem> query
        )
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                if (!string.IsNullOrEmpty(filter))
                    query = query.Where(x => x.Title.ToLower().Contains(filter.ToLower()));

                var result = await query
                    .AsNoTracking()
                    .Take(limit)
                    .Select(x => new NamebookDTO<long>
                    {
                        Id = x.Id,
                        DisplayName = x.Title,
                    })
                    .ToListAsync();

                return result;
            });
        }


        #endregion

        #region Save

        /// <summary>
        /// Saves a Conversation entity and returns the complete MainUIFormDTO including all related collections.
        /// Handles insert/update logic, many-to-many relationships, and ordered one-to-many collections.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity data and related selections</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>ConversationMainUIFormDTO with saved data and updated collections</returns>
        public virtual async Task<ConversationMainUIFormDTO> SaveConversationAndReturnMainUIFormDTO(ConversationSaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            new ConversationSaveBodyDTOValidationRules().ValidateAndThrow(saveBodyDTO);

            return await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeSaveConversationAndReturnMainUIFormDTO(saveBodyDTO);

                var savedDTO = await SaveConversationAndReturnDTO(saveBodyDTO.ConversationDTO, authorizeUpdate, authorizeInsert);






                var result = new ConversationMainUIFormDTO
                {
                    ConversationDTO = savedDTO,



                };

                await OnAfterSaveConversationAndReturnMainUIFormDTO(saveBodyDTO, result);

                return result;
            });
        }




        /// <summary>
        /// Lifecycle hook called before saving Conversation with MainUIFormDTO.
        /// Override this method to add custom validation or modify the SaveBodyDTO.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity and related data</param>
        protected virtual async Task OnBeforeSaveConversationAndReturnMainUIFormDTO(ConversationSaveBodyDTO saveBodyDTO) { }

        /// <summary>
        /// Lifecycle hook called after saving Conversation and after updating related collections.
        /// Override this method to add custom business logic after the main entity is saved.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The original SaveBodyDTO</param>
        /// <param name="mainUIFormDTO">The save result and DTO sent to the UI</param>
        protected virtual async Task OnAfterSaveConversationAndReturnMainUIFormDTO(ConversationSaveBodyDTO saveBodyDTO, ConversationMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Saves a Conversation entity and returns the DTO with blob data populated.
        /// </summary>
        /// <param name="saveDTO">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved ConversationDTO with blob properties populated</returns>
        public async virtual Task<ConversationDTO> SaveConversationAndReturnDTO(ConversationDTO saveDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var poco = await SaveConversation(saveDTO, authorizeUpdate, authorizeInsert);

                var dto = poco.Adapt<ConversationDTO>(Mapper.ConversationToDTOConfig());



                return dto;
            });
        }

        /// <summary>
        /// Core save method that handles both insert and update operations for Conversation.
        /// Validates the DTO, maps to entity, handles many-to-one relationships, and manages blob deletion.
        /// </summary>
        /// <param name="dto">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved Conversation entity</returns>
        public async virtual Task<Conversation> SaveConversation(ConversationDTO dto, bool authorizeUpdate, bool authorizeInsert)
        {
            ConversationDTOValidationRules validationRules = new ConversationDTOValidationRules();
            validationRules.ValidateAndThrow(dto);

            Conversation poco = null;
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeConversationIsMapped(dto);
                DbSet<Conversation> dbSet = _deps.Context.DbSet<Conversation>();

                if (dto.Id > 0)
                {
                    if (authorizeUpdate)
                    {
                        await _deps.AuthorizationService.AuthorizeConversationUpdateAndThrow(dto);
                    }

                    poco = await GetInstanceAsync<Conversation, long>(dto.Id, dto.Version);
                    await OnBeforeConversationUpdate(poco, dto);
                    dto.Adapt(poco, Mapper.ConversationDTOToEntityConfig());
                    dbSet.Update(poco);
                }
                else
                {
                    if (authorizeInsert)
                    {
                        await _deps.AuthorizationService.AuthorizeConversationInsertAndThrow(dto);
                    }

                    poco = dto.Adapt<Conversation>(Mapper.ConversationDTOToEntityConfig());
                    await OnBeforeConversationInsert(poco, dto);
                    await dbSet.AddAsync(poco);
                }

                if (dto.OwningTaskItemId > 0)
                {
                    poco.OwningTaskItem = await GetInstanceAsync<TaskItem, long>(dto.OwningTaskItemId.Value, null);
                }
                else
                {
                    var _ = poco.OwningTaskItem; // HACK
                    poco.OwningTaskItem = null;
                }

                await _deps.Context.SaveChangesAsync();






            });

            return poco;
        }

        /// <summary>
        /// Lifecycle hook called before the ConversationDTO is mapped to the entity.
        /// Override this method to add custom validation or modify the DTO before mapping.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="conversationDTO">The DTO about to be mapped</param>
        protected virtual async Task OnBeforeConversationIsMapped(ConversationDTO conversationDTO) { }

        /// <summary>
        /// Lifecycle hook called before updating an existing Conversation entity.
        /// Override this method to add custom business logic during updates.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="conversation">The existing entity being updated</param>
        /// <param name="conversationDTO">The DTO containing new data</param>
        protected virtual async Task OnBeforeConversationUpdate(Conversation conversation, ConversationDTO conversationDTO) { }

        /// <summary>
        /// Lifecycle hook called before inserting a new Conversation entity.
        /// Override this method to add custom business logic during inserts.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="conversation">The new entity being inserted</param>
        /// <param name="conversationDTO">The DTO containing the data</param>
        protected virtual async Task OnBeforeConversationInsert(Conversation conversation, ConversationDTO conversationDTO) { }





        #endregion

        #region Delete

        /// <summary>
        /// Per-id variant of the pre-delete hook. By default forwards to
        /// <see cref="OnBeforeConversationListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity being deleted</param>
        public virtual Task OnBeforeConversationDelete(long id) =>
            OnBeforeConversationListDelete(id.StructToList());

        /// <summary>
        /// Deletes a single Conversation entity with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="id">The ID of the entity to delete</param>
        public async virtual Task DeleteConversation(long id)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeConversationDelete(id);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();

                List<long> listForDelete_1 = id.StructToList();



                await DeleteEntityAsync<Conversation, long>(id);
            });
        }

        /// <summary>
        /// Lifecycle hook called before deleting a list of Conversation entities.
        /// Override this to add custom validation or business logic before batch deletion.
        /// </summary>
        /// <param name="listForDelete">The list of entity IDs being deleted</param>
        public virtual async Task OnBeforeConversationListDelete(List<long> listForDelete) { }

        /// <summary>
        /// Deletes multiple Conversation entities with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="listForDelete_1">The list of entity IDs to delete</param>
        public async virtual Task DeleteConversationList(List<long> listForDelete_1)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeConversationListDelete(listForDelete_1);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();



                await DeleteEntitiesAsync<Conversation, long>(listForDelete_1);
            });
        }

        #endregion

        #region One To Many



        #endregion

    }
}