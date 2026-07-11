//HintName: WarehouseService.generated.cs
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
    /// Generated service for the Warehouse entity. Override lifecycle hooks
    /// by creating a <c>WarehouseService</c> class that inherits from this class.
    /// </summary>
    public class WarehouseServiceGenerated : ServiceBase
    {
        protected readonly EntityServiceDependencies _deps;


        public WarehouseServiceGenerated(EntityServiceDependencies deps) : base(deps.Context, deps.Localizer)
        {
            _deps = deps;

        }

        #region Read

        /// <summary>
        /// Retrieves the complete MainUIFormDTO for Warehouse, including the entity DTO and all related collections (one-to-many, many-to-many).
        /// </summary>
        /// <param name="id">The ID of the Warehouse entity</param>
        /// <returns>WarehouseMainUIFormDTO containing the entity DTO and related data</returns>
        public async virtual Task<WarehouseMainUIFormDTO> GetWarehouseMainUIFormDTO(byte id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = new WarehouseMainUIFormDTO
                {
                    WarehouseDTO = await GetWarehouseDTO(id),
                };

                await OnAfterGetWarehouseMainUIFormDTO(result);

                return result;
            });
        }

        /// <summary>
        /// Lifecycle hook called after retrieving Warehouse MainUIFormDTO.
        /// Override this method to enrich the MainUIFormDTO with additional data (e.g., computed fields, extra queries).
        /// This method runs inside a database transaction.
        /// </summary>
        /// <example>
        /// protected override async Task OnAfterGetWarehouseMainUIFormDTO(WarehouseMainUIFormDTO mainUIFormDTO)
        /// {
        ///     mainUIFormDTO.CustomProperty = await _deps.Context.DbSet&lt;OtherEntity&gt;().Where(x => x.WarehouseId == mainUIFormDTO.WarehouseDTO.Id).CountAsync();
        /// }
        /// </example>
        /// <param name="mainUIFormDTO">The MainUIFormDTO that was just constructed with entity and related data</param>
        protected virtual async Task OnAfterGetWarehouseMainUIFormDTO(WarehouseMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Retrieves a single Warehouse entity as a DTO with blob data populated.
        /// </summary>
        /// <param name="id">The ID of the Warehouse entity</param>
        /// <returns>WarehouseDTO with all blob properties populated</returns>
        public async virtual Task<WarehouseDTO> GetWarehouseDTO(byte id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dto = await _deps.Context.DbSet<Warehouse>()
                    .AsNoTracking()
                    .Where(x => x.Id == id).ProjectToType<WarehouseDTO>(Mapper.WarehouseProjectToConfig())
                    .SingleOrDefaultAsync();

                if (dto == null)
                    throw new BusinessException(_deps.Localizer["EntityDoesNotExistInDatabase"]);



                return dto;
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Warehouse entities.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<Warehouse>> GetPaginatedWarehouseResult(FilterDTO filterDTO, IQueryable<Warehouse> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Warehouse DTOs with blob data populated.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResultDTO containing WarehouseDTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<WarehouseDTO>> GetPaginatedWarehouseList(FilterDTO filterDTO, IQueryable<Warehouse> query)
        {
            PaginatedResult<Warehouse> paginationResult = new();
            List<WarehouseDTO> dtoList = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginatedWarehouseResult(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<WarehouseDTO>(Mapper.WarehouseProjectToConfig())
                    .ToListAsync();


            });

            return new PaginatedResultDTO<WarehouseDTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of Warehouse entities to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> ExportWarehouseListToExcel(FilterDTO filterDTO, IQueryable<Warehouse> query, CancellationToken cancellationToken = default)
        {
            IQueryable<WarehouseDTO> exportQuery = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                PaginatedResult<Warehouse> paginationResult = await GetPaginatedWarehouseResult(filterDTO, query);
                int maxRows = _deps.ExcelSettings.ExcelExportMaxRows;
                exportQuery = paginationResult.Query
                    .OrderBy(x => x.Id)
                    .Take(maxRows)
                    .ProjectToType<WarehouseDTO>(Mapper.WarehouseExcelProjectToConfig());
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new WarehouseDTO());
            return await _deps.ExcelService.FillReportTemplateAsync(
                exportQuery.AsAsyncEnumerable(),
                excelPropertiesToExclude,
                _deps.Localizer,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves a list of Warehouse entities without pagination.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of Warehouse entities</returns>
        public async virtual Task<List<Warehouse>> GetWarehouseList(IQueryable<Warehouse> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = await query
                    .ToListAsync();

                return result;
            });
        }

        /// <summary>
        /// Retrieves a list of Warehouse DTOs without pagination, with blob data populated.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of WarehouseDTO with blob properties populated</returns>
        public async virtual Task<List<WarehouseDTO>> GetWarehouseDTOList(IQueryable<Warehouse> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dtoList = await query
                    .AsNoTracking()
                    .ProjectToType<WarehouseDTO>(Mapper.WarehouseToDTOConfig())
                    .ToListAsync();



                return dtoList;
            });
        }



        #endregion

        #region Save

        /// <summary>
        /// Saves a Warehouse entity and returns the complete MainUIFormDTO including all related collections.
        /// Handles insert/update logic, many-to-many relationships, and ordered one-to-many collections.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity data and related selections</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>WarehouseMainUIFormDTO with saved data and updated collections</returns>
        public virtual async Task<WarehouseMainUIFormDTO> SaveWarehouseAndReturnMainUIFormDTO(WarehouseSaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            new WarehouseSaveBodyDTOValidationRules().ValidateAndThrow(saveBodyDTO);

            return await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeSaveWarehouseAndReturnMainUIFormDTO(saveBodyDTO);

                var savedDTO = await SaveWarehouseAndReturnDTO(saveBodyDTO.WarehouseDTO, authorizeUpdate, authorizeInsert);






                var result = new WarehouseMainUIFormDTO
                {
                    WarehouseDTO = savedDTO,



                };

                await OnAfterSaveWarehouseAndReturnMainUIFormDTO(saveBodyDTO, result);

                return result;
            });
        }




        /// <summary>
        /// Lifecycle hook called before saving Warehouse with MainUIFormDTO.
        /// Override this method to add custom validation or modify the SaveBodyDTO.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity and related data</param>
        protected virtual async Task OnBeforeSaveWarehouseAndReturnMainUIFormDTO(WarehouseSaveBodyDTO saveBodyDTO) { }

        /// <summary>
        /// Lifecycle hook called after saving Warehouse and after updating related collections.
        /// Override this method to add custom business logic after the main entity is saved.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The original SaveBodyDTO</param>
        /// <param name="mainUIFormDTO">The save result and DTO sent to the UI</param>
        protected virtual async Task OnAfterSaveWarehouseAndReturnMainUIFormDTO(WarehouseSaveBodyDTO saveBodyDTO, WarehouseMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Saves a Warehouse entity and returns the DTO with blob data populated.
        /// </summary>
        /// <param name="saveDTO">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved WarehouseDTO with blob properties populated</returns>
        public async virtual Task<WarehouseDTO> SaveWarehouseAndReturnDTO(WarehouseDTO saveDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var poco = await SaveWarehouse(saveDTO, authorizeUpdate, authorizeInsert);

                var dto = poco.Adapt<WarehouseDTO>(Mapper.WarehouseToDTOConfig());



                return dto;
            });
        }

        /// <summary>
        /// Core save method that handles both insert and update operations for Warehouse.
        /// Validates the DTO, maps to entity, handles many-to-one relationships, and manages blob deletion.
        /// </summary>
        /// <param name="dto">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved Warehouse entity</returns>
        public async virtual Task<Warehouse> SaveWarehouse(WarehouseDTO dto, bool authorizeUpdate, bool authorizeInsert)
        {
            WarehouseDTOValidationRules validationRules = new WarehouseDTOValidationRules();
            validationRules.ValidateAndThrow(dto);

            Warehouse poco = null;
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeWarehouseIsMapped(dto);
                DbSet<Warehouse> dbSet = _deps.Context.DbSet<Warehouse>();

                if (dto.Id > 0)
                {
                    if (authorizeUpdate)
                    {
                        await _deps.AuthorizationService.AuthorizeWarehouseUpdateAndThrow(dto);
                    }

                    poco = await GetInstanceAsync<Warehouse, byte>(dto.Id, dto.Version);
                    await OnBeforeWarehouseUpdate(poco, dto);
                    dto.Adapt(poco, Mapper.WarehouseDTOToEntityConfig());
                    dbSet.Update(poco);
                }
                else
                {
                    if (authorizeInsert)
                    {
                        await _deps.AuthorizationService.AuthorizeWarehouseInsertAndThrow(dto);
                    }

                    poco = dto.Adapt<Warehouse>(Mapper.WarehouseDTOToEntityConfig());
                    await OnBeforeWarehouseInsert(poco, dto);
                    await dbSet.AddAsync(poco);
                }



                await _deps.Context.SaveChangesAsync();






            });

            return poco;
        }

        /// <summary>
        /// Lifecycle hook called before the WarehouseDTO is mapped to the entity.
        /// Override this method to add custom validation or modify the DTO before mapping.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="warehouseDTO">The DTO about to be mapped</param>
        protected virtual async Task OnBeforeWarehouseIsMapped(WarehouseDTO warehouseDTO) { }

        /// <summary>
        /// Lifecycle hook called before updating an existing Warehouse entity.
        /// Override this method to add custom business logic during updates.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="warehouse">The existing entity being updated</param>
        /// <param name="warehouseDTO">The DTO containing new data</param>
        protected virtual async Task OnBeforeWarehouseUpdate(Warehouse warehouse, WarehouseDTO warehouseDTO) { }

        /// <summary>
        /// Lifecycle hook called before inserting a new Warehouse entity.
        /// Override this method to add custom business logic during inserts.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="warehouse">The new entity being inserted</param>
        /// <param name="warehouseDTO">The DTO containing the data</param>
        protected virtual async Task OnBeforeWarehouseInsert(Warehouse warehouse, WarehouseDTO warehouseDTO) { }





        #endregion

        #region Delete

        /// <summary>
        /// Per-id variant of the pre-delete hook. By default forwards to
        /// <see cref="OnBeforeWarehouseListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity being deleted</param>
        public virtual Task OnBeforeWarehouseDelete(byte id) =>
            OnBeforeWarehouseListDelete(id.StructToList());

        /// <summary>
        /// Deletes a single Warehouse entity with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="id">The ID of the entity to delete</param>
        public async virtual Task DeleteWarehouse(byte id)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeWarehouseDelete(id);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();

                List<byte> listForDelete_1 = id.StructToList();



                await DeleteEntityAsync<Warehouse, byte>(id);
            });
        }

        /// <summary>
        /// Lifecycle hook called before deleting a list of Warehouse entities.
        /// Override this to add custom validation or business logic before batch deletion.
        /// </summary>
        /// <param name="listForDelete">The list of entity IDs being deleted</param>
        public virtual async Task OnBeforeWarehouseListDelete(List<byte> listForDelete) { }

        /// <summary>
        /// Deletes multiple Warehouse entities with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="listForDelete_1">The list of entity IDs to delete</param>
        public async virtual Task DeleteWarehouseList(List<byte> listForDelete_1)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeWarehouseListDelete(listForDelete_1);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();



                await DeleteEntitiesAsync<Warehouse, byte>(listForDelete_1);
            });
        }

        #endregion

        #region One To Many



        #endregion

    }
}