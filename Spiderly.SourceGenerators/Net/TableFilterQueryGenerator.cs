﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Generates the `TableFilterQueryable` static class (`TableFilterQueryable.generated.cs`)
    /// within the `{YourBaseNamespace}.TableFiltering` namespace. This class provides a method
    /// `Build` that dynamically constructs an EF Core query with filtering based on the
    /// `TableFilterDTO` payload. It intelligently handles filtering on properties that might
    /// exist in the DTO but not directly in the entity, by looking up mapping configurations.
    /// </summary>
    [Generator]
    public class TableFilterQueryGenerator : IIncrementalGenerator
    {

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = Helpers.GetClassIncrementalValuesProvider(context.SyntaxProvider, new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.DTO,
                    NamespaceExtensionCodes.DataMappers,
                });

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.DTO,
                });

            var allClasses = classDeclarations.Collect()
                .Combine(referencedProjectClasses);

            context.RegisterImplementationSourceOutput(allClasses, static (spc, source) => Execute(source.Left, source.Right, spc));
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, SourceProductionContext context)
        {
            if (classes.Count <= 1) 
                return;

            List<SpiderlyClass> spiderClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> allClasses = spiderClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = Helpers.GetDTOClasses(spiderClasses, allClasses);
            List<SpiderlyClass> currentProjectEntities = spiderClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> allEntityClasses = allClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();

            StringBuilder sb = new();
            List<string> usings = new();
            StringBuilder sbUsings = new();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);
            string projectName = Helpers.GetProjectName(namespaceValue);

            sb.AppendLine($$"""
using LinqKit;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Enums;
using System.Text.Json;
using {{basePartOfNamespace}}.Entities;

namespace {{basePartOfNamespace}}.TableFiltering
{
    public static class TableFilterQueryable
    {
""");
            foreach (SpiderlyClass entity in currentProjectEntities)
            {
                string baseType = entity.BaseType;

                if (baseType == null)
                    continue;

                sb.AppendLine($$"""
        public static async Task<PaginationResult<{{entity.Name}}>> Build(IQueryable<{{entity.Name}}> query, TableFilterDTO tableFilterPayload)
        {
            Expression<Func<{{entity.Name}}, bool>> predicate = PredicateBuilder.New<{{entity.Name}}>(true);

            foreach (KeyValuePair<string, List<TableFilterContext>> item in tableFilterPayload.Filters)
            {
                foreach (TableFilterContext filter in item.Value)
                {
                    if (filter.Value != null)
                    {
                        Expression<Func<{{entity.Name}}, bool>> condition;

                        switch (item.Key)
                        {
""");
                // I go through all the DTO properties, and if I come across one that doesn't exist in the EF class, I look for a solution in the mappers; if it doesn't exist there either, I log an appropriate error.
                List<SpiderlyClass> pairDTOClasses = currentProjectDTOClasses.Where(x => x.Name == $"{entity.Name}DTO").ToList(); // Getting the pair DTO classes of entity class
                List<SpiderlyProperty> efClassProps = entity.Properties;

                foreach (SpiderlyClass pairDTOClass in pairDTOClasses)
                {
                    foreach (SpiderlyProperty DTOprop in pairDTOClass.Properties)
                    {
                        string entityDotNotation = DTOprop.Name; // RoleDisplayName
                        string DTOpropType = DTOprop.Type;

                        if (efClassProps.Where(x => x.Name == DTOprop.Name).Any() == false) // If a property in the DTO doesn't exist in the EF class (e.g., RoleDisplayName doesn't exist).
                        {
                            if (entityDotNotation.EndsWith("CommaSeparated") && pairDTOClass.IsGenerated == true)
                            {
                                string entityPropName = entityDotNotation.Replace("CommaSeparated", ""); // "SegmentationItems"

                                sb.AppendLine(GetCaseForEnumerable(DTOprop.Name, entityPropName, entity.GetIdType(currentProjectEntities)));

                                continue;
                            }
                            else
                            {
                                entityDotNotation = GetDotNotatioOfEntityFromMappers(allEntityClasses, entity, pairDTOClass, entityDotNotation); // "Role.Id"

                                if (entityDotNotation == null)
                                    continue;

                                DTOpropType = GetPropTypeOfEntityDotNotationProperty(entityDotNotation, entity, allEntityClasses);
                            }
                        }

                        switch (DTOpropType)
                        {
                            case "string":
                                sb.AppendLine(GetCaseForString(DTOprop.Name, entityDotNotation));
                                break;
                            case "bool":
                            case "bool?":
                                sb.AppendLine(GetCaseForBool(DTOprop.Name, entityDotNotation));
                                break;
                            case "DateTime":
                            case "DateTime?":
                                sb.AppendLine(GetCaseForDateTime(DTOprop.Name, entityDotNotation));
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
                                sb.AppendLine(GetCaseForNumber(DTOprop.Name, entityDotNotation, DTOpropType));
                                break;
                            default:
                                //sb.AppendLine(GetCaseForManyToOneFromMapping(prop, c, classes)); // FT: it's already done in other cases
                                break;
                        }



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

            return new PaginationResult<{{entity.Name}}>()
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
            context.AddSource("TableFilterQueryable.generated", SourceText.From(sbUsings.ToString(), Encoding.UTF8));
        }



        private static string GetCaseForString(string DTOIdentifier, string entityDotNotation)
        {
            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filter.MatchMode)
                                {
                                    case MatchModeCodes.StartsWith:
                                        condition = x => x.{{entityDotNotation}}.StartsWith(filter.Value.ToString());
                                        break;
                                    case MatchModeCodes.Contains:
                                        condition = x => x.{{entityDotNotation}}.Contains(filter.Value.ToString());
                                        break;
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation}}.Equals(filter.Value.ToString());
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
                                switch (filter.MatchMode)
                                {
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation}}.Equals(Convert.ToBoolean(filter.Value.ToString()));
                                        break;
                                    default:
                                        throw new ArgumentException("Invalid bool match mode!");
                                }
                                predicate = predicate.And(condition);
                                break;
