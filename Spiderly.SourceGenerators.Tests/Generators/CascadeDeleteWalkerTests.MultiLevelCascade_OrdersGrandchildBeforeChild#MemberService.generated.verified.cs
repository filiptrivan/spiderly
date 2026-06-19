//HintName: MemberService.generated.cs
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
    /// Generated service for the Member entity. Override lifecycle hooks
    /// by creating a <c>MemberService</c> class that inherits from this class.
    /// </summary>
    public class MemberServiceGenerated : ServiceBase
    {
        protected readonly EntityServiceDependencies _deps;


        public MemberServiceGenerated(EntityServiceDependencies deps) : base(deps.Context, deps.Localizer)
        {
            _deps = deps;

        }

        #region Read

        /// <summary>
        /// Retrieves the complete MainUIFormDTO for Member, including the entity DTO and all related collections (one-to-many, many-to-many).
        /// </summary>
        /// <param name="id">The ID of the Member entity</param>
        /// <returns>MemberMainUIFormDTO containing the entity DTO and related data</returns>
        public async virtual Task<MemberMainUIFormDTO> GetMemberMainUIFormDTO(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = new MemberMainUIFormDTO
                {
                    MemberDTO = await GetMemberDTO(id),
                };

                await OnAfterGetMemberMainUIFormDTO(result);

                return result;
            });
        }

        /// <summary>
        /// Lifecycle hook called after retrieving Member MainUIFormDTO.
        /// Override this method to enrich the MainUIFormDTO with additional data (e.g., computed fields, extra queries).
        /// This method runs inside a database transaction.
        /// </summary>
        /// <example>
        /// protected override async Task OnAfterGetMemberMainUIFormDTO(MemberMainUIFormDTO mainUIFormDTO)
        /// {
        ///     mainUIFormDTO.CustomProperty = await _deps.Context.DbSet&lt;OtherEntity&gt;().Where(x => x.MemberId == mainUIFormDTO.MemberDTO.Id).CountAsync();
        /// }
        /// </example>
        /// <param name="mainUIFormDTO">The MainUIFormDTO that was just constructed with entity and related data</param>
        protected virtual async Task OnAfterGetMemberMainUIFormDTO(MemberMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Retrieves a single Member entity as a DTO with blob data populated.
        /// </summary>
        /// <param name="id">The ID of the Member entity</param>
        /// <returns>MemberDTO with all blob properties populated</returns>
        public async virtual Task<MemberDTO> GetMemberDTO(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dto = await _deps.Context.DbSet<Member>()
                    .AsNoTracking()
                    .Where(x => x.Id == id).ProjectToType<MemberDTO>(Mapper.MemberProjectToConfig())
                    .SingleOrDefaultAsync();

                if (dto == null)
                    throw new BusinessException(_deps.Localizer["EntityDoesNotExistInDatabase"]);



                return dto;
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Member entities.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<Member>> GetPaginatedMemberResult(FilterDTO filterDTO, IQueryable<Member> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Member DTOs with blob data populated.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResultDTO containing MemberDTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<MemberDTO>> GetPaginatedMemberList(FilterDTO filterDTO, IQueryable<Member> query)
        {
            PaginatedResult<Member> paginationResult = new();
            List<MemberDTO> dtoList = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginatedMemberResult(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<MemberDTO>(Mapper.MemberProjectToConfig())
                    .ToListAsync();


            });

            return new PaginatedResultDTO<MemberDTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of Member entities to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> ExportMemberListToExcel(FilterDTO filterDTO, IQueryable<Member> query, CancellationToken cancellationToken = default)
        {
            IQueryable<MemberDTO> exportQuery = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                PaginatedResult<Member> paginationResult = await GetPaginatedMemberResult(filterDTO, query);
                int maxRows = _deps.ExcelSettings.ExcelExportMaxRows;
                exportQuery = paginationResult.Query
                    .OrderBy(x => x.Id)
                    .Take(maxRows)
                    .ProjectToType<MemberDTO>(Mapper.MemberExcelProjectToConfig());
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new MemberDTO());
            return await _deps.ExcelService.FillReportTemplateAsync(
                exportQuery.AsAsyncEnumerable(),
                excelPropertiesToExclude,
                _deps.Localizer,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves a list of Member entities without pagination.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of Member entities</returns>
        public async virtual Task<List<Member>> GetMemberList(IQueryable<Member> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = await query
                    .ToListAsync();

                return result;
            });
        }

        /// <summary>
        /// Retrieves a list of Member DTOs without pagination, with blob data populated.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of MemberDTO with blob properties populated</returns>
        public async virtual Task<List<MemberDTO>> GetMemberDTOList(IQueryable<Member> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dtoList = await query
                    .AsNoTracking()
                    .ProjectToType<MemberDTO>(Mapper.MemberToDTOConfig())
                    .ToListAsync();



                return dtoList;
            });
        }

        /// <summary>
        /// Retrieves autocomplete suggestions for the Team many-to-one relationship in Member.
        /// </summary>
        /// <param name="limit">Maximum number of results to return</param>
        /// <param name="filter">Text filter for Name</param>
        /// <param name="query">Base query for Team entities</param>
        /// <returns>List of NamebookDTO containing ID and DisplayName</returns>
        public async virtual Task<List<NamebookDTO<long>>> GetTeamAutocompleteListForMember(
            int limit,
            string filter,
            IQueryable<Team> query
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
        /// Saves a Member entity and returns the complete MainUIFormDTO including all related collections.
        /// Handles insert/update logic, many-to-many relationships, and ordered one-to-many collections.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity data and related selections</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>MemberMainUIFormDTO with saved data and updated collections</returns>
        public virtual async Task<MemberMainUIFormDTO> SaveMemberAndReturnMainUIFormDTO(MemberSaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            new MemberSaveBodyDTOValidationRules().ValidateAndThrow(saveBodyDTO);

            return await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeSaveMemberAndReturnMainUIFormDTO(saveBodyDTO);

                var savedDTO = await SaveMemberAndReturnDTO(saveBodyDTO.MemberDTO, authorizeUpdate, authorizeInsert);






                var result = new MemberMainUIFormDTO
                {
                    MemberDTO = savedDTO,



                };

                await OnAfterSaveMemberAndReturnMainUIFormDTO(saveBodyDTO, result);

                return result;
            });
        }




        /// <summary>
        /// Lifecycle hook called before saving Member with MainUIFormDTO.
        /// Override this method to add custom validation or modify the SaveBodyDTO.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity and related data</param>
        protected virtual async Task OnBeforeSaveMemberAndReturnMainUIFormDTO(MemberSaveBodyDTO saveBodyDTO) { }

        /// <summary>
        /// Lifecycle hook called after saving Member and after updating related collections.
        /// Override this method to add custom business logic after the main entity is saved.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The original SaveBodyDTO</param>
        /// <param name="mainUIFormDTO">The save result and DTO sent to the UI</param>
        protected virtual async Task OnAfterSaveMemberAndReturnMainUIFormDTO(MemberSaveBodyDTO saveBodyDTO, MemberMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Saves a Member entity and returns the DTO with blob data populated.
        /// </summary>
        /// <param name="saveDTO">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved MemberDTO with blob properties populated</returns>
        public async virtual Task<MemberDTO> SaveMemberAndReturnDTO(MemberDTO saveDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var poco = await SaveMember(saveDTO, authorizeUpdate, authorizeInsert);

                var dto = poco.Adapt<MemberDTO>(Mapper.MemberToDTOConfig());



                return dto;
            });
        }

        /// <summary>
        /// Core save method that handles both insert and update operations for Member.
        /// Validates the DTO, maps to entity, handles many-to-one relationships, and manages blob deletion.
        /// </summary>
        /// <param name="dto">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved Member entity</returns>
        public async virtual Task<Member> SaveMember(MemberDTO dto, bool authorizeUpdate, bool authorizeInsert)
        {
            MemberDTOValidationRules validationRules = new MemberDTOValidationRules();
            validationRules.ValidateAndThrow(dto);

            Member poco = null;
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeMemberIsMapped(dto);
                DbSet<Member> dbSet = _deps.Context.DbSet<Member>();

                if (dto.Id > 0)
                {
                    if (authorizeUpdate)
                    {
                        await _deps.AuthorizationService.AuthorizeMemberUpdateAndThrow(dto);
                    }

                    poco = await GetInstanceAsync<Member, long>(dto.Id, dto.Version);
                    await OnBeforeMemberUpdate(poco, dto);
                    dto.Adapt(poco, Mapper.MemberDTOToEntityConfig());
                    dbSet.Update(poco);
                }
                else
                {
                    if (authorizeInsert)
                    {
                        await _deps.AuthorizationService.AuthorizeMemberInsertAndThrow(dto);
                    }

                    poco = dto.Adapt<Member>(Mapper.MemberDTOToEntityConfig());
                    await OnBeforeMemberInsert(poco, dto);
                    await dbSet.AddAsync(poco);
                }

                if (dto.TeamId > 0)
                {
                    poco.Team = await GetInstanceAsync<Team, long>(dto.TeamId.Value, null);
                }
                else
                {
                    var _ = poco.Team; // HACK
                    poco.Team = null;
                }

                await _deps.Context.SaveChangesAsync();






            });

            return poco;
        }

        /// <summary>
        /// Lifecycle hook called before the MemberDTO is mapped to the entity.
        /// Override this method to add custom validation or modify the DTO before mapping.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="memberDTO">The DTO about to be mapped</param>
        protected virtual async Task OnBeforeMemberIsMapped(MemberDTO memberDTO) { }

        /// <summary>
        /// Lifecycle hook called before updating an existing Member entity.
        /// Override this method to add custom business logic during updates.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="member">The existing entity being updated</param>
        /// <param name="memberDTO">The DTO containing new data</param>
        protected virtual async Task OnBeforeMemberUpdate(Member member, MemberDTO memberDTO) { }

        /// <summary>
        /// Lifecycle hook called before inserting a new Member entity.
        /// Override this method to add custom business logic during inserts.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="member">The new entity being inserted</param>
        /// <param name="memberDTO">The DTO containing the data</param>
        protected virtual async Task OnBeforeMemberInsert(Member member, MemberDTO memberDTO) { }





        #endregion

        #region Delete

        /// <summary>
        /// Per-id variant of the pre-delete hook. By default forwards to
        /// <see cref="OnBeforeMemberListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity being deleted</param>
        public virtual Task OnBeforeMemberDelete(long id) =>
            OnBeforeMemberListDelete(id.StructToList());

        /// <summary>
        /// Deletes a single Member entity with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="id">The ID of the entity to delete</param>
        public async virtual Task DeleteMember(long id)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeMemberDelete(id);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();

                List<long> listForDelete_1 = id.StructToList();



                await DeleteEntityAsync<Member, long>(id);
            });
        }

        /// <summary>
        /// Lifecycle hook called before deleting a list of Member entities.
        /// Override this to add custom validation or business logic before batch deletion.
        /// </summary>
        /// <param name="listForDelete">The list of entity IDs being deleted</param>
        public virtual async Task OnBeforeMemberListDelete(List<long> listForDelete) { }

        /// <summary>
        /// Deletes multiple Member entities with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="listForDelete_1">The list of entity IDs to delete</param>
        public async virtual Task DeleteMemberList(List<long> listForDelete_1)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeMemberListDelete(listForDelete_1);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();



                await DeleteEntitiesAsync<Member, long>(listForDelete_1);
            });
        }

        #endregion

        #region One To Many



        #endregion

    }
}