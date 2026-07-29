using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Generates FluentValidation validator classes (`{YourAppName}ValidationRules.generated.cs`)
    /// within the `{YourBaseNamespace}.ValidationRules` namespace. These validators are
    /// automatically created based on the validation attributes defined on your DTO properties.
    /// </summary>
    [Generator]
    public class FluentValidationGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // #if DEBUG
            //             if (!Debugger.IsAttached)
            //             {
            //                 Debugger.Launch();
            //             }
            // #endif
            var combined = PipelineFactory.CreatePipeline(context,
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO },
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

            if (!config.IsGeneratorEnabled(nameof(FluentValidationGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses, spiderlyEnumNames);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = SpiderlyClassFactory.GetDTOClasses(currentProjectClasses, allClasses);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();

            string namespaceValue = currentProjectEntities.Count > 0
                ? currentProjectEntities[0].Namespace
                : currentProjectDTOClasses[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            string result = $$"""
using FluentValidation;
{{string.Join("\n", ReferencedAssemblyAnalyzer.GetClassesUsings(currentProjectDTOClasses))}}
using Spiderly.Shared.FluentValidation;

namespace {{basePartOfNamespace}}.ValidationRules
{
{{string.Join("\n", GetDTOClassesValidationRules(currentProjectDTOClasses, currentProjectEntities))}}
}
""";

            context.AddSpiderlyCSharpSource("ValidationRules.generated", result, nullableContext);
        }

        private static List<string> GetDTOClassesValidationRules(List<SpiderlyClass> currentProjectDTOClasses, List<SpiderlyClass> currentProjectEntities)
        {
            List<string> result = new();

            foreach (IGrouping<string, SpiderlyClass> DTOClassGroup in currentProjectDTOClasses.GroupBy(x => x.Name)) // Grouping because UserDTO.generated and UserDTO
            {
                List<SpiderlyProperty> DTOProperties = new();

                foreach (SpiderlyClass DTOClass in DTOClassGroup)
                {
                    DTOProperties.AddRange(DTOClass.Properties);
                }

                SpiderlyClass entity = currentProjectEntities.SingleOrDefault(x =>
                    SpiderlyNaming.IsGeneratedDTOName(DTOClassGroup.Key, x.Name, "DTO", "SaveBodyDTO")
                ); // If it is null then we only made DTO, without entity class

                List<SpiderValidationRule> rules = ValidationRuleBuilder.GetValidationRules(DTOProperties, entity);

                result.Add($$"""
    public partial class {{DTOClassGroup.Key}}ValidationRules : AbstractValidator<{{DTOClassGroup.Key}}>
    {
        public {{DTOClassGroup.Key}}ValidationRules()
        {
{{GetDTOValidationRules(rules)}}
            ConfigureCustomRules();
        }

        partial void ConfigureCustomRules();
    }
""");
            }

            return result;
        }

        private static string GetDTOValidationRules(List<SpiderValidationRule> rules)
        {
            List<string> result = new();

            foreach (SpiderValidationRule rule in rules)
            {
                string ruleChain = string.Join("", rule.ValidationRuleParts.Select(x => ToFluentValidationCall(x, rule.Property.Type.Raw)));
                result.Add($$"""
            RuleFor(x => x.{{rule.Property.Name}}){{ruleChain}};
""");
            }

            return string.Join("\n", result);
        }

        private static string ToFluentValidationCall(SpiderValidationRulePart rulePart, string propertyType) => rulePart switch
        {
            NotEmptyRulePart => ".NotEmpty()",
            MaximumLengthRulePart p => $".MaximumLength({p.MaxLength})",
            LengthRangePart p => $".Length({p.Min}, {p.Max})",
            ExactLengthRulePart p => $".Length({p.Length})",
            PrecisionScaleRulePart p => $".PrecisionScale({p.Precision}, {p.Scale}, false)",
            GreaterThanOrEqualToRulePart p => $".GreaterThanOrEqualTo({GetNumericLiteralWithSuffix(p.Value, propertyType)})",
            LessThanOrEqualToRulePart p => $".LessThanOrEqualTo({GetNumericLiteralWithSuffix(p.Value, propertyType)})",
            EmailAddressRulePart => ".EmailAddress()",
            UnlessRulePart p => $".Unless({p.Condition})",
            NotHaveWhiteSpaceRulePart => ".NotHaveWhiteSpace()",
            _ => throw new NotSupportedException($"Unknown validation rule part: {rulePart.GetType().Name}")
        };

        private static string GetNumericLiteralWithSuffix(string value, string propertyType)
        {
            if (propertyType == "decimal" || propertyType == "decimal?")
                return $"{value}m";

            if (propertyType == "float" || propertyType == "float?")
                return $"{value}f";

            return value;
        }
    }
}
