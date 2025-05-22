using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.BaseEntities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Spiderly.Shared.Interfaces;
using System.Reflection;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Entities;
using Microsoft.EntityFrameworkCore.Metadata;

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

        public DbSet<TUser> User { get; set; }
        public DbSet<Role> Role { get; set; }
        public DbSet<UserRole> UserRole { get; set; } // M2M
        public DbSet<Permission> Permission { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Automatically register all classes marked with the [Entity] attribute
            List<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

            List<string> alreadyEntityTypeNames = modelBuilder.Model.GetEntityTypes().Select(x => x.Name).ToList();

            List<Type> entityTypes = assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type != null && type.Namespace != null && type.Namespace.EndsWith(".Entities"))
                .ToList();

            foreach (Type entityType in entityTypes)
                modelBuilder.Entity(entityType);

            List<IMutableEntityType> mutableEntityTypes = modelBuilder.Model.GetEntityTypes().ToList();

            modelBuilder.Entity<UserRole>()
                .HasKey(ru => new { ru.RoleId, ru.UserId });

            modelBuilder.Entity<TUser>()
                .HasMany(e => e.Roles)
                .WithMany()
                .UsingEntity<UserRole>(
                    j => j.HasOne<Role>().WithMany().HasForeignKey(ru => ru.RoleId),
                    j => j.HasOne<TUser>().WithMany().HasForeignKey(ru => ru.UserId)
                );

            if (SettingsProvider.Current.UseGoogleAsExternalProvider == false)
            {
                modelBuilder.Entity<TUser>().Ignore(x => x.HasLoggedInWithExternalProvider);
            }

            if (SettingsProvider.Current.AppHasLatinTranslation == false)
            {
                modelBuilder.Entity<Permission>().Ignore(x => x.NameLatin);
                modelBuilder.Entity<Permission>().Ignore(x => x.DescriptionLatin);
            }

            mutableEntityTypes.ConfigureReferenceTypesSetNull(modelBuilder);
            mutableEntityTypes.ConfigureManyToManyRelationships(modelBuilder);
            mutableEntityTypes.ConfigureManyToOneRequired(modelBuilder);
            mutableEntityTypes.ConfigureManyToOneCascadeDelete(modelBuilder);

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
            }
        }

        void HandleBusinessObjectChanges<T>(BusinessObject<T> businessObject, EntityEntry changedEntity) where T : struct
        {
            DateTime now = DateTime.Now;

            switch (changedEntity.State)
            {
                case EntityState.Added:
                    businessObject.SetCreatedAt(now);
                    businessObject.SetModifiedAt(now);
                    businessObject.SetVersion(1);
                    break;

                case EntityState.Modified:
                    Entry(businessObject).Property(x => x.CreatedAt).IsModified = false;
                    businessObject.SetModifiedAt(now);
                    businessObject.SetVersion(businessObject.Version + 1);
                    break;
            }
        }

    }


}
