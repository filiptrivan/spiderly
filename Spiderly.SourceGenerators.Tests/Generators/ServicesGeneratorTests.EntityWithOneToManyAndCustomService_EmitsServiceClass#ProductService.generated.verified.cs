//HintName: ProductService.generated.cs
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
    /// Generated service for the Product entity. Override lifecycle hooks
    /// by creating a <c>ProductService</c> class that inherits from this class.
    /// </summary>
    public class ProductServiceGenerated : ServiceBase
    {
        protected readonly EntityServiceDependencies _deps;


        public ProductServiceGenerated(EntityServiceDependencies deps) : base(deps.Context, deps.Localizer)
        {
            _deps = deps;

        }

        #region Read

        /// <summary>
        /// Retrieves the complete MainUIFormDTO for Product, including the entity DTO and all related collections (one-to-many, many-to-many).
        /// </summary>
        /// <param name="id">The ID of the Product entity</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>ProductMainUIFormDTO containing the entity DTO and related data</returns>
        public async virtual Task<ProductMainUIFormDTO> GetProductMainUIFormDTO(long id, bool authorize)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeProductReadAndThrow(id);
                }

                var result = new ProductMainUIFormDTO
                {
                    ProductDTO = await GetProductDTO(id, false),
                };

                await OnAfterGetProductMainUIFormDTO(result);

                return result;
            });
        }

        /// <summary>
        /// Lifecycle hook called after retrieving Product MainUIFormDTO.
        /// Override this method to enrich the MainUIFormDTO with additional data (e.g., computed fields, extra queries).
        /// This method runs inside a database transaction.
        /// </summary>
        /// <example>
        /// protected override async Task OnAfterGetProductMainUIFormDTO(ProductMainUIFormDTO mainUIFormDTO)
        /// {
        ///     mainUIFormDTO.CustomProperty = await _deps.Context.DbSet&lt;OtherEntity&gt;().Where(x => x.ProductId == mainUIFormDTO.ProductDTO.Id).CountAsync();
        /// }
        /// </example>
        /// <param name="mainUIFormDTO">The MainUIFormDTO that was just constructed with entity and related data</param>
        protected virtual async Task OnAfterGetProductMainUIFormDTO(ProductMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Retrieves a single Product entity as a DTO with blob data populated.
        /// </summary>
        /// <param name="id">The ID of the Product entity</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>ProductDTO with all blob properties populated</returns>
        public async virtual Task<ProductDTO> GetProductDTO(long id, bool authorize)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeProductReadAndThrow(id);
                }

                var dto = await _deps.Context.DbSet<Product>()
                    .AsNoTracking()
                    .Where(x => x.Id == id).ProjectToType<ProductDTO>(Mapper.ProductProjectToConfig())
                    .SingleOrDefaultAsync();

                if (dto == null)
                    throw new BusinessException(_deps.Localizer["EntityDoesNotExistInDatabase"]);



                return dto;
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Product entities.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<Product>> GetPaginatedProductList(FilterDTO filterDTO, IQueryable<Product> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Product DTOs with blob data populated.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>PaginatedResultDTO containing ProductDTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<ProductDTO>> GetPaginatedProductList(FilterDTO filterDTO, IQueryable<Product> query, bool authorize)
        {
            PaginatedResult<Product> paginationResult = new();
            List<ProductDTO> dtoList = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginatedProductList(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<ProductDTO>(Mapper.ProductProjectToConfig())
                    .ToListAsync();

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeProductReadAndThrow(dtoList.Select(x => x.Id).ToList());
                }


            });

            return new PaginatedResultDTO<ProductDTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of Product entities to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> ExportProductListToExcel(FilterDTO filterDTO, IQueryable<Product> query, bool authorize, CancellationToken cancellationToken = default)
        {
            IQueryable<ProductDTO> exportQuery = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                PaginatedResult<Product> paginationResult = await GetPaginatedProductList(filterDTO, query);
                int maxRows = Spiderly.Shared.SettingsProvider.Current.ExcelExportMaxRows;
                exportQuery = paginationResult.Query
                    .OrderBy(x => x.Id)
                    .Take(maxRows)
                    .ProjectToType<ProductDTO>(Mapper.ProductExcelProjectToConfig());
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new ProductDTO());
            return await _deps.ExcelService.FillReportTemplateAsync(
                exportQuery.AsAsyncEnumerable(),
                excelPropertiesToExclude,
                _deps.Localizer,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves a list of Product entities without pagination.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>List of Product entities</returns>
        public async virtual Task<List<Product>> GetProductList(IQueryable<Product> query, bool authorize)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = await query
                    .ToListAsync();

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeProductReadAndThrow(result.Select(x => x.Id).ToList());
                }

                return result;
            });
        }

        /// <summary>
        /// Retrieves a list of Product DTOs without pagination, with blob data populated.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <param name="authorize">Whether to perform authorization check for Read operation</param>
        /// <returns>List of ProductDTO with blob properties populated</returns>
        public async virtual Task<List<ProductDTO>> GetProductDTOList(IQueryable<Product> query, bool authorize)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dtoList = await query
                    .AsNoTracking()
                    .ProjectToType<ProductDTO>(Mapper.ProductToDTOConfig())
                    .ToListAsync();

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeProductReadAndThrow(dtoList.Select(x => x.Id).ToList());
                }



                return dtoList;
            });
        }



        #endregion

        #region Save

        /// <summary>
        /// Saves a Product entity and returns the complete MainUIFormDTO including all related collections.
        /// Handles insert/update logic, many-to-many relationships, and ordered one-to-many collections.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity data and related selections</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>ProductMainUIFormDTO with saved data and updated collections</returns>
        public virtual async Task<ProductMainUIFormDTO> SaveProductAndReturnMainUIFormDTO(ProductSaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            new ProductSaveBodyDTOValidationRules().ValidateAndThrow(saveBodyDTO);

            return await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeSaveProductAndReturnMainUIFormDTO(saveBodyDTO);

                var savedDTO = await SaveProductAndReturnDTO(saveBodyDTO.ProductDTO, authorizeUpdate, authorizeInsert);






                var result = new ProductMainUIFormDTO
                {
                    ProductDTO = savedDTO,



                };

                await OnAfterSaveProductAndReturnMainUIFormDTO(saveBodyDTO, result);

                return result;
            });
        }




        /// <summary>
        /// Lifecycle hook called before saving Product with MainUIFormDTO.
        /// Override this method to add custom validation or modify the SaveBodyDTO.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity and related data</param>
        protected virtual async Task OnBeforeSaveProductAndReturnMainUIFormDTO(ProductSaveBodyDTO saveBodyDTO) { }

        /// <summary>
        /// Lifecycle hook called after saving Product and after updating related collections.
        /// Override this method to add custom business logic after the main entity is saved.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The original SaveBodyDTO</param>
        /// <param name="mainUIFormDTO">The save result and DTO sent to the UI</param>
        protected virtual async Task OnAfterSaveProductAndReturnMainUIFormDTO(ProductSaveBodyDTO saveBodyDTO, ProductMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Saves a Product entity and returns the DTO with blob data populated.
        /// </summary>
        /// <param name="saveDTO">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved ProductDTO with blob properties populated</returns>
        public async virtual Task<ProductDTO> SaveProductAndReturnDTO(ProductDTO saveDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var poco = await SaveProduct(saveDTO, authorizeUpdate, authorizeInsert);

                var dto = poco.Adapt<ProductDTO>(Mapper.ProductToDTOConfig());



                return dto;
            });
        }

        /// <summary>
        /// Core save method that handles both insert and update operations for Product.
        /// Validates the DTO, maps to entity, handles many-to-one relationships, and manages blob deletion.
        /// </summary>
        /// <param name="dto">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved Product entity</returns>
        public async virtual Task<Product> SaveProduct(ProductDTO dto, bool authorizeUpdate, bool authorizeInsert)
        {
            ProductDTOValidationRules validationRules = new ProductDTOValidationRules();
            validationRules.ValidateAndThrow(dto);

            Product poco = null;
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeProductIsMapped(dto);
                DbSet<Product> dbSet = _deps.Context.DbSet<Product>();

                if (dto.Id > 0)
                {
                    if (authorizeUpdate)
                    {
                        await _deps.AuthorizationService.AuthorizeProductUpdateAndThrow(dto);
                    }

                    poco = await GetInstanceAsync<Product, long>(dto.Id, dto.Version);
                    await OnBeforeProductUpdate(poco, dto);
                    dto.Adapt(poco, Mapper.ProductDTOToEntityConfig());
                    dbSet.Update(poco);
                }
                else
                {
                    if (authorizeInsert)
                    {
                        await _deps.AuthorizationService.AuthorizeProductInsertAndThrow(dto);
                    }

                    poco = dto.Adapt<Product>(Mapper.ProductDTOToEntityConfig());
                    await OnBeforeProductInsert(poco, dto);
                    await dbSet.AddAsync(poco);
                }



                await _deps.Context.SaveChangesAsync();






            });

            return poco;
        }

        /// <summary>
        /// Lifecycle hook called before the ProductDTO is mapped to the entity.
        /// Override this method to add custom validation or modify the DTO before mapping.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="productDTO">The DTO about to be mapped</param>
        protected virtual async Task OnBeforeProductIsMapped(ProductDTO productDTO) { }

        /// <summary>
        /// Lifecycle hook called before updating an existing Product entity.
        /// Override this method to add custom business logic during updates.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="product">The existing entity being updated</param>
        /// <param name="productDTO">The DTO containing new data</param>
        protected virtual async Task OnBeforeProductUpdate(Product product, ProductDTO productDTO) { }

        /// <summary>
        /// Lifecycle hook called before inserting a new Product entity.
        /// Override this method to add custom business logic during inserts.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="product">The new entity being inserted</param>
        /// <param name="productDTO">The DTO containing the data</param>
        protected virtual async Task OnBeforeProductInsert(Product product, ProductDTO productDTO) { }





        #endregion

        #region Delete

        /// <summary>
        /// Per-id variant of the pre-delete hook. By default forwards to
        /// <see cref="OnBeforeProductListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity being deleted</param>
        public virtual Task OnBeforeProductDelete(long id) =>
            OnBeforeProductListDelete(id.StructToList());

        /// <summary>
        /// Deletes a single Product entity with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="id">The ID of the entity to delete</param>
        /// <param name="authorize">Whether to perform authorization check for Delete operation</param>
        public async virtual Task DeleteProduct(long id, bool authorize)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeProductDelete(id);

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeProductDeleteAndThrow(id);
                }

                List<long> listForDelete_1 = id.StructToList();



                await DeleteEntityAsync<Product, long>(id);
            });
        }

        /// <summary>
        /// Lifecycle hook called before deleting a list of Product entities.
        /// Override this to add custom validation or business logic before batch deletion.
        /// </summary>
        /// <param name="listForDelete">The list of entity IDs being deleted</param>
        public virtual async Task OnBeforeProductListDelete(List<long> listForDelete) { }

        /// <summary>
        /// Deletes multiple Product entities with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="listForDelete_1">The list of entity IDs to delete</param>
        /// <param name="authorize">Whether to perform authorization check for Delete operation</param>
        public async virtual Task DeleteProductList(List<long> listForDelete_1, bool authorize)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeProductListDelete(listForDelete_1);

                if (authorize)
                {
                    await _deps.AuthorizationService.AuthorizeProductDeleteAndThrow(listForDelete_1);
                }



                await DeleteEntitiesAsync<Product, long>(listForDelete_1);
            });
        }

        #endregion

        #region One To Many



        #endregion

    }
}