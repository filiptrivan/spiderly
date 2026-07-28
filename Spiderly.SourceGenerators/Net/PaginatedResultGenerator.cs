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

            var combinedWithEnums = combined.Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var (combinedSource, enumNames) = source;
                var ((classes, referencedClasses), config) = combinedSource;
                Execute(classes, referencedClasses, enumNames, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, ImmutableArray<string> spiderlyEnumNames, SpiderlyConfig config, SourceProductionContext context)
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

                foreach (var prop in resolvedProps)
                {
                    if (prop.IsCommaSeparated)
                    {
                        string entityPropName = prop.DTOPropName.Replace("CommaSeparated", ""); // "SegmentationItems"
                        SpiderlyProperty listProp = efClassProps.First(x => x.Name == entityPropName);
                        SpiderlyClass childEntity = Helpers.GetEntityByPropertyType(listProp, allEntities);
                        string childIdType = childEntity.GetIdType(allEntities);
                        sb.AppendLine(GetCaseForEnumerable(prop.DTOPropName, entityPropName, childIdType));
                        continue;
                    }

                    switch (prop.ResolvedType)
                    {
                        case "string":
                            sb.AppendLine(GetCaseForString(prop.DTOPropName, prop.EntityDotNotation));
                            break;
                        case "bool":
                        case "bool?":
                            sb.AppendLine(GetCaseForBool(prop.DTOPropName, prop.EntityDotNotation));
                            break;
                        case "DateTime":
                        case "DateTime?":
                            sb.AppendLine(GetCaseForTemporal(prop.DTOPropName, prop.EntityDotNotation, "DateTime", "Convert.ToDateTime(filterRuleDTO.Value.ToString())"));
                            break;
                        case "DateOnly":
                        case "DateOnly?":
                            sb.AppendLine(GetCaseForTemporal(prop.DTOPropName, prop.EntityDotNotation, "DateOnly", "DateOnly.Parse(filterRuleDTO.Value.ToString())"));
                            break;
                        case "TimeOnly":
                        case "TimeOnly?":
                            sb.AppendLine(GetCaseForTemporal(prop.DTOPropName, prop.EntityDotNotation, "TimeOnly", "TimeOnly.Parse(filterRuleDTO.Value.ToString())"));
                            break;
                        case "long":
                        case "long?":
                        case "int":
                        case "int?":
                        case "decimal":
                        case "decimal?":
                        case "float":
                        case "float?":
                        case "double":
                        case "double?":
                        case "byte":
                        case "byte?":
                            sb.AppendLine(GetCaseForNumber(prop.DTOPropName, prop.EntityDotNotation, prop.ResolvedType));
                            break;
                        default:
                            //sb.AppendLine(GetCaseForManyToOneFromMapping(prop, c, classes)); // FT: it's already done in other cases
                            break;
                    }
                }

                sb.AppendLine($$"""
                            default:
                                break;
                        }
                    }
                }
            }

            query = query.Where(predicate);

""");
                // Generate sorting
                StringBuilder sbSort = new();

                foreach (var prop in resolvedProps)
                {
                    // Collections (CommaSeparated) are not sortable
                    if (prop.IsCommaSeparated)
                        continue;

                    if (prop.ResolvedType.IsBaseDataType())
                        sbSort.AppendLine(GetSortCase(prop.DTOPropName, prop.EntityDotNotation));
                }

                if (sbSort.Length > 0)
                {
                    sb.AppendLine($$"""
            if (filterDTO.MultiSortMeta?.Count > 0)
            {
                for (int i = 0; i < filterDTO.MultiSortMeta.Count; i++)
                {
                    bool ascending = filterDTO.MultiSortMeta[i].Order == 1;
                    switch (filterDTO.MultiSortMeta[i].Field)
                    {
{{sbSort}}                        default:
                            break;
                    }
                }
            }

""");
                }

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
            context.AddSource("PaginatedResultGenerator.generated", SourceText.From(sbUsings.ToString(), Encoding.UTF8));
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

                        entityDotNotation = GetDotNotatioOfEntityFromMappers(allEntities, entity, pairDTOClass, entityDotNotation);

                        if (entityDotNotation == null)
                            continue;

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
                                query = query.ApplySort(x => x.{{entityDotNotation}}, ascending, i == 0);
                                break;
""";
        }

        private static string GetCaseForString(string DTOIdentifier, string entityDotNotation)
        {
            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filterRuleDTO.MatchMode)
                                {
                                    case MatchModeCodes.StartsWith:
                                        condition = x => x.{{entityDotNotation}}.ToLower().StartsWith(filterRuleDTO.Value.ToString().ToLower());
                                        break;
                                    case MatchModeCodes.Contains:
                                        condition = x => x.{{entityDotNotation}}.ToLower().Contains(filterRuleDTO.Value.ToString().ToLower());
                                        break;
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation}}.ToLower().Equals(filterRuleDTO.Value.ToString().ToLower());
                                        break;
                                    default:
                                        throw new ArgumentException("Invalid string match mode!");
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
                                        condition = x => x.{{entityDotNotation}}.Equals(Convert.ToBoolean(filterRuleDTO.Value.ToString()));
                                        break;
                                    default:
                                        throw new ArgumentException("Invalid bool match mode!");
                                }
                                predicate = predicate.And(condition);
                                break;
""";
        }

        private static string GetCaseForTemporal(string DTOIdentifier, string entityDotNotation, string typeLabel, string parseExpr)
        {
            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filterRuleDTO.MatchMode)
                                {
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation}} == {{parseExpr}};
                                        break;
                                    case MatchModeCodes.LessThan:
                                        condition = x => x.{{entityDotNotation}} < {{parseExpr}};
                                        break;
                                    case MatchModeCodes.GreaterThan:
                                        condition = x => x.{{entityDotNotation}} > {{parseExpr}};
                                        break;
                                    default:
                                        throw new ArgumentException("Invalid {{typeLabel}} match mode!");
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
                                        condition = x => x.{{entityDotNotation}} == {{numberTypeWithoutQuestion}}.Parse(filterRuleDTO.Value.ToString());
                                        break;
                                    case MatchModeCodes.LessThan:
                                        condition = x => x.{{entityDotNotation}} < {{numberTypeWithoutQuestion}}.Parse(filterRuleDTO.Value.ToString());
                                        break;
                                    case MatchModeCodes.GreaterThan:
                                        condition = x => x.{{entityDotNotation}} > {{numberTypeWithoutQuestion}}.Parse(filterRuleDTO.Value.ToString());
                                        break;
                                    case MatchModeCodes.In:
                                        {{numberType}}[] values = JsonSerializer.Deserialize<{{numberType}}[]>(filterRuleDTO.Value.ToString());
                                        condition = x => values.Contains(x.{{entityDotNotation}});
                                        break;
                                    default:
                                        throw new ArgumentException("Invalid numeric match mode!");
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
                                        {{idType}}[] values = JsonSerializer.Deserialize<{{idType}}[]>(filterRuleDTO.Value.ToString());
                                        condition = x => x.{{entityDotNotation}}.Any(x => values.Contains(x.Id));
                                        break;
                                    default:
                                        throw new ArgumentException("Invalid Enumerable match mode!");
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
        private static string GetDotNotatioOfEntityFromMappers(List<SpiderlyClass> allClasses, SpiderlyClass entity, SpiderlyClass DTOClass, string DTOClassProp)
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
