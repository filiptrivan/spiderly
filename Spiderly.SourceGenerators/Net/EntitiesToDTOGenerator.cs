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

                string propertyType = GetEmittedDTOPropertyType(property, nullableContext);
                string listInitializer = GetEmittedDTOPropertyInitializer(property, propertyType, nullableContext);

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
        /// The DTO property type as emitted. Nullability is keyed off <c>[Required]</c> — the same signal EF
        /// turns into NOT NULL and Swashbuckle turns into a required schema member — so the C# type asserts
        /// what the schema already guarantees instead of declaring every field optional on the grounds that
        /// JSON could omit it. Required reference types are non-nullable and carry <c>= null!</c> (see
        /// <see cref="GetEmittedDTOPropertyInitializer"/>); optional ones are <c>string?</c> / <c>FooDTO?</c>.
        /// <para>
        /// Value types are settled earlier, in <see cref="SpiderlyClassFactory.GetFormatedDTOPropertyType"/>:
        /// <c>int?</c> IS the type, so it applies in an oblivious context too. This method only re-introduces
        /// the reference-type ANNOTATION, which is illegal (CS8632) for an oblivious consumer.
        /// </para>
        /// <para>
        /// Collections are exempt in both directions: they carry <c>= new()</c>, so an absent field yields an
        /// empty list, never null.
        /// </para>
        /// </summary>
        private static string GetEmittedDTOPropertyType(SpiderlyProperty property, NullableContextOptions nullableContext)
        {
            string declaredType = property.Type.Raw;

            if (!nullableContext.AnnotationsEnabled())
                return declaredType;

            // Already nullable (value-type '?' from the DTO column mapping, or an annotated reference type).
            if (declaredType.EndsWith("?"))
                return declaredType;

            // Initialized to an empty instance on construction — never null.
            if (property.Type.IsEnumerable())
                return declaredType;

            // Value-type scalars can't take a reference annotation. Generated ones already came through the
            // column mapping with their final nullability; a hand-written [SpiderlyDTO] class's properties
            // bypass that mapping entirely, so this guard is what keeps its 'int' from becoming 'int?'.
            if (property.Type.IsBaseDataType() && !property.Type.IsReferenceTypeScalar)
                return declaredType;

            if (property.IsEnum)
                return declaredType;

            return property.IsRequired ? declaredType : $"{declaredType}?";
        }

        /// <summary>
        /// The initializer trailing an emitted DTO property, or an empty string.
        /// <para>
        /// Collections get <c>= new()</c> in every context — that is what makes an absent JSON array an empty
        /// list. A non-nullable reference type additionally needs <c>= null!</c> under an annotated context,
        /// or the emitted file raises CS8618 in a consumer who cannot edit it (and, with
        /// <c>&lt;WarningsAsErrors&gt;Nullable&lt;/WarningsAsErrors&gt;</c>, fails their build). The assertion
        /// is backed the same way it is on entities: the column is NOT NULL for a read, and the generated
        /// FluentValidation <c>.NotEmpty()</c> rejects the request before business code sees it for a write.
        /// </para>
        /// </summary>
        private static string GetEmittedDTOPropertyInitializer(SpiderlyProperty property, string emittedType, NullableContextOptions nullableContext)
        {
            if (property.Type.IsEnumerable())
                return " = new();";

            if (!nullableContext.AnnotationsEnabled() || emittedType.EndsWith("?"))
                return "";

            // Value types and enums are already definitely-assigned by their default.
            if (property.IsEnum || (property.Type.IsBaseDataType() && !property.Type.IsReferenceTypeScalar))
                return "";

            return " = null!;";
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
