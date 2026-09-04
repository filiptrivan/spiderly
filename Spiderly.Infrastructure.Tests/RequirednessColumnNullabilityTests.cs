using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;

namespace Spiderly.Infrastructure.Tests
{
    /// <summary>
    /// What decides a column's nullability, pinned against the REAL model: the fixture derives from
    /// <see cref="ApplicationDbContext{TUser}"/> without overriding <c>OnModelCreating</c>, so entity
    /// discovery and all three <c>Configure*Relationships</c> passes run in production order, over
    /// <b>Npgsql</b> with <b>lazy-loading proxies</b> on. No connection is ever opened — a model and its
    /// DDL are built from metadata alone.
    /// <para>
    /// Supersedes <c>Spiderly.Shared.Tests.ManyToOneRequirednessModelTests</c>, which asserts the same
    /// navigation invariant but over <c>UseInMemoryDatabase</c>, with only the many-to-one pass and no
    /// proxies. Column nullability is decided by RELATIONAL conventions, so a green in-memory model is not
    /// evidence about a real one — which is precisely how a foreign-key incident survived a passing suite.
    /// </para>
    /// <para>
    /// Two different rules live here, and the difference is deliberate. For NAVIGATIONS Spiderly owns
    /// requiredness outright (<c>Extensions.ConfigureManyToOneRelationships</c> calls
    /// <c>.IsRequired([Required] != null)</c>), so every navigation case is an invariant we must hold. For
    /// SCALARS Spiderly configures nothing and EF's conventions decide, so those cases are
    /// CHARACTERIZATION: they record what EF does, including the one case where a non-nullable annotation
    /// silently wins over a missing <c>[Required]</c>. That case is why SPIDERLY028 exists, and it is the
    /// reason we deliberately did NOT add a scalar-requiredness pass — see
    /// <c>SpiderlyDiagnostics.NullabilityRequirednessMismatch</c>.
    /// </para>
    /// <para>
    /// The entity shapes below include the two disagreements SPIDERLY028 rejects at build time. They are
    /// declarable here only because analyzers do not flow through a <c>ProjectReference</c> — the
    /// generators are referenced as an <c>Analyzer</c> by <c>Spiderly.Security</c> alone, so no Spiderly
    /// diagnostic runs in this project.
    /// </para>
    /// </summary>
    public class RequirednessColumnNullabilityTests
    {
        // --- Navigations: Spiderly configures these explicitly, so each case is an invariant ---

        [Fact]
        public void RequiredNavigation_HasNonNullableForeignKey()
        {
            Assert.False(ColumnIsNullable<NavShapes>("RequiredNavId"));
        }

        [Fact]
        public void OptionalNavigation_HasNullableForeignKey()
        {
            Assert.True(ColumnIsNullable<NavShapes>("OptionalNavId"));
        }

        [Fact]
        public void RequiredNavigation_AnnotatedNullable_StillHasNonNullableForeignKey()
        {
            // [Required] wins over the annotation: .IsRequired(true) is explicit fluent config, which
            // outranks EF's nullable-reference convention.
            Assert.False(ColumnIsNullable<NavShapes>("RequiredButNullableNavId"));
        }

        [Fact]
        public void OptionalNavigation_AnnotatedNonNullable_StillHasNullableForeignKey()
        {
            // THE INCIDENT SHAPE. A navigation with no [Required] annotated non-nullable — what a consumer
            // writes by reflex when flipping to <Nullable>enable</Nullable>. .IsRequired(false) is called
            // explicitly for it, so the FK must stay nullable; if it does not, an insert that legitimately
            // omits the relationship writes a default 0 into a non-nullable shadow FK and dies on a
            // foreign-key violation rather than storing NULL.
            Assert.True(ColumnIsNullable<NavShapes>("OptionalButNonNullableNavId"));
        }

        // --- Scalars: Spiderly configures nothing; these record what EF's conventions do ---

        [Fact]
        public void RequiredScalar_IsNonNullableColumn()
        {
            Assert.False(ColumnIsNullable<ScalarShapes>(nameof(ScalarShapes.RequiredScalar)));
        }

        [Fact]
        public void OptionalScalar_IsNullableColumn()
        {
            Assert.True(ColumnIsNullable<ScalarShapes>(nameof(ScalarShapes.OptionalScalar)));
        }

        [Fact]
        public void RequiredScalar_AnnotatedNullable_IsNonNullableColumn()
        {
            // The ATTRIBUTE wins here — RequiredPropertyAttributeConvention is a DataAnnotation source and
            // outranks the nullable-reference convention. This is the half of the scalar story the
            // SPIDERLY028 descriptor originally got wrong.
            Assert.False(ColumnIsNullable<ScalarShapes>(nameof(ScalarShapes.RequiredButNullableScalar)));
        }

        [Fact]
        public void OptionalScalar_AnnotatedNonNullable_IsNonNullableColumn()
        {
            // The ANNOTATION wins here — nothing opposes NonNullableReferencePropertyConvention, so the
            // column silently becomes NOT NULL even though [Required] is absent. Characterization, not an
            // endorsement: this is the disagreement SPIDERLY028 rejects at build time, which is what makes
            // a runtime scalar-requiredness pass unnecessary.
            Assert.False(ColumnIsNullable<ScalarShapes>(nameof(ScalarShapes.OptionalButNonNullableScalar)));
        }

        // --- The nullable-oblivious consumer: [Required] is the only signal, and it must be enough ---

