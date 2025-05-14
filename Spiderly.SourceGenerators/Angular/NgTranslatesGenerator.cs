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
using CodegenCS;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// **Summary:**
    /// Generates Angular translation services (`class-names.generated.ts` and `labels.generated.ts`)
    /// within the `{your-app-path}\Angular\src\app\business\services\translates` directory.
    /// These services leverage Transloco to provide basic translation capabilities for class names and DTO property labels
    /// based on your backend DTO classes.
    ///
    /// **Key Features:**
    /// - **Automated Translation Key Generation:** Scans your C# DTO classes and generates `translate` methods in Angular services.
    /// - **Class Name Translation:** Creates `TranslateClassNamesGeneratedService` which provides a `translate(className: string)` method. This method attempts to translate a given class name (without the "DTO" suffix) using Transloco's `translate` function with the class name as the key.
    /// - **Property Label Translation:** Creates `TranslateLabelsGeneratedService` with a `translate(propertyName: string)` method. This method tries to translate a given DTO property name (with some basic heuristics to remove "Id", "DisplayName", "CommaSeparated" suffixes) using Transloco with the processed property name as the key.
    /// - **Transloco Integration:** Assumes you are using `@jsverse/transloco` for internationalization in your Angular project.
    /// - **Simple Key Mapping:** Uses the class name or property name (after basic processing) directly as the translation key. You'll need to ensure corresponding entries exist in your Transloco translation files.
    ///
    /// **How to Use:**
    /// 1. Ensure you have `@jsverse/transloco` installed and configured in your Angular project.
    /// 2. Build your .NET project. This source generator will automatically run during the build process.
    /// 3. In your Angular components or services, inject either `TranslateClassNamesGeneratedService` or `TranslateLabelsGeneratedService`.
    /// 4. Call the `translate()` method, passing the class name (e.g., `user`) or property name (e.g., `firstName`) as a string.
    /// 5. Ensure you have corresponding translation keys (e.g., `"User": "Utilisateur"`, `"FirstName": "Prénom"`) defined in your Transloco language files (e.g., `en.json`, `fr.json`).
    ///
    /// **Generated Output:**
    /// - `class-names.generated.ts`: An Angular service (`TranslateClassNamesGeneratedService`) with a `translate` method for class names.
    /// - `labels.generated.ts`: An Angular service (`TranslateLabelsGeneratedService`) with a `translate` method for DTO property labels.
    /// - Both services are `@Injectable({ providedIn: 'root' })` for easy dependency injection.
    /// - The `translate` methods use a `switch` statement to map the input string to a `this.translocoService.translate()` call.
    /// </summary>
    [Generator]
    public class NgTranslatesGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
//#if DEBUG
//            if (!Debugger.IsAttached)
//            {
//                Debugger.Launch();
//            }
//#endif
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => Helpers.IsSyntaxTargetForGenerationEveryClass(s),
                    transform: static (ctx, _) => Helpers.GetSemanticTargetForGenerationEveryClass(ctx))
                .Where(static c => c is not null);

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.DTO
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

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedClassesDTO, string callingProjectDirectory, SourceProductionContext context)
        {
            if (classes.Count <= 1) 
                return;

            if (callingProjectDirectory.Contains(".WebAPI") == false)
                return;

            // ...\API\PlayertyLoyals.Business -> ...\Angular\src\app\business\services\translates
            string outputPath = callingProjectDirectory.ReplaceEverythingAfter(@"\API\", @"\Angular\src\app\business\services\translates");

            StringBuilder sbClassNames = new();
            StringBuilder sbLabels = new();
            List<SpiderlyProperty> DTOProperties = new List<SpiderlyProperty>();

            referencedClassesDTO = referencedClassesDTO.OrderBy(x => x.Name).ToList();

            foreach (SpiderlyClass DTOClass in referencedClassesDTO)
            {
                DTOProperties.AddRange(DTOClass.Properties);
            }

            sbClassNames.AppendLine($$"""
import { Injectable } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

@Injectable({
  providedIn: 'root',
})
export class TranslateClassNamesGeneratedService {

    constructor(
        private translocoService: TranslocoService
    ) {
    }

    translate = (name: string): string => {
        switch(name) 
        {
{{string.Join("\n", GetCasesForClassNameTranslate(referencedClassesDTO))}}
            default:
                return null;
        }
    }
}
""");

            sbLabels.AppendLine($$"""
import { Injectable } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

@Injectable({
  providedIn: 'root',
})
export class TranslateLabelsGeneratedService {

    constructor(
        private translocoService: TranslocoService
    ) {
    }

    translate = (name: string): string => {
        switch(name) 
        {
{{string.Join("\n", GetCasesForLabelTranslate(DTOProperties))}}
            default:
                return null;
        }
    }
}
""");
            
            Helpers.WriteToTheFile(sbClassNames.ToString(), Path.Combine(outputPath, "class-names.generated.ts"));
            Helpers.WriteToTheFile(sbLabels.ToString(), Path.Combine(outputPath, "labels.generated.ts"));
        }

        private static List<string> GetCasesForLabelTranslate(List<SpiderlyProperty> DTOProperties)
        {
            List<string> result = new();
            
            foreach (SpiderlyProperty DTOProperty in DTOProperties)
            {
                string propName = DTOProperty.Name;

                if (propName.EndsWith("Id") && propName != "Id")
                    propName = propName.Substring(0, propName.Length - 2);

                if (propName.EndsWith("DisplayName") && propName != "DisplayName")
                    continue;

                if (propName.EndsWith("CommaSeparated") && propName != "CommaSeparated")
                    propName = propName.Replace("CommaSeparated", "");

                string singleCase = $$"""
            case '{{propName.FirstCharToLower()}}':
                return this.translocoService.translate('{{propName}}');
""";

                if (result.Contains(singleCase) == false)
                    result.Add(singleCase);
            }

            return result;
        }

        private static List<string> GetCasesForClassNameTranslate(IList<SpiderlyClass> DTOclasses)
        {
            List<string> result = new();

            foreach (string className in DTOclasses.DistinctBy(x => x.Name).Select(x => x.Name.Replace("DTO", "")))
            {
                result.Add($$""""
            case '{{className}}':
                return this.translocoService.translate('{{className}}');
"""");
            }

            return result;
        }

    }
}
