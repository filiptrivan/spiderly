using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.BaseEntities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Spiderly.Shared.Interfaces;
using System.Reflection;
using Spiderly.Security.Interfaces;
using Microsoft.EntityFrameworkCore.Metadata;
using Spiderly.Infrastructure.Converters;
using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Infrastructure
{
    /// <summary>
    /// Represents the application's database context, including common entities such as users, roles, and permissions.
    /// Supports generic user types implementing <see cref="IUser"/> and automatically registers all entity types
    /// from assemblies with the ".Entities" namespace. Applies custom relationship configurations and handles 
    /// auditing and versioning for tracked business entities.
    /// </summary>
    /// <typeparam name="TUser">The user type used in the application, which must implement <see cref="IUser"/>.</typeparam>
    public class ApplicationDbContext<TUser> : DbContext, IApplicationDbContext
        where TUser : class, IUser, new()
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext<TUser>> options)
                : base(options)
        {
        }

        protected ApplicationDbContext(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            List<Type> entityTypes = EntityTypeDiscovery.GetAllEntityTypes();

            foreach (Type entityType in entityTypes)
                modelBuilder.Entity(entityType);

            List<IMutableEntityType> mutableEntityTypes = modelBuilder.Model.GetEntityTypes().ToList();

            foreach (IMutableEntityType mutableEntityType in mutableEntityTypes)
            {
                Type entityType = mutableEntityType.ClrType;
                if (entityType.IsSubclassOf(typeof(BusinessObject<byte>)))
                {
                    modelBuilder.Entity(entityType).Property("Id").ValueGeneratedOnAdd();
                }
            }

            mutableEntityTypes.ConfigureManyToManyRelationships(modelBuilder);
            mutableEntityTypes.ConfigureManyToOneRelationships(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }


        public DbSet<TEntity> DbSet<TEntity>() where TEntity : class
        {
            return Set<TEntity>();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (EntityEntry changedEntity in ChangeTracker.Entries())
            {
                HandleObjectChanges(changedEntity);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        void HandleObjectChanges(EntityEntry changedEntity)
        {
            switch (changedEntity.Entity)
            {
                case BusinessObject<long> businessObjectLong:
                    HandleBusinessObjectChanges(businessObjectLong, changedEntity);
                    break;

                case BusinessObject<int> businessObjectInt:
                    HandleBusinessObjectChanges(businessObjectInt, changedEntity);
                    break;

                case BusinessObject<byte> businessObjectByte:
                    HandleBusinessObjectChanges(businessObjectByte, changedEntity);
                    break;

                default:
                    if (changedEntity.Entity is ReadonlyObject<long> or ReadonlyObject<int> or ReadonlyObject<byte>)
                        break;

                    Type entityClrType = changedEntity.Entity.GetType();
                    Type currentType = entityClrType.BaseType;
                    while (currentType != null && currentType != typeof(object))
                    {
                        if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(BusinessObject<>))
                        {
                            Type idType = currentType.GetGenericArguments()[0];
                            throw new InvalidOperationException(
                                $"Spiderly: entity '{entityClrType.Name}' inherits BusinessObject<{idType.Name}>, " +
                                $"but primary keys must be int, long, or byte. This is enforced at compile time by SPIDERLY018; " +
                                $"reaching this branch means the diagnostic was suppressed (<NoWarn> / #pragma) or the assembly " +
                                $"was built against an older Spiderly. Remove the suppression and rebuild.");
                        }
                        currentType = currentType.BaseType;
                    }
                    break;
            }
        }

        void HandleBusinessObjectChanges<T>(BusinessObject<T> businessObject, EntityEntry changedEntity) where T : struct
        {
            DateTime now = DateTime.UtcNow;

            switch (changedEntity.State)
            {
                case EntityState.Added:
                    businessObject.CreatedAt = now;
                    businessObject.ModifiedAt = now;
                    businessObject.Version = 1;
                    break;

                case EntityState.Modified:
                    Entry(businessObject).Property(x => x.CreatedAt).IsModified = false;
                    businessObject.ModifiedAt = now;
                    businessObject.Version++;
                    break;
            }
        }

        /// <summary>
        /// Configures global conventions for the Entity Framework model to automatically handle UTC conversion for all DateTime properties.
        /// </summary>
        /// <param name="configurationBuilder">The builder used to configure conventions for the model.</param>
        /// <remarks>
        /// This method applies UTC conversion to all DateTime and DateTime? properties across the entire data model:
        /// <list type="bullet">
        /// <item><description>When saving: Converts DateTime values to UTC if not already in UTC</description></item>
        /// <item><description>When reading: Ensures DateTime values are marked with DateTimeKind.Utc</description></item>
        /// <item><description>Handles both nullable and non-nullable DateTime properties</description></item>
        /// </list>
        /// This eliminates the need for manual UTC conversion throughout the application.
        /// </remarks>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<DateTimeUtcConverter>();

            configurationBuilder.Properties<DateTime?>()
                .HaveConversion<NullableDateTimeUtcConverter>();
        }

    }


}
