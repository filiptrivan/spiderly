using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Enums;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// **Summary:**
    /// Generates Angular entity and DTO classes (`{your-app-name}\Angular\src\app\business\entities\{your-app-name}-entities.generated.ts`)
    /// based on corresponding C# classes within the '.Entities' and '.DTO' namespaces.
    /// This generator simplifies frontend data modeling by automatically creating TypeScript interfaces/classes
    /// that mirror your backend data structures.
    ///
    /// **Key Features:**
    /// - **Automatic Type Generation:** Creates Angular classes for each C# entity and DTO.
    /// - **Inheritance from BaseEntity:** Generated classes extend a `BaseEntity` class from the `spiderly` library, providing common entity properties and functionality.
    /// - **Property Mapping:** Maps C# properties to their equivalent TypeScript types using `Helpers.GetAngularType()`.
    /// - **Constructor Generation:** Includes a constructor that accepts an optional object literal for easy instantiation, with all properties being optional (`?:`).
    /// - **Import Handling:** Automatically imports necessary types, including `BaseEntity`, `TableFilter`, enums (from a generated enums file), and other related entity/DTO classes.
    /// - **Handles Duplicate Class Names:** Groups properties from potentially duplicate C# class names (e.g., `UserDTO` and `UserDTO.generated`) into a single Angular class.
    ///
    /// **How to Use:**
    /// 1. Ensure your C# entity and DTO classes are located in namespaces ending with `.Entities` or `.DTO`.
    /// 2. Build your .NET project. This source generator will automatically run during the build process.
    /// 3. In your Angular application, import the generated entity/DTO classes from the generated `entities.generated.ts` file.
    /// 4. Use these classes for type-safe data handling within your Angular components and services.
    ///
    /// **Generated Output:**
    /// - A TypeScript file (`{your-app-name}-entities.generated.ts`) containing:
    ///     - Import statements for `BaseEntity` and other necessary types.
    ///     - TypeScript classes (extending `BaseEntity`) representing your C# entities and DTOs.
    ///     - Properties within these classes with their corresponding Angular data types (all properties are optional).
    ///     - Constructors for easy object creation.
    ///     - Imports for any referenced enum types from a related `enums.generated.ts` file.
    ///
    /// **Note:** This generator relies on the `BaseEntity` class from the `spiderly` Angular library. Ensure this library is installed in your Angular project. It also assumes that a separate source generator (`NgEnumsGenerator`) is responsible for creating the `enums.generated.ts` file if your DTOs or entities use enums.
    /// </summary>
    [Generator]
    public class NgEntitiesGenerator : IIncrementalGenerator
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

            IncrementalValueProvider<string> callingProjectDirectory = context.GetCallingPath();

            var combined = classDeclarations.Collect()
                .Combine(referencedProjectClasses)
                .Combine(callingProjectDirectory);

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (classesAndEntities, callingPath) = source;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, callingPath, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, string callingProjectDirectory, SourceProductionContext context)
        {
            if (classes.Count <= 1)
                return; // FT: one because of config settings

            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = Helpers.GetDTOClasses(currentProjectClasses, allClasses);

            string namespaceValue = currentProjectClasses[0].Namespace;
            string projectName = Helpers.GetProjectName(namespaceValue);

            // ...\API\PlayertyLoyals.Business -> ...\Angular\src\app\business\entities\{projectName}-entities.ts
            string outputPath = callingProjectDirectory.ReplaceEverythingAfter(@"\API\", $@"\Angular\src\app\business\entities\{projectName.FromPascalToKebabCase()}-entities.generated.ts");

            StringBuilder sb = new();
            StringBuilder sbImports = new();
            sbImports.Append($$"""
import { BaseEntity, TableFilter, TableFilterContext, TableFilterSortMeta, MimeTypes, Namebook } from 'spiderly';
{{string.Join("\n", GetEnumPropertyImports(currentProjectDTOClasses, projectName))}}

""");

            foreach (IGrouping<string, SpiderlyClass> DTOClassGroup in currentProjectDTOClasses.GroupBy(x => x.Name)) // Grouping because UserDTO.generated and UserDTO
            {
                List<SpiderlyProperty> DTOProperties = new();

                foreach (SpiderlyClass DTOClass in DTOClassGroup) // It can only be 2 here
                    DTOProperties.AddRange(DTOClass.Properties);

                List<string> angularPropertyDefinitions = GetAllAngularPropertyDefinitions(DTOProperties); // FT: If, in some moment, we want to make another aproach set this to false, now it doesn't matter
                string angularClassIdentifier = DTOClassGroup.Key.Replace("DTO", "");

                sbImports.Append(string.Join("\n", Helpers.GetAngularImports(DTOProperties, projectName)));

                sb.AppendLine($$"""


export class {{angularClassIdentifier}} extends BaseEntity
{
    {{string.Join("\n\t", angularPropertyDefinitions)}}

    constructor(
    {
        {{string.Join(",\n\t\t", DTOProperties.Select(x => x.Name.FirstCharToLower()))}}
    }:{
        {{string.Join("\n\t\t", angularPropertyDefinitions)}}     
    } = {}
    ) {
        super('{{angularClassIdentifier}}'); 

        {{string.Join("\n\t\t", GetAngularPropertyAssignments(DTOProperties))}}
    }
}
""");

            }

            sbImports.Append(sb);

            Helpers.WriteToTheFile(sbImports.ToString(), outputPath);
        }

        private static List<string> GetAllAngularPropertyDefinitions(List<SpiderlyProperty> DTOProperties)
        {
            List<string> result = new();

            foreach (SpiderlyProperty DTOProp in DTOProperties)
            {
                string DTOPropLowerCase = DTOProp.Name.FirstCharToLower();

                string angularDataType = Helpers.GetAngularType(DTOProp.Type);
                result.Add($"{DTOPropLowerCase}?: {angularDataType};");
            }

            return result;
        }

        private static List<string> GetAngularPropertyAssignments(List<SpiderlyProperty> DTOProperties)
        {
            List<string> result = new();

            foreach (SpiderlyProperty DTOProp in DTOProperties)
            {
                string DTOPropLowerCase = DTOProp.Name.FirstCharToLower();
                result.Add($"this.{DTOPropLowerCase} = {DTOPropLowerCase};");
            }

            return result;
        }

        private static List<string> GetEnumPropertyImports(List<SpiderlyClass> DTOClasses, string projectName)
        {
            List<string> result = new();

            foreach (IGrouping<string, SpiderlyClass> DTOClassGroup in DTOClasses.GroupBy(x => x.Name)) // Grouping because UserDTO.generated and UserDTO
            {
                List<SpiderlyProperty> DTOProperties = new List<SpiderlyProperty>();

                foreach (SpiderlyClass DTOClass in DTOClassGroup) // It can only be 2 here
                    DTOProperties.AddRange(DTOClass.Properties);

                foreach (SpiderlyProperty property in DTOProperties.Where(x => x.Type.IsEnum()))
                {
                    if (result.Contains(property.Name) == false)
                    {
                        result.Add($$"""
import { {{property.Type}} } from "../enums/{{projectName.FromPascalToKebabCase()}}-enums.generated";
""");
                    }
                }
            }

            return result;
        }


    }
}