        [Fact]
        public void ObliviousConsumer_RequiredNavigation_HasNonNullableForeignKey()
        {
            // Load-bearing for UPGRADES, not for correctness: every existing Spiderly app is oblivious, so
            // if this ever flips, a package bump alters FK columns in a live database.
            Assert.False(ColumnIsNullable<ObliviousNavShapes>("RequiredNavId"));
        }

        [Fact]
        public void ObliviousConsumer_OptionalNavigation_HasNullableForeignKey()
        {
            Assert.True(ColumnIsNullable<ObliviousNavShapes>("OptionalNavId"));
        }

        // --- The axes that separate this harness from the in-memory test ---

        [Fact]
        public void ForeignKeyNullability_IsIndependentOfLazyLoadingProxies()
        {
            // Isolates one of the three axes the in-memory test omits. If requiredness ever differs with
            // proxies on, this fails and names the cause instead of leaving it to bisection.
            Assert.Equal(
                ColumnIsNullable<NavShapes>("OptionalButNonNullableNavId", proxies: false),
                ColumnIsNullable<NavShapes>("OptionalButNonNullableNavId", proxies: true));
        }

        [Fact]
        public void GeneratedPostgresDdl_AgreesWithTheModel()
        {
            // The model is metadata; this is the DDL a migration would actually emit. Asserting both means
            // a provider-level disagreement cannot hide behind a green metadata assertion.
            string ddl = NewContext(proxies: true).Database.GenerateCreateScript();

            Assert.Contains("\"OptionalButNonNullableNavId\" bigint", ddl);
            Assert.DoesNotContain("\"OptionalButNonNullableNavId\" bigint NOT NULL", ddl);
            Assert.Contains("\"RequiredNavId\" bigint NOT NULL", ddl);
        }

        private static bool ColumnIsNullable<TEntity>(string columnName, bool proxies = true)
        {
            using RequirednessTestDbContext context = NewContext(proxies);

            IEntityType entityType = Assert.IsAssignableFrom<IEntityType>(context.Model.FindEntityType(typeof(TEntity)));
            IProperty property = Assert.IsAssignableFrom<IProperty>(entityType.FindProperty(columnName));

            return property.IsNullable;
        }

        private static RequirednessTestDbContext NewContext(bool proxies) =>
            new(TestContexts.ModelOnlyNpgsqlOptions(proxies));

        /// <summary>
        /// Deliberately does NOT override <c>OnModelCreating</c> — the whole point is to run the real one.
        /// Consequence: <c>EntityTypeDiscovery</c> sweeps loaded assemblies for <c>[SpiderlyEntity]</c>, so
        /// any future entity marked with it in THIS test assembly joins this model. Keep test entities in
        /// this project unmarked unless they belong here.
        /// </summary>
        private sealed class RequirednessTestDbContext : ApplicationDbContext<TestUser>
        {
            public RequirednessTestDbContext(DbContextOptions options) : base(options) { }
        }

        // Public + virtual navigations: lazy-loading proxies cannot subclass a private or sealed entity.

        [SpiderlyEntity]
        public class NavTarget : BusinessObject<long>
        {
            [Required]
            public string Name { get; set; } = null!;

            public virtual List<NavShapes> ViaRequired { get; } = new();
            public virtual List<NavShapes> ViaOptional { get; } = new();
            public virtual List<NavShapes> ViaRequiredButNullable { get; } = new();
            public virtual List<NavShapes> ViaOptionalButNonNullable { get; } = new();

            public virtual List<ObliviousNavShapes> ViaObliviousRequired { get; } = new();
            public virtual List<ObliviousNavShapes> ViaObliviousOptional { get; } = new();
        }

#nullable disable
        /// <summary>
        /// What an NRT-off consumer writes: no annotations anywhere, so <c>[Required]</c> is the only signal
        /// EF has. This is the shape every existing Spiderly app is in today, which makes it the one that
        /// decides whether a change to the relationship configuration is schema-affecting on upgrade.
        /// </summary>
        [SpiderlyEntity]
        public class ObliviousNavShapes : BusinessObject<long>
        {
            [Required]
            [WithMany(nameof(NavTarget.ViaObliviousRequired))]
            public virtual NavTarget RequiredNav { get; set; }

            [WithMany(nameof(NavTarget.ViaObliviousOptional))]
            public virtual NavTarget OptionalNav { get; set; }
        }
#nullable restore

        [SpiderlyEntity]
        public class NavShapes : BusinessObject<long>
        {
            [Required]
            [WithMany(nameof(NavTarget.ViaRequired))]
            public virtual NavTarget RequiredNav { get; set; } = null!;

            [WithMany(nameof(NavTarget.ViaOptional))]
            public virtual NavTarget? OptionalNav { get; set; }

            // Disagreement SPIDERLY028 rejects: [Required] with a nullable annotation.
            [Required]
            [WithMany(nameof(NavTarget.ViaRequiredButNullable))]
            public virtual NavTarget? RequiredButNullableNav { get; set; }

            // Disagreement SPIDERLY028 rejects, and the incident shape: no [Required], annotated
            // non-nullable.
            [WithMany(nameof(NavTarget.ViaOptionalButNonNullable))]
            public virtual NavTarget OptionalButNonNullableNav { get; set; } = null!;
        }

        [SpiderlyEntity]
        public class ScalarShapes : BusinessObject<long>
        {
            [Required]
            public string RequiredScalar { get; set; } = null!;

            public string? OptionalScalar { get; set; }

            // Disagreement SPIDERLY028 rejects: [Required] with a nullable annotation.
            [Required]
            public string? RequiredButNullableScalar { get; set; }

            // Disagreement SPIDERLY028 rejects: no [Required], annotated non-nullable.
            public string OptionalButNonNullableScalar { get; set; } = null!;
        }

    }
}
