using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Generates the `PaginatedResultGenerator` static class (`PaginatedResultGenerator.generated.cs`)
    /// within the `{YourBaseNamespace}.Filtering` namespace. This class provides a method
    /// `Build` that dynamically constructs an EF Core query with filtering based on the
    /// `FilterDTO` payload. It intelligently handles filtering on properties that might
    /// exist in the DTO but not directly in the entity, by looking up mapping configurations.
    /// </summary>
    [Generator]
    public class PaginatedResultGenerator : IIncrementalGenerator
    {

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif
            var combined = PipelineFactory.CreatePipeline(context,
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO, ClassCategoryCodes.DataMappers },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO });

            var combinedWithEnums = combined
                .Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider))
                .Combine(PipelineFactory.GetNullableContextProvider(context));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var ((combinedSource, enumNames), nullableContext) = source;
                var ((classes, referencedClasses), config) = combinedSource;
                Execute(classes, referencedClasses, enumNames, config, nullableContext, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, ImmutableArray<string> spiderlyEnumNames, SpiderlyConfig config, NullableContextOptions nullableContext, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(PaginatedResultGenerator)))
                return;

            List<SpiderlyClass> spiderlyClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses, spiderlyEnumNames);
            List<SpiderlyClass> allClasses = spiderlyClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = SpiderlyClassFactory.GetDTOClasses(spiderlyClasses, allClasses);
            List<SpiderlyClass> currentProjectEntities = spiderlyClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();
            List<SpiderlyClass> allEntities = allClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();

            // This generator's pipeline also collects DTOs and data mappers, so `classes` can be non-empty
            // in a project that declares no entities at all — there is simply nothing to paginate there.
            // Same guard EntitiesToDTOGenerator and ServicesGenerator already carry.
            if (currentProjectEntities.Count == 0)
                return;

            StringBuilder sb = new();
            List<string> usings = new();
            StringBuilder sbUsings = new();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            sb.AppendLine($$"""
using LinqKit;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Classes;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Extensions;
using System.Text.Json;
using {{basePartOfNamespace}}.Entities;

namespace {{basePartOfNamespace}}.Filtering
{
    public static class PaginatedResultGenerator
    {
""");
            foreach (SpiderlyClass entity in currentProjectEntities)
            {
                sb.AppendLine($$"""
        public static async Task<PaginatedResult<{{entity.Name}}>> Build(IQueryable<{{entity.Name}}> query, FilterDTO filterDTO)
        {
            Expression<Func<{{entity.Name}}, bool>> predicate = PredicateBuilder.New<{{entity.Name}}>(true);

            foreach (KeyValuePair<string, List<FilterRuleDTO>> filter in filterDTO.Filters)
            {
                foreach (FilterRuleDTO filterRuleDTO in filter.Value)
                {
                    if (filterRuleDTO.Value != null)
                    {
                        Expression<Func<{{entity.Name}}, bool>> condition;

                        switch (filter.Key)
                        {
""");
                // I go through all the DTO properties, and if I come across one that doesn't exist in the EF class, I look for a solution in the mappers; if it doesn't exist there either, I log an appropriate error.
                List<SpiderlyClass> pairDTOClasses = currentProjectDTOClasses.Where(x => x.Name == $"{entity.Name}DTO").ToList(); // Getting the pair DTO classes of entity class
                List<SpiderlyProperty> efClassProps = entity.Properties;

                List<(string DTOPropName, string EntityDotNotation, string ResolvedType, bool IsCommaSeparated)> resolvedProps =
                    ResolveDTOProperties(pairDTOClasses, efClassProps, entity, allEntities);

                // Every field that gets a case below, in emission order — the pre-joined list is baked
                // into the unknown-field error so a client (human or agent) can self-correct.
                List<string> filterableFields = new();

                foreach (var prop in resolvedProps)
                {
                    string? caseText;

                    if (prop.IsCommaSeparated)
                    {
                        string entityPropName = prop.DTOPropName.Replace("CommaSeparated", ""); // "SegmentationItems"
                        SpiderlyProperty listProp = efClassProps.First(x => x.Name == entityPropName);
                        SpiderlyClass childEntity = Helpers.GetEntityByPropertyType(listProp, allEntities);

                        // GetCaseForEnumerable emits values.Contains(x.Id), so a keyless junction child has
                        // nothing to filter by. DEGRADE rather than throw: CommaSeparatedDisplayNameValidator
                        // already reports SPIDERLY029 for this shape, located at the property, and it does so
                        // from EntityValidationGenerator where a per-entity catch keeps the rest generating.
                        // Throwing here instead aborted this whole Execute, so the Filtering file was never
                        // emitted and EVERY entity lost Build() — a CS0103 wall hiding the real diagnostic.
                        // Omitting the case simply leaves the column unfilterable (the switch has a default).
                        string? childIdType = childEntity.GetIdTypeOrNull(allEntities);

                        caseText = childIdType != null
                            ? GetCaseForEnumerable(prop.DTOPropName, entityPropName, childIdType)
                            : null;
                    }
                    else
                    {
                        caseText = prop.ResolvedType switch
                        {
                            "string" => GetCaseForString(prop.DTOPropName, prop.EntityDotNotation),
                            "bool" or "bool?" => GetCaseForBool(prop.DTOPropName, prop.EntityDotNotation),
                            "DateTime" or "DateTime?" => GetCaseForTemporal(prop.DTOPropName, prop.EntityDotNotation, $"Convert.ToDateTime({FilterValueAsString})"),
                            "DateOnly" or "DateOnly?" => GetCaseForTemporal(prop.DTOPropName, prop.EntityDotNotation, $"DateOnly.Parse({FilterValueAsString})"),
                            "TimeOnly" or "TimeOnly?" => GetCaseForTemporal(prop.DTOPropName, prop.EntityDotNotation, $"TimeOnly.Parse({FilterValueAsString})"),
                            "long" or "long?" or "int" or "int?" or "decimal" or "decimal?"
                                or "float" or "float?" or "double" or "double?" or "byte" or "byte?"
                                => GetCaseForNumber(prop.DTOPropName, prop.EntityDotNotation, prop.ResolvedType),
                            _ => null,
                        };
                    }

                    // The ONE site that pairs "a case was emitted" with "the field is listed in the error
                    // message" — structurally, so a newly supported type can't silently fall out of the list.
                    if (caseText != null)
                    {
                        sb.AppendLine(caseText);
                        filterableFields.Add(prop.DTOPropName);
                    }
                }

                // Unknown filter/sort fields THROW (400) instead of silently no-opping — a silently
                // ignored filter returns UNFILTERED rows the caller treats as filtered, and a silently
                // ignored sort left the query unordered under the Id tie-breaker's isFirst: false
                // (the BACKEND-RS-1F 500). Hand-written overrides that consume pseudo filter keys must
                // Filters.Remove(...) them before delegating to the generated base.
                sb.AppendLine($$"""
                            default:
                                throw PaginationErrors.UnknownFilterField(filter.Key, "{{JoinForErrorMessage(filterableFields)}}");
                        }
                    }
                }
            }

            query = query.Where(predicate);

""");
                // Generate sorting — collections (CommaSeparated) are not sortable.
                var sortableProps = resolvedProps
                    .Where(p => p.IsCommaSeparated == false && p.ResolvedType.IsBaseDataType())
                    .ToList();

                string sortCases = string.Concat(sortableProps.Select(p => GetSortCase(p.DTOPropName, p.EntityDotNotation) + "\n"));

                // Emitted even when there are no sortable fields (a switch with only a default is valid C#),
                // so such an entity still rejects a client sort instead of ignoring it.
                sb.AppendLine($$"""
            if (filterDTO.MultiSortMeta?.Count > 0)
            {
                for (int i = 0; i < filterDTO.MultiSortMeta.Count; i++)
                {
                    bool ascending = filterDTO.MultiSortMeta[i].Order == 1;
                    switch (filterDTO.MultiSortMeta[i].Field)
                    {
{{sortCases}}                        default:
                            throw PaginationErrors.UnknownSortField(filterDTO.MultiSortMeta[i].Field, "{{JoinForErrorMessage(sortableProps.Select(p => p.DTOPropName))}}");
                    }
                }
            }

""");

                // Skip/Take on an unordered query returns rows in arbitrary heap/plan order (PostgreSQL),
                // so pages can repeat/drop rows. Always end with Id: as the whole ORDER BY when the client
                // sent no sort, as a ThenBy tie-breaker after user sorts on non-unique columns otherwise.
                // M2M junctions have no Id (no BusinessObject base) and keep the legacy unordered behavior.
                if (entity.IsManyToMany() == false)
                {
                    sb.AppendLine($$"""
            query = query.ApplySort(x => x.Id, ascending: false, isFirst: filterDTO.MultiSortMeta == null || filterDTO.MultiSortMeta.Count == 0);

""");
                }

                sb.AppendLine($$"""
            return new PaginatedResult<{{entity.Name}}>()
            {
                TotalRecords = await query.CountAsync(),
                Query = query
            };
        }

""");
            }
            sb.AppendLine($$"""
    }
}
""");
            foreach (string item in usings.Distinct())
            {
                sbUsings.AppendLine($$"""
using {{item}};
""");
            }

            sbUsings.AppendLine(sb.ToString());
            context.AddSpiderlyCSharpSource("PaginatedResultGenerator.generated", sbUsings.ToString(), nullableContext);
        }



        /// <summary>
        /// Resolves DTO properties to their entity dot-notation paths and types.
        /// Handles direct properties, CommaSeparated (M2M), and navigation properties via mapper lookup.
        /// </summary>
        private static List<(string DTOPropName, string EntityDotNotation, string ResolvedType, bool IsCommaSeparated)> ResolveDTOProperties(
            List<SpiderlyClass> pairDTOClasses,
            List<SpiderlyProperty> efClassProps,
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities)
        {
            List<(string, string, string, bool)> result = new();

            foreach (SpiderlyClass pairDTOClass in pairDTOClasses)
            {
                foreach (SpiderlyProperty DTOprop in pairDTOClass.Properties)
                {
                    string entityDotNotation = DTOprop.Name;
                    string DTOpropType = DTOprop.Type.Raw;

                    if (efClassProps.Any(x => x.Name == DTOprop.Name) == false)
                    {
                        if (entityDotNotation.EndsWith("CommaSeparated") && pairDTOClass.IsGenerated == true)
                        {
                            result.Add((DTOprop.Name, entityDotNotation, DTOpropType, true));
                            continue;
                        }

                        string? resolvedEntityDotNotation = GetDotNotatioOfEntityFromMappers(allEntities, entity, pairDTOClass, entityDotNotation);

                        if (resolvedEntityDotNotation == null)
                            continue;

                        entityDotNotation = resolvedEntityDotNotation;

                        DTOpropType = GetPropTypeOfEntityDotNotationProperty(entityDotNotation, entity, allEntities);
                    }

                    result.Add((DTOprop.Name, entityDotNotation, DTOpropType, false));
                }
            }

            return result;
        }

        /// <summary>
        /// Generates a sort <c>case</c> for the given DTO property, mapping it to its entity dot-notation path.
        /// <example>
        /// <code>
        /// case "roleDisplayName":
        ///     query = query.ApplySort(x => x.Role.Name, ascending, i == 0);
        ///     break;
        /// </code>
        /// </example>
        /// </summary>
        private static string GetSortCase(string DTOIdentifier, string entityDotNotation)
        {
            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                query = query.ApplySort(x => x.{{entityDotNotation.AsNullForgivingProjection()}}, ascending, i == 0);
                                break;
""";
        }

        /// <summary>
        /// The filter rule's value as a non-null string, for use inside an emitted predicate lambda.
        /// <para>
        /// The emitted code already guards <c>if (filterRuleDTO.Value != null)</c>, but every use sits inside a
        /// lambda that CAPTURES <c>filterRuleDTO</c>, and the compiler cannot carry a null check into a lambda
        /// whose invocation it cannot order — so it warns regardless of the guard. <c>object.ToString()</c> is
        /// itself <c>string?</c>, hence the second <c>!</c>. Why suppressing is safe rather than a papered-over
        /// null: see <see cref="Extensions.AsNullForgivingProjection"/>.
        /// </para>
        /// </summary>
        private const string FilterValueAsString = "filterRuleDTO.Value!.ToString()!";

        /// <summary>
        /// The emitted invalid-match-mode throw, shared by all five case emitters so the call shape
        /// can't drift between them. Wording and error code live in <c>PaginationErrors</c> (Spiderly.Shared).
        /// </summary>
        private const string InvalidMatchModeThrow = """throw PaginationErrors.InvalidMatchMode(filterRuleDTO.MatchMode, filter.Key);""";

        /// <summary>
        /// camelCases and joins field names for baking into an emitted <c>PaginationErrors</c> argument.
        /// </summary>
        private static string JoinForErrorMessage(IEnumerable<string> fieldNames) =>
            string.Join(", ", fieldNames.Select(f => f.FirstCharToLower()));


        private static string GetCaseForString(string DTOIdentifier, string entityDotNotation)
        {
            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filterRuleDTO.MatchMode)
                                {
                                    case MatchModeCodes.StartsWith:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingDereference()}}.ToLower().StartsWith({{FilterValueAsString}}.ToLower());
                                        break;
                                    case MatchModeCodes.Contains:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingDereference()}}.ToLower().Contains({{FilterValueAsString}}.ToLower());
                                        break;
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingDereference()}}.ToLower().Equals({{FilterValueAsString}}.ToLower());
                                        break;
                                    default:
                                        {{InvalidMatchModeThrow}}
                                }
                                predicate = predicate.And(condition);
                                break;
""";
        }

        private static string GetCaseForBool(string DTOIdentifier, string entityDotNotation)
        {
            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filterRuleDTO.MatchMode)
                                {
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingDereference()}}.Equals(Convert.ToBoolean({{FilterValueAsString}}));
                                        break;
                                    default:
                                        {{InvalidMatchModeThrow}}
                                }
                                predicate = predicate.And(condition);
                                break;
