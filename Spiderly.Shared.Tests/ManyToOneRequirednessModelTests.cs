using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Spiderly.Infrastructure;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// <c>[Required]</c> — not the navigation's NRT annotation — decides whether a many-to-one is required,
    /// and therefore whether its foreign-key column is nullable.
    /// <para>
    /// This matters because the two disagree constantly. A navigation is conventionally written
    /// <c>= null!</c> so the entity compiles clean under <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c>, which
    /// makes EF's own convention read it as REQUIRED even when the entity carries no <c>[Required]</c>.
    /// If that convention won, the FK column would silently become NOT NULL and saving the entity without
    /// the relationship would write a default <c>0</c> instead of <c>NULL</c>. It does not win —
    /// <c>ConfigureManyToOneRelationships</c>' explicit <c>.IsRequired([Required] != null)</c> holds, which
    /// is what these tests pin. Characterization, not regression: they were written while chasing an FK
    /// violation that turned out to have a different cause, and they were green on the commit that added
    /// them.
    /// </para>
    /// <para>
    /// Worth pinning anyway, because this is the invariant a consumer's NRT migration depends on: an
    /// existing app annotating its navigations must not thereby generate a migration altering FK columns to
    /// NOT NULL. Nothing else asserts it.
    /// </para>
    /// </summary>
    public class ManyToOneRequirednessModelTests
    {
        private class Category : BusinessObject<long>
        {
            public virtual List<Comment> Comments { get; } = new();
        }

        private class Author : BusinessObject<long>
        {
            public virtual List<Comment> Comments { get; } = new();
        }

        private class Comment : BusinessObject<long>
        {
            // No [Required]: an optional relationship whose navigation is nonetheless non-nullable,
            // which is what the NRT convention prescribes for a reference property.
            [WithMany(nameof(Category.Comments))]
            public virtual Category Category { get; set; } = null!;

            [Required]
            [WithMany(nameof(Author.Comments))]
            public virtual Author Author { get; set; } = null!;
        }

        private class ManyToOneTestDbContext : DbContext
        {
            public ManyToOneTestDbContext(DbContextOptions options) : base(options) { }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Category>();
                modelBuilder.Entity<Author>();
                modelBuilder.Entity<Comment>();

                List<IMutableEntityType> mutableEntityTypes = modelBuilder.Model.GetEntityTypes().ToList();
                mutableEntityTypes.ConfigureManyToOneRelationships(modelBuilder);

                base.OnModelCreating(modelBuilder);
            }
        }

        private static IEntityType CommentEntityType()
        {
            DbContextOptions options = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(databaseName: nameof(ManyToOneRequirednessModelTests))
                .Options;

            using ManyToOneTestDbContext context = new(options);

            IEntityType? comment = context.Model.FindEntityType(typeof(Comment));
            Assert.NotNull(comment);
            return comment;
        }

        private static IProperty ForeignKeyTo<TPrincipal>(IEntityType dependent)
        {
            IForeignKey foreignKey = Assert.Single(
                dependent.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(TPrincipal));

            return Assert.Single(foreignKey.Properties);
        }

        [Fact]
        public void NavigationWithoutRequired_HasANullableForeignKey()
        {
            Assert.True(ForeignKeyTo<Category>(CommentEntityType()).IsNullable);
        }

        [Fact]
        public void NavigationWithRequired_HasANonNullableForeignKey()
        {
            Assert.False(ForeignKeyTo<Author>(CommentEntityType()).IsNullable);
        }
    }
}
