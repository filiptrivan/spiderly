using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    public static class SpiderlyClassFactory
    {
        public static List<SpiderlyClass> GetSpiderlyClasses(IList<ClassDeclarationSyntax> currentProjectClasses, List<SpiderlyClass> referencedProjectsClasses)
            => GetSpiderlyClasses(currentProjectClasses, referencedProjectsClasses, ImmutableArray<string>.Empty);

        /// <summary>
        /// Enum-aware overload. <paramref name="spiderlyEnumNames"/> is the set of <c>[SpiderlyEnum]</c>-decorated enum type names
        /// in the current compilation; entity properties whose stringified type matches a name in the set get <c>IsEnum = true</c>.
        /// Pass <see cref="ImmutableArray{T}.Empty"/> to opt out of enum tagging (legacy behavior).
        /// </summary>
        public static List<SpiderlyClass> GetSpiderlyClasses(IList<ClassDeclarationSyntax> currentProjectClasses, List<SpiderlyClass> referencedProjectsClasses, ImmutableArray<string> spiderlyEnumNames)
        {
            List<SpiderlyClass> result = currentProjectClasses
                .Select(x =>
                {
                    return new SpiderlyClass
                    {
                        Name = x.Identifier.Text,
                        // BaseNamespaceDeclarationSyntax covers both forms: block-scoped `namespace Foo { }`
                        // and file-scoped `namespace Foo;`. Filtering on NamespaceDeclarationSyntax alone
                        // misses the file-scoped one, which is the IDE default for a hand-added entity.
                        // Still null for a type in the global namespace — entities always declare one.
                        Namespace = x.Ancestors()
                            .OfType<BaseNamespaceDeclarationSyntax>()
                            .FirstOrDefault()!.Name.ToString(),
                        BaseType = x.GetBaseType(),
                        IsAbstract = x.IsAbstract(),
                        Description = ClassAnalyzer.GetXmlDocSummary(x),
                        Properties = ClassAnalyzer.GetAllPropertiesOfTheClass(x, currentProjectClasses, referencedProjectsClasses, spiderlyEnumNames),
                        // GetAllAttributesOfTheClass only returns null for a null `c` argument; x here always
                        // comes from currentProjectClasses, so it is never null.
                        Attributes = ClassAnalyzer.GetAllAttributesOfTheClass(x, currentProjectClasses, referencedProjectsClasses)!,
                        Methods = ClassAnalyzer.GetMethodsOfCurrentClass(x),
                        Location = x.Identifier.GetLocation(),
                    };
                })
                .OrderBy(x => x.Name)
                .ToList();

            // Tag one-to-one principal-inverse navs once, cross-entity, so IsManyToOneType() can exclude them
            // locally at every call site (it can't otherwise — the principal inverse is M2O-shaped and the local
            // predicate has no view of the other entity's [WithOne]). Genuine M2O navs carry [WithMany], so this
            // is always false for them and never perturbs M2O output. Cheap-guarded inside IsOneToOnePrincipalInverse.
            //
            // Flag BOTH current and referenced classes: generators that run in the .WebAPI project (e.g.
            // ControllerGenerator) iterate entities as *referenced* (the entities live in the .Business project),
            // so flagging only the current set would leave the controller's view unflagged and inconsistent with
            // the service's. These are the same instances those generators consume, so the flag propagates.
            List<SpiderlyClass> allClasses = result.Concat(referencedProjectsClasses).ToList();
            foreach (SpiderlyClass cls in allClasses)
                foreach (SpiderlyProperty prop in cls.Properties)
                    prop.IsOneToOnePrincipalInverseNav = prop.IsOneToOnePrincipalInverse(cls, allClasses);

            return result;
        }

        #region DTO

        public static List<SpiderlyClass> GetDTOClasses(List<SpiderlyClass> currentProjectClasses, List<SpiderlyClass> allClasses)
        {
            List<SpiderlyClass> DTOList = new();

            foreach (var x in currentProjectClasses)
            {
                if (x.HasSpiderlyDTOAttribute())
                {
                    DTOList.Add(ToHandWrittenDTO(x));
                }
                else if (x.HasSpiderlyEntityAttribute())
                {
                    string dtoNamespace = GetDtoNamespaceForEntity(x.Namespace);

                    DTOList.Add(new SpiderlyClass
                    {
                        Name = $"{x.Name}DTO",
                        BaseType = x.GetDTOBaseType(),
                        Description = x.Description,
                        Properties = GetSpiderlyDTOProperties(x, allClasses),
                        Namespace = dtoNamespace,
                        Location = x.Location,
                        IsGenerated = true
                    });
                    DTOList.Add(new SpiderlyClass
                    {
                        Name = $"{x.Name}SaveBodyDTO",
                        Properties = GetSaveBodyDTOProperties(x, allClasses),
                        Namespace = dtoNamespace,
                        Location = x.Location,
                        IsGenerated = true
                    });
                    DTOList.Add(new SpiderlyClass
                    {
                        Name = $"{x.Name}MainUIFormDTO",
                        Properties = GetMainUIFormDTOProperties(x, allClasses),
                        Namespace = dtoNamespace,
                        Location = x.Location,
                        IsGenerated = true
                    });
                }
            }

            // Merge hand-written `partial class {Entity}DTO` extensions that omitted [SpiderlyDTO]: they carry
            // the same name as a DTO Spiderly generates from an entity, so without this their members are
            // silently dropped from every artifact generator (entities.generated.ts, validators, ...) —
            // the field compiles and serializes but never reaches the generated artifacts (spiderly#258).
            // Scoped to those generated names, so a standalone unmarked DTO still requires the marker. This
            // produces the same two-same-named-entries shape a [SpiderlyDTO] partial already does, which the
            // downstream callers (NgEntities, FluentValidation, Mapper, ...) group and merge.
            HashSet<string> generatedDTONames = new(DTOList.Where(d => d.IsGenerated).Select(d => d.Name));

            foreach (var x in currentProjectClasses)
            {
                if (x.HasSpiderlyDTOAttribute() || x.HasSpiderlyEntityAttribute())
                    continue;

                if (generatedDTONames.Contains(x.Name) == false)
                    continue;

                DTOList.Add(ToHandWrittenDTO(x));
            }

            return DTOList;
        }

        /// <summary>
        /// Projects a hand-written DTO class — a `[SpiderlyDTO]` class or an unmarked `partial {Entity}DTO`
        /// extension — into the DTO list verbatim, flagged non-generated so callers merge it with the
        /// entity-derived DTO of the same name.
        /// </summary>
        private static SpiderlyClass ToHandWrittenDTO(SpiderlyClass x) => new()
        {
            Name = x.Name,
            Properties = x.Properties,
            Attributes = x.Attributes,
            BaseType = x.BaseType,
            IsAbstract = x.IsAbstract,
            Methods = x.Methods,
            Namespace = x.Namespace,
            Location = x.Location,
            IsGenerated = false,
        };

        private static string GetDtoNamespaceForEntity(string entityNamespace)
        {
            string entitiesSuffix = $".{Helpers.EntitiesNamespaceEnding}";
            string dtoSuffix = $".{Helpers.DTONamespaceEnding}";

            return entityNamespace.EndsWith(entitiesSuffix)
                ? entityNamespace.Substring(0, entityNamespace.Length - entitiesSuffix.Length) + dtoSuffix
                : entityNamespace;
        }

        private static List<SpiderlyProperty> GetSaveBodyDTOProperties(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<SpiderlyProperty> result = new();

            result.Add(new SpiderlyProperty { Name = $"{entity.Name}DTO", Type = $"{entity.Name}DTO", EntityName = $"{entity.Name}SaveBodyDTO", IsSaveBodyMainDTO = true });

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x =>
                    x.HasExcludeFromDTOAttribute() == false &&
                    (
                        x.HasUIOrderedOneToManyAttribute() ||
                        x.IsMultiSelectControlType() ||
                        x.IsMultiAutocompleteControlType() ||
                        x.HasSimpleManyToManyTableLazyLoadAttribute() ||
                        x.HasComplexManyToManyListAttribute()
                    )
                )
            )
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);
                string extractedEntityIdType = extractedEntity.GetIdType(entities);


                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyProperty orderedProp = new SpiderlyProperty { Name = $"Ordered{property.Name}SaveBodyDTO", Type = $"List<{extractedEntity.Name}SaveBodyDTO>", EntityName = $"{entity.Name}SaveBodyDTO" };

                    if (property.Attributes.Any(x => x.Name == "Required"))
                        orderedProp.Attributes.Add(new SpiderlyAttribute { Name = "Required" });

                    result.Add(orderedProp);
                }
                else if (property.IsMultiSelectControlType())
                {
                    result.Add(new SpiderlyProperty { Name = $"Selected{property.Name}Ids", Type = $"List<{extractedEntityIdType}>", EntityName = $"{entity.Name}SaveBodyDTO" });
                }
                else if (property.IsMultiAutocompleteControlType())
                {
                    result.Add(new SpiderlyProperty { Name = $"Selected{property.Name}NamebookDTOList", Type = $"List<NamebookDTO<{extractedEntityIdType}>>", EntityName = $"{entity.Name}SaveBodyDTO" });
                }
                else if (property.HasSimpleManyToManyTableLazyLoadAttribute())
                {
                    result.Add(new SpiderlyProperty { Name = $"Selected{property.Name}Ids", Type = $"List<{extractedEntityIdType}>", EntityName = $"{entity.Name}SaveBodyDTO" });
                    result.Add(new SpiderlyProperty { Name = $"Unselected{property.Name}Ids", Type = $"List<{extractedEntityIdType}>", EntityName = $"{entity.Name}SaveBodyDTO" });
                    result.Add(new SpiderlyProperty { Name = $"AreAll{property.Name}Selected", Type = "bool?", EntityName = $"{entity.Name}SaveBodyDTO" });
                    result.Add(new SpiderlyProperty { Name = $"{property.Name}TableFilter", Type = "FilterDTO", EntityName = $"{entity.Name}SaveBodyDTO" });
                }
                else if (property.HasComplexManyToManyListAttribute())
                {
                    result.Add(new SpiderlyProperty { Name = property.Name, Type = $"List<{extractedEntity.Name}DTO>", EntityName = $"{entity.Name}SaveBodyDTO" });
                }
            }

            return result;
        }

        private static List<SpiderlyProperty> GetMainUIFormDTOProperties(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<SpiderlyProperty> result = new();

            result.Add(new SpiderlyProperty { Name = $"{entity.Name}DTO", Type = $"{entity.Name}DTO", EntityName = $"{entity.Name}MainUIFormDTO" });

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x =>
                    x.HasExcludeFromDTOAttribute() == false &&
                    (
                        x.HasUIOrderedOneToManyAttribute() ||
                        x.IsMultiSelectControlType() ||
                        x.IsMultiAutocompleteControlType() ||
                        x.HasComplexManyToManyListAttribute()
                    )
                )
            )
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);
                string extractedEntityIdType = extractedEntity.GetIdType(entities);

                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyProperty orderedProp = new SpiderlyProperty { Name = $"Ordered{property.Name}MainUIFormDTO", Type = $"List<{extractedEntity.Name}MainUIFormDTO>", EntityName = $"{entity.Name}MainUIFormDTO" };

                    if (property.Attributes.Any(x => x.Name == "Required"))
                        orderedProp.Attributes.Add(new SpiderlyAttribute { Name = "Required" });

                    result.Add(orderedProp);
                }
                else if (property.IsMultiSelectControlType())
                {
                    result.Add(new SpiderlyProperty { Name = $"{property.Name}Ids", Type = $"List<{extractedEntityIdType}>", EntityName = $"{entity.Name}MainUIFormDTO" });
                }
                else if (property.IsMultiAutocompleteControlType())
                {
                    result.Add(new SpiderlyProperty { Name = $"{property.Name}NamebookDTOList", Type = $"List<NamebookDTO<{extractedEntityIdType}>>", EntityName = $"{entity.Name}MainUIFormDTO" });
                }
                else if (property.HasComplexManyToManyListAttribute())
                {
                    result.Add(new SpiderlyProperty { Name = property.Name, Type = $"List<{extractedEntity.Name}DTO>", EntityName = $"{entity.Name}MainUIFormDTO" });
                }
            }

            return result;
        }

        public static List<SpiderlyProperty> GetSpiderlyDTOProperties(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<SpiderlyProperty> DTOProperties = new(); // public string Email { get; set; }

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.ShouldSkipPropertyInDTO())
                    continue;

                foreach (SpiderlyDTOColumn column in GetDTOColumns(property, entity, entities))
                {
                    DTOProperties.Add(new SpiderlyProperty
                    {
                        Name = column.Name,
                        Type = column.Type,
                        EntityName = $"{property.EntityName}DTO",
                        Description = column.Description,
                        IsEnum = column.IsEnum,
                        IsRequired = column.IsRequired,
                    });
                }
            }

            return DTOProperties;
        }

        /// <summary>
        /// Derives the read-DTO column(s) a single entity property expands into — the single source
        /// of truth for the entity-property → DTO-column mapping. <see cref="GetSpiderlyDTOProperties"/>
        /// turns these into DTO properties; <c>ExcelPropertiesGenerator</c> matches them by
        /// <see cref="SpiderlyDTOColumn.Name"/> to know which Excel columns to drop. Keeping both
        /// directions on this one method makes silent drift between them impossible.
        /// <para>
        /// Callers that only want columns actually present in the DTO should gate on
        /// <c>ShouldSkipPropertyInDTO()</c> first (as <see cref="GetSpiderlyDTOProperties"/> does);
        /// this method assumes the property is included and only computes the column shape.
        /// </para>
        /// </summary>
        public static IEnumerable<SpiderlyDTOColumn> GetDTOColumns(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            // The principal side of a 1-1 is excluded from the read DTO by default: the FK lives on the
            // dependent, so flattening it here would emit a bogus {Nav}Id / {Nav}DisplayName referencing a
            // column that doesn't exist on this entity. Carve it out before the M2O branch claims it.
            if (property.IsOneToOnePrincipalInverse(entity, entities))
                yield break;

            // FK-bearing reference navs (M2O + 1-1 dependent) flatten to the same read-DTO columns —
            // ManyToOneId + ManyToOneDisplayName. (The principal inverse was skipped above.)
            bool isRequired = property.IsEffectivelyRequired();

            if (property.IsForeignKeyReferenceNav())
            {
                SpiderlyClass manyToOneClass = entities.SingleOrDefault(x => x.Name == property.Type.Name);

                yield return new SpiderlyDTOColumn { Name = $"{property.Name}DisplayName", Type = "string", Kind = SpiderlyDTOColumnKind.ManyToOneDisplayName };

                // Skip FK synthesis when an explicit FK scalar is declared on the entity —
                // the scalar flows through the Scalar branch below under its real name
                // (which may be {NavName}Id by convention, or a renamed property via [ForeignKey]).
                // The 1-1 dependent always declares its FK explicitly today, so it takes this skip and
                // its FK column is emitted exactly once, by the scalar branch.
                if (property.ResolveExplicitForeignKeyName(entity) == null)
                    yield return new SpiderlyDTOColumn { Name = $"{property.Name}Id", Type = GetFormatedDTOPropertyType(manyToOneClass.GetIdType(entities)), Kind = SpiderlyDTOColumnKind.ManyToOneId, IsRequired = isRequired };
            }
            else if (property.Type.IsOneToManyType() && property.HasGenerateCommaSeparatedDisplayNameAttribute())
            {
                yield return new SpiderlyDTOColumn { Name = $"{property.Name}CommaSeparated", Type = "string", Kind = SpiderlyDTOColumnKind.OneToManyCommaSeparated };
            }
            else if (property.Type.IsOneToManyType() && property.HasIncludeInDTOAttribute())
            {
                yield return new SpiderlyDTOColumn { Name = $"{property.Name}DTOList", Type = property.Type.Raw.WithoutNullableSuffix().Replace(">", "DTO>"), Kind = SpiderlyDTOColumnKind.OneToManyDTOList };
            }
            else if (property.IsBlob())
            {
                yield return new SpiderlyDTOColumn { Name = $"{property.Name}Data", Type = "string", Kind = SpiderlyDTOColumnKind.BlobData, IsRequired = isRequired };
                yield return new SpiderlyDTOColumn { Name = property.Name, Type = "string", Kind = SpiderlyDTOColumnKind.BlobValue, IsRequired = isRequired };
            }
            else
            {
                yield return new SpiderlyDTOColumn { Name = property.Name, Type = GetFormatedDTOPropertyType(property.Type.Raw), Kind = SpiderlyDTOColumnKind.Scalar, Description = property.Description, IsEnum = property.IsEnum, IsRequired = isRequired };
            }
        }

        /// <summary>
        /// The DTO type for a scalar entity property.
        /// <para>
        /// Value-typed scalars are ALWAYS <c>Nullable&lt;T&gt;</c>, even under <c>[Required]</c>. This looks
        /// like the DTO understating what the schema guarantees, and it is — deliberately. A generated
        /// <c>{Entity}DTO</c> serves four roles at once: response shape, request shape, the Angular form
        /// model (an empty numeric input posts <c>null</c>), and the sparse placeholder carrier for
        /// <c>[ComplexManyToManyList]</c> grids, where an all-null row IS the sentinel for "no record"
        /// (see <c>ServiceOneToManyGenerator</c>'s placeholder skip). The last two need <c>null</c> to be
        /// representable, and <c>int</c> cannot hold it. Making value types follow <c>[Required]</c> was
        /// tried and reverted: it broke the FK unwrap, the placeholder protocol, and paginated filtering.
        /// Value-type honesty needs the read/write DTO split first.
        /// </para>
        /// <para>
        /// Reference types are different and DO follow <c>[Required]</c>: their <c>?</c> is only an
        /// ANNOTATION (illegal in an oblivious context, CS8632), applied by
        /// <c>EntitiesToDTOGenerator</c>. With <c>RespectNullableAnnotations</c> off, a non-nullable
        /// <c>string</c> still accepts <c>null</c> at runtime, so the form and placeholder protocols are
        /// untouched by it.
        /// </para>
        /// </summary>
        public static string GetFormatedDTOPropertyType(string propertyType)
        {
            string core = propertyType.WithoutNullableSuffix();

            if (SpiderlyTypeRef.ReferenceTypeScalarNames.Contains(core))
                return core;

            if (propertyType.IsBaseDataType())
                return $"{core}?";

            return propertyType;
        }


        #endregion
    }
}
