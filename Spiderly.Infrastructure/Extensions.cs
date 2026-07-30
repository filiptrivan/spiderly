using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.Attributes.Entity;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Infrastructure
{
    public static class Extensions
    {
        public static void ConfigureManyToManyRelationships(this List<IMutableEntityType> mutableEntityTypes, ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in mutableEntityTypes)
            {
                Type clrType = entityType.ClrType;

                if (clrType.IsM2MEntity() == false)
                    continue;

                List<PropertyInfo> properties = clrType.GetProperties().ToList();

                List<PropertyInfo> m2mProperties = properties
                    .Where(x => x != null && x.GetCustomAttribute<M2MWithManyAttribute>() != null)
                    .ToList();

                // m2mProperties is pre-filtered to properties carrying [M2MWithMany], so the re-read is non-null.
                var m2mEntity_1 = m2mProperties
                    .Select(x => new { Property = x, Attribute = x.GetCustomAttribute<M2MWithManyAttribute>()! })
                    .First();

                var m2mEntity_2 = m2mProperties
                    .Select(x => new { Property = x, Attribute = x.GetCustomAttribute<M2MWithManyAttribute>()! })
                    .Last();

                PropertyInfo? m2mWithManyProperty_1 = mutableEntityTypes
                    .Where(x => x.Name == m2mEntity_1.Property.PropertyType.FullName)
                    .SelectMany(x => x.ClrType.GetProperties())
                    .Where(x => x.Name == m2mEntity_1.Attribute.WithManyProperty)
                    .SingleOrDefault();
                PropertyInfo? m2mWithManyProperty_2 = mutableEntityTypes
                    .Where(x => x.Name == m2mEntity_2.Property.PropertyType.FullName)
                    .SelectMany(x => x.ClrType.GetProperties())
                    .Where(x => x.Name == m2mEntity_2.Attribute.WithManyProperty)
                    .SingleOrDefault();

                if (m2mWithManyProperty_1 == null || m2mWithManyProperty_2 == null)
                    throw new Exception($"Bad WithManyProperty definitions for {clrType.Name}.");

                List<string> primaryKeys = [$"{m2mEntity_1.Property.Name}Id", $"{m2mEntity_2.Property.Name}Id"];

                foreach (PropertyInfo property in properties.Where(x => x != null && x.GetCustomAttribute<KeyAttribute>() != null))
                    primaryKeys.Add($"{property.Name}Id");

                modelBuilder.Entity(clrType)
                    .HasKey(primaryKeys.ToArray());

                if (properties.Count == 2 || (m2mWithManyProperty_1.PropertyType.ToString() != m2mWithManyProperty_2.PropertyType.ToString()))
                {
                    modelBuilder.Entity(m2mEntity_1.Property.PropertyType)
                        .HasMany(m2mEntity_1.Attribute.WithManyProperty)
                        .WithMany(m2mEntity_2.Attribute.WithManyProperty)
                        .UsingEntity(
                            clrType,
                            j => j.HasOne(m2mEntity_2.Property.Name)
                                  .WithMany()
                                  .HasForeignKey($"{m2mEntity_2.Property.Name}Id"),
                            j => j.HasOne(m2mEntity_1.Property.Name)
                                  .WithMany()
                                  .HasForeignKey($"{m2mEntity_1.Property.Name}Id")
                        );
                }
                else
                {
                    modelBuilder.Entity(m2mEntity_1.Property.PropertyType)
                        .HasMany(clrType, m2mWithManyProperty_1.Name)
                        .WithOne(m2mEntity_1.Property.Name)
                        .HasForeignKey($"{m2mEntity_1.Property.Name}Id");

                    modelBuilder.Entity(m2mEntity_2.Property.PropertyType)
                        .HasMany(clrType, m2mWithManyProperty_2.Name)
                        .WithOne(m2mEntity_2.Property.Name)
                        .HasForeignKey($"{m2mEntity_2.Property.Name}Id");
                }

            }
        }

        /// <summary>
        /// Configures many-to-one relationships declared with <c>[WithMany]</c>, making <c>[Required]</c> the
        /// authority on the foreign key's nullability.
        /// <para>
        /// SCALAR requiredness is DELIBERATELY not configured here, or anywhere: EF's own conventions already
        /// land every case a consumer can legally write, and the single case where they diverge is rejected at
        /// build time by SPIDERLY028. <b>Do not add a scalar pass</b> — it would be unreachable code that also
        /// manufactures the very disagreement the diagnostic exists to prevent. Case table:
        /// <c>SpiderlyDiagnostics.NullabilityRequirednessMismatch</c>; all eight cases pinned against a real
        /// Npgsql model: <c>Spiderly.Infrastructure.Tests.RequirednessColumnNullabilityTests</c>.
        /// </para>
        /// </summary>
        public static void ConfigureManyToOneRelationships(this List<IMutableEntityType> mutableEntityTypes, ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in mutableEntityTypes)
            {
                Type clrType = entityType.ClrType;

                foreach (PropertyInfo property in clrType.GetProperties())
                {
                    WithManyAttribute? withManyAttribute = property.GetCustomAttribute<WithManyAttribute>();

                    if (
                        property.IsManyToOneType() == false ||
                        property.GetCustomAttribute<M2MWithManyAttribute>() != null ||
                        // This pass only configures navigations that declare a back-collection via [WithMany].
                        // A [WithMany]-less reference nav here is one side of a one-to-one — the [WithOne]
                        // dependent or the principal inverse — both of which ConfigureOneToOneRelationships
                        // owns. Skipping avoids dereferencing a null withManyAttribute below (the NRE that
                        // otherwise breaks model creation for any bidirectional 1-1).
                        withManyAttribute == null
                    )
                    {
                        continue;
                    }

                    RequiredAttribute? requiredAttribute = property.GetCustomAttribute<RequiredAttribute>();

                    DeleteBehavior deleteBehavior = ResolveDeleteBehavior(property);

                    string foreignKeyName = ResolveForeignKeyName(property, clrType);

                    // ORDER MATTERS: .IsRequired() must come AFTER .HasForeignKey(). HasForeignKey
                    // re-resolves the relationship's foreign-key properties, which discards requiredness
                    // configured before it; EF's conventions then decide instead of us.
                    //
                    // That went unnoticed for so long because the conventions AGREE with us in three of the
                    // four cases: [Required] is picked up by RequiredNavigationAttributeConvention, so
                    // required navigations came out NOT NULL anyway. The gap was the fourth — no [Required],
                    // annotated non-nullable, which is what a consumer writes by reflex when flipping to
                    // <Nullable>enable</Nullable>. There the discarded .IsRequired(false) left
                    // NonNullableNavigationConvention unopposed, the column became NOT NULL, and an insert
                    // legitimately omitting the relationship wrote a default 0 into the shadow FK and died
                    // on 23503 foreign_key_violation instead of storing NULL.
                    //
                    // Position, not the attribute, is what makes this authoritative: measured by forcing
                    // .IsRequired(false) here, which turns every required navigation nullable and so
                    // outranks the [Required] convention. Pinned by
                    // Spiderly.Infrastructure.Tests.RequirednessColumnNullabilityTests.
                    // ConfigureOneToOneRelationships below already had the correct order, which is why
                    // one-to-one was never affected and why the bug looked like it had no cause.
                    modelBuilder.Entity(clrType)
                        .HasOne(property.PropertyType, property.Name)
                        .WithMany(withManyAttribute.WithMany)
                        .OnDelete(deleteBehavior)
                        .HasForeignKey(foreignKeyName)
                        .IsRequired(requiredAttribute != null);
                }
            }
        }

        /// <summary>
        /// Configures one-to-one relationships declared with <c>[WithOne]</c> on the dependent
        /// (foreign-key-holding) side. Emits <c>HasOne().WithOne().HasForeignKey()</c> plus a
        /// declarative unique index on the FK column.
        /// <para>
        /// Delete behavior is app-layer only — <c>NoAction</c> by default, <c>SetNull</c> when
        /// <c>[SetNull]</c> is present. We never emit a DB-level <c>ON DELETE CASCADE</c>; cascades
        /// are handled in the generated delete pipeline.
        /// </para>
        /// <para>
        /// The unique index is declarative (<c>HasIndex(fk).IsUnique()</c>) so provider conventions
        /// handle NULLs — PostgreSQL emits <c>NULLS DISTINCT</c>, SQL Server adds an automatic
        /// <c>IS NOT NULL</c> filter — which lets an optional 1-1 keep many NULL FKs. Never raw SQL,
        /// never <c>HasFilter(null)</c>.
        /// </para>
        /// </summary>
        public static void ConfigureOneToOneRelationships(this List<IMutableEntityType> mutableEntityTypes, ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in mutableEntityTypes)
            {
                Type clrType = entityType.ClrType;

                foreach (PropertyInfo property in clrType.GetProperties())
                {
                    WithOneAttribute? withOneAttribute = property.GetCustomAttribute<WithOneAttribute>();
                    if (withOneAttribute == null)
                        continue;

                    RequiredAttribute? requiredAttribute = property.GetCustomAttribute<RequiredAttribute>();

                    DeleteBehavior deleteBehavior = ResolveDeleteBehavior(property);

                    string foreignKeyName = ResolveForeignKeyName(property, clrType);

                    modelBuilder.Entity(clrType)
                        .HasOne(property.PropertyType, property.Name)
                        .WithOne(withOneAttribute.WithOne) // null => unidirectional inverse
                        .HasForeignKey(clrType, foreignKeyName) // dependent type must be specified for reference-to-reference
                        .OnDelete(deleteBehavior)
                        .IsRequired(requiredAttribute != null);

                    modelBuilder.Entity(clrType)
                        .HasIndex(foreignKeyName)
                        .IsUnique();
                }
            }
        }

        /// <summary>
        /// App-layer delete behavior for a relationship navigation: <c>SetNull</c> when it carries
        /// <c>[SetNull]</c> (nullable FK), otherwise <c>NoAction</c>. Shared by the many-to-one and
        /// one-to-one configurators. We never emit a DB-level cascade — cascades run in the generated
        /// delete pipeline.
        /// </summary>
        private static DeleteBehavior ResolveDeleteBehavior(PropertyInfo property)
            => property.GetCustomAttribute<SetNullAttribute>() == null
                ? DeleteBehavior.NoAction
                : DeleteBehavior.SetNull;

        /// <summary>
        /// Resolves the FK column name for a many-to-one navigation. EF Core's
        /// HasForeignKey(string) overload automatically picks up a CLR property with that
        /// name if one exists on the entity; otherwise it creates a shadow property.
        /// So this resolver can stay name-based — EF decides real-vs-shadow.
        ///
        /// Priority: [ForeignKey] on navigation → [ForeignKey(nameof(Nav))] on a scalar → convention "{NavName}Id".
        /// </summary>
        private static string ResolveForeignKeyName(PropertyInfo navigation, Type clrType)
        {
            ForeignKeyAttribute? fkFromNav = navigation.GetCustomAttribute<ForeignKeyAttribute>();
            if (fkFromNav != null)
                return fkFromNav.Name;

            PropertyInfo? scalarPointingBack = clrType.GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<ForeignKeyAttribute>()?.Name == navigation.Name);
            if (scalarPointingBack != null)
                return scalarPointingBack.Name;

            return $"{navigation.Name}Id";
        }

        private static bool IsM2MEntity(this Type type)
        {
            return type.GetCustomAttribute<M2MAttribute>() != null;
        }

    }
}
