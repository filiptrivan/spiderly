using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// **Summary:**
    /// Generates FluentValidation validator classes (`{YourProjectName}ValidationRules.generated.cs`)
    /// within the `{YourBaseNamespace}.ValidationRules` namespace. These validators are
    /// automatically created based on the validation attributes defined on your DTO properties.
    ///
    /// **Key Features:**
    /// - **Automatic Validator Generation:** For each DTO class in your project (within the '.DTO' namespace) that corresponds to an entity (in the '.Entities' namespace),
    ///   it generates a FluentValidation validator class.
    /// - **Validation Rule Mapping:** Translates standard .NET validation attributes (e.g., `[Required]`, `[StringLength]`, `[EmailAddress]`, `[Range]`, `[RegularExpression]`, `[PrecisionScale]`, `[NotHaveWhiteSpace]`)
    ///   into their FluentValidation equivalents (e.g. `NotNull()`, `NotEmpty()`, `Length()`, `EmailAddress()`, `InclusiveBetween()`, `Matches()`, `ScalePrecision()`, `MustNotWhiteSpace()`).
    /// - **Extensibility:** Allowing you to easily add custom validation rules as needed by CustomValidator attribute (e.g. CustomValidator("...")).
    /// - **Namespace Alignment:** Places the generated validator classes in a `.ValidationRules` sub-namespace, aligning with the `.DTO` namespace structure.
    /// - **Handles Partial DTOs:** Correctly processes DTO classes that are split across multiple partial files.
    ///
    /// **How to Use:**
    /// 1. Decorate your DTO properties with standard .NET validation attributes.
    /// 2. Ensure that for each DTO you want validation rules generated, there is a corresponding entity class (with the same name, minus the "DTO" suffix) in your '.Entities' namespace.
    /// 3. Build your .NET project. This source generator will automatically run during the build process.
    /// 4. The generated validator classes will be created in a `.ValidationRules` sub-namespace.
    /// 5. In your services or wherever you need to perform validation, instantiate these generated validator classes and use them to validate your DTO instances.
    ///
    /// **Generated Output:**
    /// - `{YourProjectName}ValidationRules.generated.cs`: Contains FluentValidation validator classes (e.g., `UserDTOValidationRules`) for each of your DTOs. Each validator class inherits from `AbstractValidator<{YourDTO}>` and defines validation rules in its constructor using the `RuleFor` method and FluentValidation's validation methods (e.g., `NotNull()`, `Length()`).
    /// - The namespace will be `{YourBaseNamespace}.ValidationRules`.
    ///
    /// **Note:** This generator provides a convenient way to automatically create basic FluentValidation rules based on your existing .NET validation attributes. For more complex validation scenarios or custom validation logic, add CustomValidator attribute (e.g. CustomValidator("...")).
    /// </summary>
    [Generator]
    public class FluentValidationGenerator : IIncrementalGenerator
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
            if (classes.Count <= 1) return;

            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = Helpers.GetDTOClasses(currentProjectClasses, allClasses);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);
            string projectName = Helpers.GetProjectName(namespaceValue);

            string result = $$"""
using FluentValidation;
using {{basePartOfNamespace}}.DTO;
using Spiderly.Shared.FluentValidation;

namespace {{basePartOfNamespace}}.ValidationRules
{
{{string.Join("\n", GetDTOClassesValidationRules(currentProjectDTOClasses, currentProjectEntities))}}
}
""";

            context.AddSource($"{projectName}ValidationRules.generated", SourceText.From(result, Encoding.UTF8));
        }

        private static List<string> GetDTOClassesValidationRules(List<SpiderlyClass> currentProjectDTOClasses, List<SpiderlyClass> currentProjectEntities)
        {
            List<string> result = new();

            foreach (IGrouping<string, SpiderlyClass> DTOClassGroup in currentProjectDTOClasses.GroupBy(x => x.Name)) // Grouping because UserDTO.generated and UserDTO
            {
                List<SpiderlyProperty> DTOProperties = new();
                List<SpiderlyAttribute> DTOAttributes = new();

                foreach (SpiderlyClass DTOClass in DTOClassGroup)
                {
                    DTOProperties.AddRange(DTOClass.Properties);
                    DTOAttributes.AddRange(DTOClass.Attributes);
                }

                SpiderlyClass entity = currentProjectEntities.Where(x => DTOClassGroup.Key.Replace("DTO", "") == x.Name).SingleOrDefault(); // If it is null then we only made DTO, without entity class

                List<SpiderValidationRule> rules = Helpers.GetValidationRules(DTOProperties, DTOAttributes, entity);

                result.Add($$"""
    public class {{DTOClassGroup.Key}}ValidationRules : AbstractValidator<{{DTOClassGroup.Key}}>
    {
        public {{DTOClassGroup.Key}}ValidationRules()
        {
{{GetDTOValidationRules(rules)}}
        }
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
                result.Add($$"""
            RuleFor(x => x.{{rule.Property.Name}}){{string.Join("", rule.ValidationRuleParts.Select(x => $".{x.Name}({x.MethodParametersBody})"))}};
""");
            }

            return string.Join("\n", result);
        }
    }
}