""";
        }

        private static string GetCaseForTemporal(string DTOIdentifier, string entityDotNotation, string parseExpr)
        {
            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filterRuleDTO.MatchMode)
                                {
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingProjection()}} == {{parseExpr}};
                                        break;
                                    case MatchModeCodes.LessThan:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingProjection()}} < {{parseExpr}};
                                        break;
                                    case MatchModeCodes.GreaterThan:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingProjection()}} > {{parseExpr}};
                                        break;
                                    default:
                                        {{InvalidMatchModeThrow}}
                                }
                                predicate = predicate.And(condition);
                                break;
""";
        }

        private static string GetCaseForNumber(string DTOIdentifier, string entityDotNotation, string numberType)
        {
            string numberTypeWithoutQuestion = numberType.WithoutNullableSuffix();

            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filterRuleDTO.MatchMode)
                                {
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingProjection()}} == {{numberTypeWithoutQuestion}}.Parse({{FilterValueAsString}});
                                        break;
                                    case MatchModeCodes.LessThan:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingProjection()}} < {{numberTypeWithoutQuestion}}.Parse({{FilterValueAsString}});
                                        break;
                                    case MatchModeCodes.GreaterThan:
                                        condition = x => x.{{entityDotNotation.AsNullForgivingProjection()}} > {{numberTypeWithoutQuestion}}.Parse({{FilterValueAsString}});
                                        break;
                                    case MatchModeCodes.In:
                                        {{numberType}}[] values = JsonSerializer.Deserialize<{{numberType}}[]>({{FilterValueAsString}}) ?? Array.Empty<{{numberType}}>();
                                        condition = x => values.Contains(x.{{entityDotNotation.AsNullForgivingProjection()}});
                                        break;
                                    default:
                                        {{InvalidMatchModeThrow}}
                                }
                                predicate = predicate.And(condition);
                                break;
