using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Spiderly.Infrastructure;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Verifies that <see cref="Extensions.ConfigureOneToOneRelationships"/> maps a <c>[WithOne]</c>
    /// dependent as a real one-to-one: a unique FK on the dependent (single-valued navs both ways)
    /// plus a declarative unique index whose NULL handling is left to provider conventions
    /// (no <c>HasFilter(null)</c>). Model metadata is read directly — no live database is required.
    /// </summary>
    public class OneToOneRelationshipModelTests
    {
        // Dependent side: holds the FK and carries [WithOne] pointing at the principal's back-navigation.
        // Mirrors the Helmio Conversation/TaskItem example documented on WithOneAttribute.
        private class Conversation : BusinessObject<long>
        {
            public long? OwningTaskItemId { get; set; }

            [WithOne(nameof(TaskItem.Conversation))]
            public virtual TaskItem OwningTaskItem { get; set; } = null!;
        }

        // Principal side: plain single-valued back-navigation, no attribute.
        private class TaskItem : BusinessObject<long>
        {
            public virtual Conversation Conversation { get; set; } = null!;
        }

        /// <summary>
        /// Registers the test pair and runs the relationship passes in the same order as the real
        /// <c>ApplicationDbContext.OnModelCreating</c> (many-to-one then one-to-one). Running the M2O pass
        /// is load-bearing: a bidirectional 1-1's navs are [WithMany]-less reference navs that the M2O pass
        /// must skip — configuring it in isolation hid a NullReferenceException that only surfaced when the
        /// full pipeline ran against a real database.
        /// </summary>
        private class OneToOneTestDbContext : DbContext
        {
            public OneToOneTestDbContext(DbContextOptions options) : base(options) { }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TaskItem>();
                modelBuilder.Entity<Conversation>();

                List<IMutableEntityType> mutableEntityTypes = modelBuilder.Model.GetEntityTypes().ToList();
                mutableEntityTypes.ConfigureManyToOneRelationships(modelBuilder);
                mutableEntityTypes.ConfigureOneToOneRelationships(modelBuilder);

                base.OnModelCreating(modelBuilder);
            }
        }

        private static IModel BuildModel()
        {
            DbContextOptions options = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(databaseName: nameof(OneToOneRelationshipModelTests))
                .Options;

            using OneToOneTestDbContext context = new(options);
            return context.Model;
        }

        [Fact]
        public void WithOne_MapsDependentAsUniqueForeignKeyToPrincipal()
        {
            IModel model = BuildModel();

            IEntityType dependent = model.FindEntityType(typeof(Conversation));
            Assert.NotNull(dependent);

            IForeignKey foreignKey = Assert.Single(dependent.GetForeignKeys());

            // One-to-one: the FK is unique and points at the principal.
            Assert.True(foreignKey.IsUnique);
            Assert.Equal(typeof(TaskItem), foreignKey.PrincipalEntityType.ClrType);

            // FK is over the explicit OwningTaskItemId column.
            IProperty fkProperty = Assert.Single(foreignKey.Properties);
            Assert.Equal(nameof(Conversation.OwningTaskItemId), fkProperty.Name);

            // Single-valued navigations both ways (not a collection).
            Assert.NotNull(foreignKey.DependentToPrincipal);
            Assert.Equal(nameof(Conversation.OwningTaskItem), foreignKey.DependentToPrincipal.Name);
            Assert.NotNull(foreignKey.PrincipalToDependent);
            Assert.Equal(nameof(TaskItem.Conversation), foreignKey.PrincipalToDependent.Name);
        }

        [Fact]
        public void WithOne_EmitsDeclarativeUniqueIndexWithoutNullCollapsingFilter()
        {
            IModel model = BuildModel();

            IEntityType dependent = model.FindEntityType(typeof(Conversation));
            Assert.NotNull(dependent);

            // Exactly one unique index, over the FK column.
            IIndex index = Assert.Single(dependent.GetIndexes());
            Assert.True(index.IsUnique);

            IProperty indexProperty = Assert.Single(index.Properties);
            Assert.Equal(nameof(Conversation.OwningTaskItemId), indexProperty.Name);

            // Declarative index: NULL handling is left to provider conventions, so no explicit
            // HasFilter(...) is applied here. (Relational GetFilter() is unavailable on the InMemory
            // provider, so we assert the absence of an explicit filter annotation instead.)
            object explicitFilter = index.FindAnnotation("Relational:Filter")?.Value;
            Assert.Null(explicitFilter);
        }
    }
}
