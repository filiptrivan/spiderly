//HintName: StudentService.generated.cs
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
    /// Generated service for the Student entity. Override lifecycle hooks
    /// by creating a <c>StudentService</c> class that inherits from this class.
    /// </summary>
    public class StudentServiceGenerated : ServiceBase
    {
        protected readonly EntityServiceDependencies _deps;


        public StudentServiceGenerated(EntityServiceDependencies deps) : base(deps.Context, deps.Localizer)
        {
            _deps = deps;

        }

        #region Read

        /// <summary>
        /// Retrieves the complete MainUIFormDTO for Student, including the entity DTO and all related collections (one-to-many, many-to-many).
        /// </summary>
        /// <param name="id">The ID of the Student entity</param>
        /// <returns>StudentMainUIFormDTO containing the entity DTO and related data</returns>
        public async virtual Task<StudentMainUIFormDTO> GetStudentMainUIFormDTO(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = new StudentMainUIFormDTO
                {
                    StudentDTO = await GetStudentDTO(id),
                };

                await OnAfterGetStudentMainUIFormDTO(result);

                return result;
            });
        }

        /// <summary>
        /// Lifecycle hook called after retrieving Student MainUIFormDTO.
        /// Override this method to enrich the MainUIFormDTO with additional data (e.g., computed fields, extra queries).
        /// This method runs inside a database transaction.
        /// </summary>
        /// <example>
        /// protected override async Task OnAfterGetStudentMainUIFormDTO(StudentMainUIFormDTO mainUIFormDTO)
        /// {
        ///     mainUIFormDTO.CustomProperty = await _deps.Context.DbSet&lt;OtherEntity&gt;().Where(x => x.StudentId == mainUIFormDTO.StudentDTO.Id).CountAsync();
        /// }
        /// </example>
        /// <param name="mainUIFormDTO">The MainUIFormDTO that was just constructed with entity and related data</param>
        protected virtual async Task OnAfterGetStudentMainUIFormDTO(StudentMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Retrieves a single Student entity as a DTO with blob data populated.
        /// </summary>
        /// <param name="id">The ID of the Student entity</param>
        /// <returns>StudentDTO with all blob properties populated</returns>
        public async virtual Task<StudentDTO> GetStudentDTO(long id)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dto = await _deps.Context.DbSet<Student>()
                    .AsNoTracking()
                    .Where(x => x.Id == id).ProjectToType<StudentDTO>(Mapper.StudentProjectToConfig())
                    .SingleOrDefaultAsync();

                if (dto == null)
                    throw new BusinessException(_deps.Localizer["EntityDoesNotExistInDatabase"]);



                return dto;
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Student entities.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResult containing the query and total record count</returns>
        public async virtual Task<PaginatedResult<Student>> GetPaginatedStudentResult(FilterDTO filterDTO, IQueryable<Student> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                return await PaginatedResultGenerator.Build(query.AsNoTracking(), filterDTO);
            });
        }

        /// <summary>
        /// Retrieves a paginated list of Student DTOs with blob data populated.
        /// </summary>
        /// <param name="filterDTO">Filter and pagination parameters</param>
        /// <param name="query">The base query to paginate</param>
        /// <returns>PaginatedResultDTO containing StudentDTO list and total record count</returns>
        public async virtual Task<PaginatedResultDTO<StudentDTO>> GetPaginatedStudentList(FilterDTO filterDTO, IQueryable<Student> query)
        {
            PaginatedResult<Student> paginationResult = new();
            List<StudentDTO> dtoList = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                paginationResult = await GetPaginatedStudentResult(filterDTO, query);

                dtoList = await paginationResult.Query
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ProjectToType<StudentDTO>(Mapper.StudentProjectToConfig())
                    .ToListAsync();


            });

            return new PaginatedResultDTO<StudentDTO> { Data = dtoList, TotalRecords = paginationResult.TotalRecords };
        }

        /// <summary>
        /// Exports a filtered list of Student entities to Excel format.
        /// </summary>
        /// <param name="filterDTO">Filter parameters for the export</param>
        /// <param name="query">The base query to export</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        public async virtual Task<byte[]> ExportStudentListToExcel(FilterDTO filterDTO, IQueryable<Student> query, CancellationToken cancellationToken = default)
        {
            IQueryable<StudentDTO> exportQuery = null;

            await _deps.Context.WithTransactionAsync(async () =>
            {
                PaginatedResult<Student> paginationResult = await GetPaginatedStudentResult(filterDTO, query);
                int maxRows = _deps.ExcelSettings.ExcelExportMaxRows;
                exportQuery = paginationResult.Query
                    .OrderBy(x => x.Id)
                    .Take(maxRows)
                    .ProjectToType<StudentDTO>(Mapper.StudentExcelProjectToConfig());
            });

            string[] excelPropertiesToExclude = ExcelPropertiesToExclude.GetHeadersToExclude(new StudentDTO());
            return await _deps.ExcelService.FillReportTemplateAsync(
                exportQuery.AsAsyncEnumerable(),
                excelPropertiesToExclude,
                _deps.Localizer,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves a list of Student entities without pagination.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of Student entities</returns>
        public async virtual Task<List<Student>> GetStudentList(IQueryable<Student> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var result = await query
                    .ToListAsync();

                return result;
            });
        }

        /// <summary>
        /// Retrieves a list of Student DTOs without pagination, with blob data populated.
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <returns>List of StudentDTO with blob properties populated</returns>
        public async virtual Task<List<StudentDTO>> GetStudentDTOList(IQueryable<Student> query)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var dtoList = await query
                    .AsNoTracking()
                    .ProjectToType<StudentDTO>(Mapper.StudentToDTOConfig())
                    .ToListAsync();



                return dtoList;
            });
        }



        #endregion

        #region Save

        /// <summary>
        /// Saves a Student entity and returns the complete MainUIFormDTO including all related collections.
        /// Handles insert/update logic, many-to-many relationships, and ordered one-to-many collections.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity data and related selections</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>StudentMainUIFormDTO with saved data and updated collections</returns>
        public virtual async Task<StudentMainUIFormDTO> SaveStudentAndReturnMainUIFormDTO(StudentSaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            new StudentSaveBodyDTOValidationRules().ValidateAndThrow(saveBodyDTO);

            return await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeSaveStudentAndReturnMainUIFormDTO(saveBodyDTO);

                var savedDTO = await SaveStudentAndReturnDTO(saveBodyDTO.StudentDTO, authorizeUpdate, authorizeInsert);






                var result = new StudentMainUIFormDTO
                {
                    StudentDTO = savedDTO,



                };

                await OnAfterSaveStudentAndReturnMainUIFormDTO(saveBodyDTO, result);

                return result;
            });
        }




        /// <summary>
        /// Lifecycle hook called before saving Student with MainUIFormDTO.
        /// Override this method to add custom validation or modify the SaveBodyDTO.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The SaveBodyDTO containing entity and related data</param>
        protected virtual async Task OnBeforeSaveStudentAndReturnMainUIFormDTO(StudentSaveBodyDTO saveBodyDTO) { }

        /// <summary>
        /// Lifecycle hook called after saving Student and after updating related collections.
        /// Override this method to add custom business logic after the main entity is saved.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="saveBodyDTO">The original SaveBodyDTO</param>
        /// <param name="mainUIFormDTO">The save result and DTO sent to the UI</param>
        protected virtual async Task OnAfterSaveStudentAndReturnMainUIFormDTO(StudentSaveBodyDTO saveBodyDTO, StudentMainUIFormDTO mainUIFormDTO) { }

        /// <summary>
        /// Saves a Student entity and returns the DTO with blob data populated.
        /// </summary>
        /// <param name="saveDTO">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved StudentDTO with blob properties populated</returns>
        public async virtual Task<StudentDTO> SaveStudentAndReturnDTO(StudentDTO saveDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            return await _deps.Context.WithTransactionAsync(async () =>
            {
                var poco = await SaveStudent(saveDTO, authorizeUpdate, authorizeInsert);

                var dto = poco.Adapt<StudentDTO>(Mapper.StudentToDTOConfig());



                return dto;
            });
        }

        /// <summary>
        /// Core save method that handles both insert and update operations for Student.
        /// Validates the DTO, maps to entity, handles many-to-one relationships, and manages blob deletion.
        /// </summary>
        /// <param name="dto">The DTO containing entity data to save</param>
        /// <param name="authorizeUpdate">Whether to perform authorization check for Update operation</param>
        /// <param name="authorizeInsert">Whether to perform authorization check for Insert operation</param>
        /// <returns>Saved Student entity</returns>
        public async virtual Task<Student> SaveStudent(StudentDTO dto, bool authorizeUpdate, bool authorizeInsert)
        {
            StudentDTOValidationRules validationRules = new StudentDTOValidationRules();
            validationRules.ValidateAndThrow(dto);

            Student poco = null;
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeStudentIsMapped(dto);
                DbSet<Student> dbSet = _deps.Context.DbSet<Student>();

                if (dto.Id > 0)
                {
                    if (authorizeUpdate)
                    {
                        await _deps.AuthorizationService.AuthorizeStudentUpdateAndThrow(dto);
                    }

                    poco = await GetInstanceAsync<Student, long>(dto.Id, dto.Version);
                    await OnBeforeStudentUpdate(poco, dto);
                    dto.Adapt(poco, Mapper.StudentDTOToEntityConfig());
                    dbSet.Update(poco);
                }
                else
                {
                    if (authorizeInsert)
                    {
                        await _deps.AuthorizationService.AuthorizeStudentInsertAndThrow(dto);
                    }

                    poco = dto.Adapt<Student>(Mapper.StudentDTOToEntityConfig());
                    await OnBeforeStudentInsert(poco, dto);
                    await dbSet.AddAsync(poco);
                }



                await _deps.Context.SaveChangesAsync();






            });

            return poco;
        }

        /// <summary>
        /// Lifecycle hook called before the StudentDTO is mapped to the entity.
        /// Override this method to add custom validation or modify the DTO before mapping.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="studentDTO">The DTO about to be mapped</param>
        protected virtual async Task OnBeforeStudentIsMapped(StudentDTO studentDTO) { }

        /// <summary>
        /// Lifecycle hook called before updating an existing Student entity.
        /// Override this method to add custom business logic during updates.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="student">The existing entity being updated</param>
        /// <param name="studentDTO">The DTO containing new data</param>
        protected virtual async Task OnBeforeStudentUpdate(Student student, StudentDTO studentDTO) { }

        /// <summary>
        /// Lifecycle hook called before inserting a new Student entity.
        /// Override this method to add custom business logic during inserts.
        /// This method runs inside a database transaction.
        /// </summary>
        /// <param name="student">The new entity being inserted</param>
        /// <param name="studentDTO">The DTO containing the data</param>
        protected virtual async Task OnBeforeStudentInsert(Student student, StudentDTO studentDTO) { }





        #endregion

        #region Delete

        /// <summary>
        /// Per-id variant of the pre-delete hook. By default forwards to
        /// <see cref="OnBeforeStudentListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity being deleted</param>
        public virtual Task OnBeforeStudentDelete(long id) =>
            OnBeforeStudentListDelete(id.StructToList());

        /// <summary>
        /// Deletes a single Student entity with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="id">The ID of the entity to delete</param>
        public async virtual Task DeleteStudent(long id)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeStudentDelete(id);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();

                List<long> listForDelete_1 = id.StructToList();



                await DeleteEntityAsync<Student, long>(id);
            });
        }

        /// <summary>
        /// Lifecycle hook called before deleting a list of Student entities.
        /// Override this to add custom validation or business logic before batch deletion.
        /// </summary>
        /// <param name="listForDelete">The list of entity IDs being deleted</param>
        public virtual async Task OnBeforeStudentListDelete(List<long> listForDelete) { }

        /// <summary>
        /// Deletes multiple Student entities with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="listForDelete_1">The list of entity IDs to delete</param>
        public async virtual Task DeleteStudentList(List<long> listForDelete_1)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBeforeStudentListDelete(listForDelete_1);

                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the delete path below is untracked ExecuteDeleteAsync, so it
                // won't flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();



                await DeleteEntitiesAsync<Student, long>(listForDelete_1);
            });
        }

        #endregion

        #region One To Many



        #endregion

    }
}