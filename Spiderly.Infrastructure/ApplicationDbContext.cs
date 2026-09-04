using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Spiderly.Shared.BaseEntities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Spiderly.Shared.Interfaces;
using System.Reflection;
using Spiderly.Security.Helpers;
using Spiderly.Security.Interfaces;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
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
            mutableEntityTypes.ConfigureOneToOneRelationships(modelBuilder);

            ConstrainAccountKeyToCanonicalForm(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Constrains the user entity's e-mail to its canonical (lower-cased) form, so the unique
        /// index over it means ONE ACCOUNT PER ADDRESS whatever casing was typed.
        /// </summary>
        /// <remarks>
        /// Without it that guarantee silently depends on the database: SQL Server's default collation
        /// is case-insensitive, so a unique index there already enforces it, while on Postgres
        /// <c>a@x.com</c> and <c>A@x.com</c> are two rows — and <c>SecurityServiceBase</c> will
        /// happily auto-provision the second account. This makes both providers promise the same
        /// thing, which is why it is expressed as a constraint on the value rather than as a
        /// case-insensitive column type (<c>citext</c>): a type would apply case-insensitive
        /// semantics to comparisons the consumer never asked about, including joins against their own
        /// ordinary e-mail columns.
        /// <para>
        /// <see cref="CanonicalizeAccountKey"/> is what normally satisfies it, so in a healthy app
        /// this never fires. It is the backstop for the two paths that one cannot reach: the
        /// synchronous <c>SaveChanges</c>, which this class does not override (the framework-wide
        /// async-only gap that an <c>ISaveChangesInterceptor</c> rework is proposed to close), and
        /// raw SQL.
        /// </para>
        /// <para>
        /// <b>On upgrade this can fail to apply</b>, and that is the point: it fails exactly when the
        /// database already holds two accounts for one address. Merging those is a per-pair judgement
        /// about which account survives, not a script, so it is deliberately the consumer's to make
        /// before the migration runs.
        /// </para>
        /// </remarks>
        void ConstrainAccountKeyToCanonicalForm(ModelBuilder modelBuilder)
        {
            // A check constraint is raw SQL, so the column has to be named and quoted by hand — but
            // the provider already knows how to do both. Asking it keeps this working for a provider
            // Spiderly does not support yet, and keeps the name tied to the interface rather than to
            // a literal. (A [Column] rename on the consumer's property would still need handling;
            // no template or consumer does that today.)
            string email = this.GetService<ISqlGenerationHelper>().DelimitIdentifier(nameof(IUser.Email));

            // TUser is already in the model: every real one carries [SpiderlyEntity], so discovery
            // registered it above and it went through all three relationship passes. Registering it
            // here instead would ADD it after those passes had been snapshotted — a silently
            // under-configured entity in exactly the case the registration would be defending.
            modelBuilder.Entity<TUser>().ToTable(t => t.HasCheckConstraint(
                $"CK_{typeof(TUser).Name}_Email_Lowercase", $"{email} = LOWER({email})"));
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
                CanonicalizeAccountKey(changedEntity);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Stamps the canonical form of the account key on every tracked write of the user entity.
        /// </summary>
        /// <remarks>
        /// Beside the audit stamping because it is the same kind of fact — a framework invariant
        /// about a framework-owned column — and because this is the one seam every tracked write
        /// passes, including a consumer's own <c>DbSet&lt;TUser&gt;().Add(...)</c> that no generated
        /// service can see. <c>SecurityServiceBase</c> already mints and links accounts on this
        /// address (see <see cref="Security.Helpers.EmailNormalizer"/>), so the framework asserts
        /// its semantics whether or not it stamps them; stamping them is what makes the assertion
        /// hold for writes the framework did not originate.
        /// <para>
        /// Deliberately scoped to <see cref="IUser"/>'s <c>Email</c>. An address on any other entity
        /// is contact data the consumer's operator typed, and folding it would be the framework
        /// rewriting input it does not own.
        /// </para>
        /// <para>
        /// <see cref="ConstrainAccountKeyToCanonicalForm"/> is the backstop for the writes this
        /// cannot see.
        /// </para>
        /// </remarks>
        void CanonicalizeAccountKey(EntityEntry changedEntity)
        {
            if (changedEntity.Entity is IUser user
                && changedEntity.State is EntityState.Added or EntityState.Modified
                && user.Email != null)
            {
                user.Email = EmailNormalizer.Normalize(user.Email);
            }
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
                    Type? currentType = entityClrType.BaseType;
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
                    // Stamp CreatedAt only when the caller left it unset (default). A hand-written
                    // insert that assigns an explicit historical CreatedAt — e.g. a migration or an
                    // import preserving a source system's creation dates — is respected. No generated
                    // path (mapper, SaveBody DTO, UI form) ever populates CreatedAt, so a non-default
                    // value here is always a deliberate assignment, never accidental caller input.
                    // ModifiedAt is stamped unconditionally: it means "last write in this system", and
                    // the insert IS that write, so a source system's modified date must not leak in.
                    if (businessObject.CreatedAt == default)
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
