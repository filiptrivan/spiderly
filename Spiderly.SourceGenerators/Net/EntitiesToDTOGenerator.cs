using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using System.Linq;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Enums;
using System.Diagnostics;
using System;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Generates partial DTO (Data Transfer Object) classes (`{YourAppName}DTOList.generated.cs`)
    /// within the `{YourBaseNamespace}.DTO` namespace. These DTOs are automatically created
    /// based on your entity classes located in the '.Entities' namespace, providing a
    /// separate representation of your data for transfer purposes.
    /// </summary>
    [Generator]
    public class EntitiesToDTOGenerator : IIncrementalGenerator
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
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities });

            var combinedWithEnums = combined.Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var (combinedSource, enumNames) = source;
                var (((classes, referencedClasses), config), nullableContext) = combinedSource;
                Execute(classes, referencedClasses, enumNames, config, nullableContext, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, ImmutableArray<string> spiderlyEnumNames, SpiderlyConfig config, NullableContextOptions nullableContext, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(EntitiesToDTOGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectEntities, spiderlyEnumNames);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            if (currentProjectEntities.Count == 0)
                return;

            bool hasValidationErrors = false;
            foreach (Diagnostic diagnostic in Validations.ValidateDisplayNameAttributes(currentProjectEntities, allEntities))
            {
                context.ReportDiagnostic(diagnostic);
                hasValidationErrors = true;
            }
            foreach (Diagnostic diagnostic in Validations.ValidateWithManyAttributes(currentProjectEntities, allEntities))
            {
                context.ReportDiagnostic(diagnostic);
                hasValidationErrors = true;
            }
            if (hasValidationErrors)
                return;

            List<SpiderlyClass> currentProjectDTOClasses = SpiderlyClassFactory.GetDTOClasses(currentProjectEntities, allEntities);

            HashSet<string> handWrittenDTONames = new HashSet<string>(
                currentProjectClasses
                    .Where(x => x.HasSpiderlyDTOAttribute())
                    .Select(x => x.Name));

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            string result = $$"""
{{GetUsings(basePartOfNamespace)}}

namespace {{basePartOfNamespace}}.DTO
{
{{GetDTOClasses(currentProjectDTOClasses, handWrittenDTONames, nullableContext)}}
}
""";

            context.AddSpiderlyCSharpSource("DTOList.generated", result, nullableContext);
        }

        private static string GetDTOClasses(List<SpiderlyClass> currentProjectDTOClasses, HashSet<string> handWrittenDTONames, NullableContextOptions nullableContext)
        {
            List<string> result = new();

            foreach (SpiderlyClass currentProjectDTOClass in currentProjectDTOClasses)
            {
                // Skip [SpiderlyDTO] when the user wrote a hand-typed partial for this class — that partial already carries the attribute,
                // and a non-AllowMultiple marker would collide.
                string attributeLine = handWrittenDTONames.Contains(currentProjectDTOClass.Name) ? "" : "[SpiderlyDTO]\n    ";

                if (currentProjectDTOClass.Description != null)
                {
                    result.Add($$"""
    /// <summary>
    /// {{currentProjectDTOClass.Description}}
    /// </summary>
    {{attributeLine}}public partial class {{currentProjectDTOClass.Name}} {{GetDTOBaseTypeExtension(currentProjectDTOClass.BaseType)}}
    {
{{GetDTOProperties(currentProjectDTOClass, nullableContext)}}
    }
""");
                }
                else
                {
                    result.Add($$"""
    {{attributeLine}}public partial class {{currentProjectDTOClass.Name}} {{GetDTOBaseTypeExtension(currentProjectDTOClass.BaseType)}}
    {
{{GetDTOProperties(currentProjectDTOClass, nullableContext)}}
    }
""");
                }
            }

            return string.Join("\n\n", result);
        }

        /// <summary>
        /// Getting the properties of the DTO based on the entity class, we don't include base type properties because of the inheritance
        /// </summary>
        private static string GetDTOProperties(SpiderlyClass currentProjectDTOClass, NullableContextOptions nullableContext)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in currentProjectDTOClass.Properties)
            {
                if (property.EntityName != currentProjectDTOClass.Name)
                    continue;

                string listInitializer = property.Type.IsEnumerable() ? " = new();" : "";
                string propertyType = GetEmittedDTOPropertyType(property, nullableContext);

                if (property.Description != null)
                {
                    result.Add($$"""
        /// <summary>
        /// {{property.Description}}
        /// </summary>
        public {{propertyType}} {{property.Name}} { get; set; }{{listInitializer}}
""");
                }
                else
                {
                    result.Add($$"""
        public {{propertyType}} {{property.Name}} { get; set; }{{listInitializer}}
""");
                }
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// The DTO property type as emitted. A DTO is the wire shape: it is deserialized field-by-field and
        /// validated afterwards by the generated FluentValidation rules, so every field is optional on arrival.
        /// The nullable-oblivious emission already encodes that for value types (<c>int</c> -&gt; <c>int?</c>,
        /// see <see cref="SpiderlyClassFactory.GetFormatedDTOPropertyType"/>); under an annotated context the
        /// same truth is extended to reference types (<c>string</c> -&gt; <c>string?</c>, a nested DTO ->
        /// <c>FooDTO?</c>) rather than asserting non-null with <c>= null!</c>, which would let a missing JSON
        /// field surface as a null hiding behind a non-nullable type.
        /// <para>
        /// Collections are exempt: they carry a <c>= new()</c> initializer, so an absent field yields an empty
        /// list, never null.
        /// </para>
        /// </summary>
        private static string GetEmittedDTOPropertyType(SpiderlyProperty property, NullableContextOptions nullableContext)
        {
            string declaredType = property.Type.Raw;

            bool annotationsEnabled = nullableContext == NullableContextOptions.Enable
                || nullableContext == NullableContextOptions.Annotations;

            if (!annotationsEnabled)
                return declaredType;

            // Already nullable (value-type '?' from the DTO column mapping, or an annotated reference type).
            if (declaredType.EndsWith("?"))
                return declaredType;

            // Initialized to an empty instance on construction — never null.
            if (property.Type.IsEnumerable())
                return declaredType;

            // Value types can't take a reference annotation; 'int'/'decimal'/... already came through the
            // oblivious mapping as 'int?' above, and an enum stays a non-null value type.
            if (property.Type.IsBaseDataType() && property.Type.Name != "string")
                return declaredType;

            if (property.IsEnum)
                return declaredType;

            return $"{declaredType}?";
        }

        #region Helpers

        private static string GetDTOBaseTypeExtension(string? DTObaseType)
        {
            return DTObaseType == null ? "" : $": {DTObaseType}";
        }

        private static string GetUsings(string basePartOfNamespace)
        {
            // {basePartOfNamespace}.Enums is needed when entity properties are typed as a
            // [SpiderlyEnum]-decorated enum — the DTO emits the enum type by short name and
            // would otherwise fail to resolve. Convention: enums always live under .Enums.
            return $$"""
using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.DTO;
using Spiderly.Security.DTO;
using Spiderly.Shared.Helpers;
using {{basePartOfNamespace}}.Enums;
""";
        }

        #endregion
    }
}