""";
        }

        private static string GetCaseForEnumerable(string DTOIdentifier, string entityDotNotation, string idType)
        {
            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filterRuleDTO.MatchMode)
                                {
                                    case MatchModeCodes.In:
                                        {{idType}}[] values = JsonSerializer.Deserialize<{{idType}}[]>({{FilterValueAsString}}) ?? Array.Empty<{{idType}}>();
                                        condition = x => x.{{entityDotNotation.AsNullForgivingDereference()}}.Any(x => values.Contains(x.Id));
                                        break;
                                    default:
                                        {{InvalidMatchModeThrow}}
                                }
                                predicate = predicate.And(condition);
                                break;
""";
        }

        /// <summary>
        /// </summary>
        /// <param name="DTOClass">UserDTO</param>
        /// <param name="DTOClassProp">RoleDisplayName</param>
        /// <returns>Role.Id</returns>
        private static string? GetDotNotatioOfEntityFromMappers(List<SpiderlyClass> allClasses, SpiderlyClass entity, SpiderlyClass DTOClass, string DTOClassProp)
        {
            if (DTOClassProp.EndsWith("DisplayName") && DTOClass.IsGenerated == true) // FT: Doing this thing with the IsGenerated so we can make prop in non generated DTO with "DisplayName" or "Id" sufix 
            {
                string baseClassInDotNotation = DTOClassProp.Replace("DisplayName", ""); // "Rolinho"
                SpiderlyProperty propertyInEntityClass = entity.Properties.Where(x => x.Name == baseClassInDotNotation).Single();
                string typeOfThePropertyInEntityClass = propertyInEntityClass.Type.Name; // "Role"
                SpiderlyClass entityClassWhichWeAreSearchingDisplayNameFor = allClasses.Where(x => x.Name == typeOfThePropertyInEntityClass).Single();
                string displayName = ClassAnalyzer.GetDisplayNameProperty(entityClassWhichWeAreSearchingDisplayNameFor); // Name
                displayName = displayName.Replace(".ToString()", "");
                return $"{baseClassInDotNotation}.{displayName}"; // FT: It's okay to do it like this, because when we generating DisplayNames for DTO, we are doing it just for the first level.
            }
            if (DTOClassProp.EndsWith("Id") && DTOClassProp.Length > 2 && DTOClass.IsGenerated == true)
            {
                string baseClassInDotNotation = DTOClassProp.Replace("Id", ""); // "Rolinho"
                return $"{baseClassInDotNotation}.Id";
            }

            return null;
        }

        // NOTE: this walk was once blamed for the CS8785 on the Spiderly.Security build. It wasn't — that
        // was currentProjectEntities[0] on an empty list (Execute, now guarded); an array index would have
        // thrown IndexOutOfRangeException, not ArgumentOutOfRangeException with Parameter 'index'.
        //
        // The walk can still outrun its segments in theory: it descends until it reaches a base data type,
        // so a [DisplayName] marking a NAVIGATION property would ask for a segment the dot notation never
        // supplied. Unguarded on purpose — no shape in the suite produces it, and it now surfaces as
        // SPIDERLY024 naming this generator rather than as an opaque CS8785.
        public static string GetPropTypeOfEntityDotNotationProperty(string entityDotNotation, SpiderlyClass entityClass, List<SpiderlyClass> allClasses)
        {
            // Rolinho.Permission.Id
            string propName = entityDotNotation.Split('.')[0]; // Rolinho
            List<SpiderlyProperty> entityClassProperties = entityClass.Properties;
            SpiderlyProperty prop = entityClassProperties.Where(x => x.Name == propName).Single(); // Role

            int i = 1;
            while (prop.Type.IsBaseDataType() == false)
            {
                SpiderlyClass helperClass = allClasses.Where(x => x.Name == prop.Type.Name).Single(); // Role

                List<SpiderlyProperty> helperProps = helperClass.Properties;

                propName = entityDotNotation.Split('.')[i]; // Id
                prop = helperProps.Where(x => x.Name == propName).Single(); // Id
                i++;
            }

            // A 'string?' NRT annotation must not flow into emitted C# (CS8632 in an oblivious
            // consumer); Nullable<T> value types keep their '?'.
            return prop.Type.Name == "string" ? "string" : prop.Type.Raw;
        }
    }
}
