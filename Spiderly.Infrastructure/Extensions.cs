using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.Attributes.Entity;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.BaseEntities;
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

                if (m2mProperties.Count != 2)
                    throw new Exception($"[M2MWithMany] attribute is required for exactly two properties in {clrType.Name}.");

                var m2mEntity_1 = m2mProperties
                    .Select(x => new { Property = x, Attribute = x.GetCustomAttribute<M2MWithManyAttribute>() })
                    .First();

                var m2mEntity_2 = m2mProperties
                    .Select(x => new { Property = x, Attribute = x.GetCustomAttribute<M2MWithManyAttribute>() })
                    .Last();

                PropertyInfo m2mWithManyProperty_1 = mutableEntityTypes
                    .Where(x => x.Name == m2mEntity_1.Property.PropertyType.FullName)
                    .SelectMany(x => x.ClrType.GetProperties())
                    .Where(x => x.Name == m2mEntity_1.Attribute.WithManyProperty)
                    .SingleOrDefault();
                PropertyInfo m2mWithManyProperty_2 = mutableEntityTypes
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

        public static void ConfigureManyToOneRelationships(this List<IMutableEntityType> mutableEntityTypes, ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in mutableEntityTypes)
            {
                Type clrType = entityType.ClrType;

                foreach (PropertyInfo property in clrType.GetProperties())
                {
                    if (
                        property.IsManyToOneType() == false ||
                        property.GetCustomAttribute<M2MWithManyAttribute>() != null
                    )
                    {
                        continue;
                    }

                    WithManyAttribute withManyAttribute = property.GetCustomAttribute<WithManyAttribute>();

                    if (withManyAttribute == null)
                        throw new Exception($"[WithMany({property.Name}.YourOneToManyProperty)] attribute is required for ManyToOne property: {clrType.Name}.{property.Name}.");

                    RequiredAttribute requiredAttribute = property.GetCustomAttribute<RequiredAttribute>();

                    SetNullAttribute setNullAttribute = property.GetCustomAttribute<SetNullAttribute>();

                    DeleteBehavior deleteBehavior;
                    if (setNullAttribute == null)
                        deleteBehavior = DeleteBehavior.NoAction;
                    else
                        deleteBehavior = DeleteBehavior.SetNull;

                    modelBuilder.Entity(clrType)
                        .HasOne(property.PropertyType, property.Name)
                        .WithMany(withManyAttribute.WithMany)
                        .OnDelete(deleteBehavior)
                        .IsRequired(requiredAttribute != null)
                        .HasForeignKey($"{property.Name}Id");
                }
            }
        }

        private static bool IsBusinessOrReadonlyEntity(this Type type)
        {
            return
                type.BaseType == typeof(BusinessObject<byte>) ||
                type.BaseType == typeof(BusinessObject<int>) ||
                type.BaseType == typeof(BusinessObject<long>) ||
                type.BaseType == typeof(ReadonlyObject<byte>) ||
                type.BaseType == typeof(ReadonlyObject<int>) ||
                type.BaseType == typeof(ReadonlyObject<long>);
        }

        private static bool IsM2MEntity(this Type type)
        {
            return type.GetCustomAttribute<M2MAttribute>() != null;
        }

    }
}