""";
        }

        private static string GetCaseForDateTime(string DTOIdentifier, string entityDotNotation)
        {
            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filter.MatchMode)
                                {
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation}} == Convert.ToDateTime(filter.Value.ToString());
                                        break;
                                    case MatchModeCodes.LessThan:
                                        condition = x => x.{{entityDotNotation}} < Convert.ToDateTime(filter.Value.ToString());
                                        break;
                                    case MatchModeCodes.GreaterThan:
                                        condition = x => x.{{entityDotNotation}} > Convert.ToDateTime(filter.Value.ToString());
                                        break;
                                    default:
                                        throw new ArgumentException("Invalid DateTime match mode!");
                                }
                                predicate = predicate.And(condition);
                                break;
""";
        }

        private static string GetCaseForNumber(string DTOIdentifier, string entityDotNotation, string numberType)
        {
            string numberTypeWithoutQuestion = numberType.Replace("?", "");

            return $$"""
                            case "{{DTOIdentifier.FirstCharToLower()}}":
                                switch (filter.MatchMode)
                                {
                                    case MatchModeCodes.Equals:
                                        condition = x => x.{{entityDotNotation}} == {{numberTypeWithoutQuestion}}.Parse(filter.Value.ToString());
                                        break;
                                    case MatchModeCodes.LessThan:
                                        condition = x => x.{{entityDotNotation}} < {{numberTypeWithoutQuestion}}.Parse(filter.Value.ToString());
                                        break;
                                    case MatchModeCodes.GreaterThan:
                                        condition = x => x.{{entityDotNotation}} > {{numberTypeWithoutQuestion}}.Parse(filter.Value.ToString());
                                        break;
                                    case MatchModeCodes.In:
                                        {{numberType}}[] values = JsonSerializer.Deserialize<{{numberType}}[]>(filter.Value.ToString());
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
                                switch (filter.MatchMode)
                                {
                                    case MatchModeCodes.In:
                                        {{idType}}[] values = JsonSerializer.Deserialize<{{idType}}[]>(filter.Value.ToString());
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
                string typeOfThePropertyInEntityClass = propertyInEntityClass.Type; // "Role"
                SpiderlyClass entityClassWhichWeAreSearchingDisplayNameFor = allClasses.Where(x => x.Name == typeOfThePropertyInEntityClass).Single();
                string displayName = Helpers.GetDisplayNameProperty(entityClassWhichWeAreSearchingDisplayNameFor); // Name
                displayName = displayName.Replace(".ToString()", "");
                return $"{baseClassInDotNotation}.{displayName}"; // FT: It's okay to do it like this, because when we generating DisplayNames for DTO, we are doing it just for the first level.
            }
            if (DTOClassProp.EndsWith("Id") && DTOClassProp.Length > 2 && DTOClass.IsGenerated == true)
            {
                string baseClassInDotNotation = DTOClassProp.Replace("Id", ""); // "Rolinho"
                return $"{baseClassInDotNotation}.Id";
            }

            foreach (SpiderlyAttribute attribute in entity.Attributes.Where(x => x.Name == "ProjectToDTO"))
            {
                // ".Map(dest => dest.TransactionPrice, src => src.Transaction.Price)"
                string wordAfterDest = attribute.Value.Split("dest.")[1].Split(",")[0]; // TransactionPrice

                if (wordAfterDest == DTOClassProp)
                    return attribute.Value.Split("src.")[1].Split(")")[0]; // Transaction.Price
            }

            return null;

            // TODO FT: Delete, complicating for custom class mapper
            //SpiderClass nonGeneratedMapperClass = allClasses.Where(x => x.Namespace.EndsWith(".DataMappers")).SingleOrDefault(); // FT: Can be null if the user still didn't made DataMappers partial class

            //List<SpiderMethod> methodsOfTheNonGeneratedMapperClass = nonGeneratedMapperClass?.Methods; // FT: Classes from referenced assemblies won't have method body, but here it's not important.

            //return GetEntityDotNotationForDTO(methodsOfTheNonGeneratedMapperClass, DTOClass.Name, entity.Name, DTOClassProp);
        }

        /// <summary>
        /// </summary>
        /// <param name="mapMethod">public static partial UserDTO Map(User poco);</param>
        /// <param name="DTOClassProp">RoleDisplayName</param>
        /// <returns></returns>
        public static string GetFirstAttributeParamFromMapper(MethodDeclarationSyntax mapMethod, string DTOClassProp)
        {
            foreach (var attributeList in mapMethod.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments; // "Role.Id", "RoleDisplayName"
                    if (arguments.Count > 1) // Doing this because of MapperIgnoreTarget
                    {
                        if (arguments[1].ToString().Split('.').First().Trim('"') == DTOClassProp)
                        {
                            return arguments[0].ToString().Trim('"');
                        }
                    }
                }
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
                SpiderlyClass helperClass = allClasses.Where(x => x.Name == prop.Type).Single(); // Role

                List<SpiderlyProperty> helperProps = helperClass.Properties;

                propName = entityDotNotation.Split('.')[i]; // Id
                prop = helperProps.Where(x => x.Name == propName).Single(); // Id
                i++;
            }

            return prop.Type;
        }

        public static string GetEntityDotNotationForDTO(List<SpiderMethod> methodsOfTheNonGeneratedMapperClass, string destinationDTOClass, string sourceEntityClass, string DTOProp)
        {
            if (methodsOfTheNonGeneratedMapperClass == null)
                return null;

            List<SpiderMethod> methodsWithTableFiltersAttribute = methodsOfTheNonGeneratedMapperClass.Where(x => x.Attributes.Any(x => x.Name == "TableFiltersListener")).ToList();

            SpiderMethod currentConfigMethod = methodsWithTableFiltersAttribute.Where(x => x.Body.Contains($".NewConfig<{sourceEntityClass}, {destinationDTOClass}>()")).SingleOrDefault();

            if (currentConfigMethod == null)
                return null;

            IEnumerable<InvocationExpressionSyntax> newConfigCalls = currentConfigMethod.DescendantNodes
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression.ToString().Contains("NewConfig"));

            foreach (InvocationExpressionSyntax invocation in newConfigCalls)
            {
                List<string> linqRows = invocation.ArgumentList?.Arguments.Select(x => x.ToString()).ToList();

                if (linqRows.Where(x => x.Split('.').LastOrDefault() == DTOProp).Count() == 1) // dest.TestDisplayName -> TestDisplayName
                {
                    string src = linqRows.Where(x => x.Split('.').LastOrDefault() != DTOProp).Single(); // src => src.Gender.Name
                    List<string> parts = src.Split('.').ToList(); // src => src ; Gender ; Name
                    List<string> partsToJoin = parts.GetRange(1, parts.Count - 1).ToList(); // Gender ; Name
                    return string.Join(".", partsToJoin);
                }
            }

            return null;
        }



    }
}
