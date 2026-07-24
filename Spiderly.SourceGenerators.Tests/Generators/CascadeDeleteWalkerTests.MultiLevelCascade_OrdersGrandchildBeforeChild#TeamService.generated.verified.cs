//HintName: TeamService.generated.cs
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
    /// Generated service for the Team entity. Override lifecycle hooks
    /// by creating a <c>TeamService</c> class that inherits from this class.
    /// </summary>
    public class TeamServiceGenerated : ServiceBase
    {
        protected readonly EntityServiceDependencies _deps;


        public TeamServiceGenerated(EntityServiceDependencies deps) : base(deps.Context, deps.Localizer)
        {
            _deps = deps;

        }

        #region Read

        /// <summary>
        /// Retrieves the complete MainUIFormDTO for Team, including the entity DTO and all related collections (one-to-many, many-to-many).
        /// </summary>
        /// <param name="id">The ID of the Team entity</param>
        /// <returns>TeamMainUIFormDTO containing the entity DTO and related data</returns>
        public async virtual Task<TeamMainUIFormDTO> GetTeamMainUIFormDTO(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = new TeamMainUIFormDTO
                {
                    TeamDTO = await GetTeamDTO(id),
                };

                await OnAfterGetTeamMainUIFormDTO(result);

                return result;
            });
        }

        /// <summary>
        /// Lifecycle hook called after retrieving Team MainUIFormDTO.
        /// Override this method to enrich the MainUIFormDTO with additional data (e.g., computed fields, extra queries).
        /// This method runs inside a database transaction.
        /// </summary>
        /// <example>
        /// protected override async Task OnAfterGetTeamMainUIFormDTO(TeamMainUIFormDTO mainUIFormDTO)
        /// {
        ///     mainUIFormDTO.CustomProperty = await _deps.Context.DbSet&lt;OtherEntity&gt;().Where(x => x.TeamId == mainUIFormDTO.TeamDTO.Id).CountAsync();
        /// }
        /// </example>
        /// <param name="mainUIFormDTO">The MainUIFormDTO that was just constructed with entity and related data</param>
        protected virtual async Task OnAfterGetTeamMainUIFormDTO(TeamMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Retrieves a single Team entity as a DTO with blob data populated.
        /// </summary>
        /// <param name="id">The ID of the Team entity</param>
        /// <returns>TeamDTO with all blob properties populated</returns>
        public async virtual Task<TeamDTO> GetTeamDTO(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dto = await _deps.Context.DbSet<Team>()
                    .AsNoTracking()
                    .Where(x => x.Id == id).ProjectToType<TeamDTO>(Mapper.TeamProjectToConfig())
                    .SingleOrDefaultAsync();

                if (dto == null)
                    throw new BusinessException(_deps.Localizer["EntityDoesNotExistInDatabase"]);



                return dto;
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Team entities.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<Team>> GetPaginatedTeamResult(FilterDTO filterDTO, IQueryable<Team> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Team DTOs with blob data populated.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResultDTO containing TeamDTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<TeamDTO>> GetPaginatedTeamList(FilterDTO filterDTO, IQueryable<Team> query)
        {
            PaginatedResult<Team> paginationResult = new();
            List<TeamDTO> dtoList = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginatedTeamResult(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<TeamDTO>(Mapper.TeamProjectToConfig())
                    .ToListAsync();


            });

            return new PaginatedResultDTO<TeamDTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of Team entities to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> ExportTeamListToExcel(FilterDTO filterDTO, IQueryable<Team> query, CancellationToken cancellationToken = default)
        {
            IQueryable<TeamDTO> exportQuery = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                PaginatedResult<Team> paginationResult = await GetPaginatedTeamResult(filterDTO, query);
                int maxRows = _deps.ExcelSettings.ExcelExportMaxRows;
                exportQuery = paginationResult.Query
                    .OrderBy(x => x.Id)
                    .Take(maxRows)
                    .ProjectToType<TeamDTO>(Mapper.TeamExcelProjectToConfig());
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new TeamDTO());
            return await _deps.ExcelService.FillReportTemplateAsync(
                exportQuery.AsAsyncEnumerable(),
                excelPropertiesToExclude,
                _deps.Localizer,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves a list of Team entities without pagination.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of Team entities</returns>
        public async virtual Task<List<Team>> GetTeamList(IQueryable<Team> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = await query
                    .ToListAsync();

                return result;
            });
        }

        /// <summary>
        /// Retrieves a list of Team DTOs without pagination, with blob data populated.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of TeamDTO with blob properties populated</returns>
        public async virtual Task<List<TeamDTO>> GetTeamDTOList(IQueryable<Team> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dtoList = await query
                    .AsNoTracking()
                    .ProjectToType<TeamDTO>(Mapper.TeamToDTOConfig())
                    .ToListAsync();



                return dtoList;
            });
        }

        /// <summary>
        /// Retrieves autocomplete suggestions for the Org many-to-one relationship in Team.
        /// </summary>
        /// <param name="limit">Maximum number of results to return</param>
        /// <param name="filter">Text filter for Name</param>
        /// <param name="query">Base query for Org entities</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
        public async virtual Task<List<NamebookDTO<long>>> GetOrgAutocompleteListForTeam(
            int limit,
            string filter,
            IQueryable<Org> query
        )
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                if (!string.IsNullOrEmpty(filter))
                    query = query.Where(x => x.Name.ToLower().Contains(filter.ToLower()));

                var result = await query
                    .AsNoTracking()
                    .Take(limit)
                    .Select(x => new NamebookDTO<long>
                    {
                        Id = x.Id,
                        DisplayName = x.Name,
                    })
                    .ToListAsync();

                return result;
            });
        }


        #endregion

        #region Save

        /// <summary>
        /// Saves a Team entity and returns the complete MainUIFormDTO including all related collections.
        /// Handles insert/update logic, many-to-many relationships, and ordered one-to-many collections.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity data and related selections</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>TeamMainUIFormDTO with saved data and updated collections</returns>
        public virtual async Task<TeamMainUIFormDTO> SaveTeamAndReturnMainUIFormDTO(TeamSaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            new TeamSaveBodyDTOValidationRules().ValidateAndThrow(saveBodyDTO);

            return await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeSaveTeamAndReturnMainUIFormDTO(saveBodyDTO);

                var savedDTO = await SaveTeamAndReturnDTO(saveBodyDTO.TeamDTO, authorizeUpdate, authorizeInsert);






                var result = new TeamMainUIFormDTO
                {
                    TeamDTO = savedDTO,



                };

                await OnAfterSaveTeamAndReturnMainUIFormDTO(saveBodyDTO, result);

                return result;
            });
        }




        /// <summary>
        /// Lifecycle hook called before saving Team with MainUIFormDTO.
        /// Override this method to add custom validation or modify the SaveBodyDTO.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity and related data</param>
        protected virtual async Task OnBeforeSaveTeamAndReturnMainUIFormDTO(TeamSaveBodyDTO saveBodyDTO) { }

        /// <summary>
        /// Lifecycle hook called after saving Team and after updating related collections.
        /// Override this method to add custom business logic after the main entity is saved.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The original SaveBodyDTO</param>
        /// <param name="mainUIFormDTO">The save result and DTO sent to the UI</param>
        protected virtual async Task OnAfterSaveTeamAndReturnMainUIFormDTO(TeamSaveBodyDTO saveBodyDTO, TeamMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Saves a Team entity and returns the DTO with blob data populated.
        /// </summary>
        /// <param name="saveDTO">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved TeamDTO with blob properties populated</returns>
        public async virtual Task<TeamDTO> SaveTeamAndReturnDTO(TeamDTO saveDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var poco = await SaveTeam(saveDTO, authorizeUpdate, authorizeInsert);

                var dto = poco.Adapt<TeamDTO>(Mapper.TeamToDTOConfig());



                return dto;
            });
        }

        /// <summary>
        /// Core save method that handles both insert and update operations for Team.
        /// Validates the DTO, maps to entity, handles many-to-one relationships, and manages blob deletion.
        /// </summary>
        /// <param name="dto">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved Team entity</returns>
        public async virtual Task<Team> SaveTeam(TeamDTO dto, bool authorizeUpdate, bool authorizeInsert)
        {
            TeamDTOValidationRules validationRules = new TeamDTOValidationRules();
            validationRules.ValidateAndThrow(dto);

            Team poco = null;
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeTeamIsMapped(dto);
                DbSet<Team> dbSet = _deps.Context.DbSet<Team>();

                if (dto.Id > 0)
                {
                    if (authorizeUpdate)
                    {
                        await _deps.AuthorizationService.AuthorizeTeamUpdateAndThrow(dto);
                    }

                    poco = await GetInstanceAsync<Team, long>(dto.Id, dto.Version);
                    await OnBeforeTeamUpdate(poco, dto);
                    dto.Adapt(poco, Mapper.TeamDTOToEntityConfig());
                    dbSet.Update(poco);
                }
                else
                {
                    if (authorizeInsert)
                    {
                        await _deps.AuthorizationService.AuthorizeTeamInsertAndThrow(dto);
                    }

                    poco = dto.Adapt<Team>(Mapper.TeamDTOToEntityConfig());
                    await OnBeforeTeamInsert(poco, dto);
                    await dbSet.AddAsync(poco);
                }

                if (dto.OrgId > 0)
                {
                    poco.Org = await GetInstanceAsync<Org, long>(dto.OrgId.Value, null);
                }
                else
                {
                    var _ = poco.Org; // HACK
                    poco.Org = null;
                }

                await _deps.Context.SaveChangesAsync();






            });

            return poco;
        }

        /// <summary>
        /// Lifecycle hook called before the TeamDTO is mapped to the entity.
        /// Override this method to add custom validation or modify the DTO before mapping.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="teamDTO">The DTO about to be mapped</param>
        protected virtual async Task OnBeforeTeamIsMapped(TeamDTO teamDTO) { }

        /// <summary>
        /// Lifecycle hook called before updating an existing Team entity.
        /// Override this method to add custom business logic during updates.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="team">The existing entity being updated</param>
        /// <param name="teamDTO">The DTO containing new data</param>
        protected virtual async Task OnBeforeTeamUpdate(Team team, TeamDTO teamDTO) { }

        /// <summary>
        /// Lifecycle hook called before inserting a new Team entity.
        /// Override this method to add custom business logic during inserts.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="team">The new entity being inserted</param>
        /// <param name="teamDTO">The DTO containing the data</param>
        protected virtual async Task OnBeforeTeamInsert(Team team, TeamDTO teamDTO) { }





        #endregion

        #region Delete

        /// <summary>
        /// Per-id variant of the pre-delete hook. By default forwards to
        /// <see cref="OnBeforeTeamListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity being deleted</param>
        public virtual Task OnBeforeTeamDelete(long id) =>
            OnBeforeTeamListDelete(id.StructToList());

        /// <summary>
        /// Per-id variant of the post-delete hook. By default forwards to
        /// <see cref="OnAfterTeamListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity that was deleted</param>
        public virtual Task OnAfterTeamDelete(long id) =>
            OnAfterTeamListDelete(id.StructToList());

        /// <summary>
        /// Deletes a single Team entity with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="id">The ID of the entity to delete</param>
        public async virtual Task DeleteTeam(long id)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeTeamDelete(id);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();

                List<long> listForDelete_1 = id.StructToList();

                var memberListForDeleteBecauseTeam_2 = await _deps.Context.DbSet<Member>()
                    .AsNoTracking()
                    .Where(x => listForDelete_1.Contains(EF.Property<long>(x, "TeamId")))
                    .Select(x => x.Id)
                    .ToListAsync();

                await _deps.Context.DbSet<Member>()
                    .Where(x => memberListForDeleteBecauseTeam_2.Contains(x.Id))
                    .ExecuteDeleteAsync();

                await DeleteEntityAsync<Team, long>(id);

                await OnAfterTeamDelete(id);

                // Persist writes the hook staged as part of this transaction; commit below
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();
            });
        }

        /// <summary>
        /// Lifecycle hook called before deleting a list of Team entities.
        /// Override this to add custom validation or business logic before batch deletion.
        /// </summary>
        /// <param name="listForDelete">The list of entity IDs being deleted</param>
        public virtual async Task OnBeforeTeamListDelete(List<long> listForDelete) { }

        /// <summary>
        /// Lifecycle hook called after deleting a list of Team entities (cascades included),
        /// still inside the delete transaction — queries observe the post-delete state, and anything
        /// written here commits or rolls back atomically with the delete.
        /// Override this to recompute denormalized aggregates or stage post-delete work.
        /// </summary>
        /// <param name="deletedIds">The list of entity IDs that were deleted</param>
        /// <example>
        /// public override async Task OnAfterTeamListDelete(List&lt;long&gt; deletedIds)
        /// {
        ///     await RecalculateAggregatesAsync(); // reads the post-delete state
        /// }
        /// </example>
        public virtual async Task OnAfterTeamListDelete(List<long> deletedIds) { }

        /// <summary>
        /// Deletes multiple Team entities with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="listForDelete_1">The list of entity IDs to delete</param>
        public async virtual Task DeleteTeamList(List<long> listForDelete_1)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeTeamListDelete(listForDelete_1);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();

                var memberListForDeleteBecauseTeam_2 = await _deps.Context.DbSet<Member>()
                    .AsNoTracking()
                    .Where(x => listForDelete_1.Contains(EF.Property<long>(x, "TeamId")))
                    .Select(x => x.Id)
                    .ToListAsync();

                await _deps.Context.DbSet<Member>()
                    .Where(x => memberListForDeleteBecauseTeam_2.Contains(x.Id))
                    .ExecuteDeleteAsync();

                await DeleteEntitiesAsync<Team, long>(listForDelete_1);

                await OnAfterTeamListDelete(listForDelete_1);

                // Persist writes the hook staged as part of this transaction; commit below
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();
            });
        }

        #endregion

        #region One To Many

        /// <summary>
        /// Retrieves namebook DTOs for Member entities related to a Team.
        /// </summary>
        /// <param name="id">The ID of the Team entity</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
        public async virtual Task<List<NamebookDTO<long>>> GetMembersNamebookListForTeam(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await _deps.Context.DbSet<Member>()
                    .AsNoTracking()
                    .Where(x => EF.Property<long>(x, "TeamId") == id)
                    .Select(x => new NamebookDTO<long>
                    {
                        Id = x.Id,
                        DisplayName = x.Name,
                    })
                    .ToListAsync();
            });
        }

        /// <summary>
        /// Retrieves all Member entities related to a Team via the Members one-to-many relationship.
        /// </summary>
        /// <param name="id">The ID of the Team entity</param>
        /// <returns>List of Member entities</returns>
        public async virtual Task<List<Member>> GetMembersForTeam(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await _deps.Context.DbSet<Member>()
                    .Where(x => EF.Property<long>(x, "TeamId") == id)
                    .ToListAsync();
            });
        }



        #endregion

    }
}