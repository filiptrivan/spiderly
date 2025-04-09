using Microsoft.EntityFrameworkCore;
using Spider.Shared.Interfaces;
using Spider.Shared.Exceptions;
using Spider.Shared.Resources;
using Spider.Shared.Extensions;
using Azure;
using System.ComponentModel;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Serilog;
using Spider.Shared.Helpers;

namespace Spider.Shared.Services
{
    public class BusinessServiceBase
    {
        private readonly IApplicationDbContext _context;

        public BusinessServiceBase(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<T> GetInstanceAsync<T, ID>(ID id, int? version) 
            where T : class, IBusinessObject<ID>
            where ID : struct
        {
            return await _context.WithTransactionAsync(async () =>
            {
                T poco = await _context.DbSet<T>().FindAsync(id);

                if (poco == null)
                    throw new BusinessException(SharedTerms.EntityDoesNotExistInDatabase);

                if (version.HasValue && poco.Version != version)
                    throw new BusinessException(SharedTerms.ConcurrencyException);

                return poco;
            });
        }

        public async Task<T> GetInstanceAsync<T, ID>(ID id) 
            where T : class, IReadonlyObject<ID>
            where ID : struct
        {
            return await _context.WithTransactionAsync(async () =>
            {
                T poco = await _context.DbSet<T>().FindAsync(id);

                if (poco == null)
                    throw new BusinessException(SharedTerms.EntityDoesNotExistInDatabase);

                return poco;
            });
        }

        protected internal async Task CheckVersionAsync<T, ID>(ID id, int version) 
            where T : class, IBusinessObject<ID> 
            where ID : struct
        {
            await _context.WithTransactionAsync(async () =>
            {
                int dbVersion = await _context.DbSet<T>().Where(x => x.Id.Equals(id)).Select(x => x.Version).SingleOrDefaultAsync();

                if (dbVersion != version)
                    throw new BusinessException(SharedTerms.ConcurrencyException);
            });
        }

        public async Task DeleteEntityAsync<T, ID>(ID id) where T : class, IBusinessObject<ID> where ID : struct // https://www.c-sharpcorner.com/article/equality-operator-with-inheritance-and-generics-in-c-sharp/
        {
            await _context.WithTransactionAsync(async () =>
            {
                int deletedRow = await _context.DbSet<T>().Where(x => x.Id.Equals(id)).ExecuteDeleteAsync();
                if (deletedRow == 0)
                    throw new BusinessException(SharedTerms.EntityDoesNotExistInDatabaseForDeleteRequest);
            });
        }

        public async Task DeleteEntitiesAsync<T, ID>(List<ID> ids) where T : class, IBusinessObject<ID> where ID : struct
        {
            if (ids == null)
                throw new ArgumentNullException("You need to pass a list of ids to delete.");

            if (ids.Count == 0)
                return; // FT: Early return, don't make db call

            await _context.WithTransactionAsync(async () =>
            {
                await _context.DbSet<T>().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
            });
        }

    }
}
