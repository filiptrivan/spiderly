using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Spiderly.SourceGenerators.Shared
{
    public static class Extensions
    {
        #region Case Extensions

        /// <summary>
        /// There is more performant way but this is NET2
        /// </summary>
        public static string FirstCharToUpper(this string input)
        {
            switch (input)
            {
                case null: return null;
                case "": return null;
                default: return input.First().ToString().ToUpper() + input.Substring(1);
            }
        }

        /// <summary>
        /// There is more performant way but this is NET2
        /// </summary>
        public static string FirstCharToLower(this string input)
        {
            switch (input)
            {
                case null: return null;
                case "": return null;
                default: return input.First().ToString().ToLower() + input.Substring(1);
            }
        }

        public static string FromPascalToKebabCase(this string pascalCaseString)
        {
            if (string.IsNullOrEmpty(pascalCaseString))
            {
                return string.Empty;
            }

            string kebabCaseString = Regex.Replace(pascalCaseString, "([a-z])([A-Z])", "$1-$2");
            kebabCaseString = kebabCaseString.ToLower();

            return kebabCaseString;
        }

        /// <summary>
        /// Converts the specified string to PascalCase.
        /// </summary>
        /// <param name="text">The string to convert.</param>
        /// <returns>The PascalCase version of the string.</returns>
        public static string ToPascalCase(this string text)
        {
            // Create a StringBuilder object to store the result.
            StringBuilder result = new();

            // Get the TextInfo object for the current culture.
            TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;

            // Flag to track if we are at the beginning of a new word.
            bool newWord = true;

            // Iterate over each character in the string.
            for (int i = 0; i < text.Length; i++)
            {
                char currentChar = text[i];

                // If the current character is a letter or digit.
                if (char.IsLetterOrDigit(currentChar))
                {
                    // If we are at the beginning of a new word, convert the character to uppercase.
                    if (newWord)
                    {
                        result.Append(textInfo.ToUpper(currentChar));
                        newWord = false;
                    }
                    // Otherwise, add the character as is for uppercase or convert to lowercase for other characters.
                    else
                    {
                        result.Append(i < text.Length - 1 && char.IsUpper(currentChar) && char.IsLower(text[i + 1]) ? currentChar : char.ToLowerInvariant(currentChar));
                    }
                }
                // If the current character is not a letter or digit, we are at the beginning of a new word.
                else
                {
                    newWord = true;
                }

                // If the current character is a lowercase letter and the next character is an uppercase letter,
                // we are at the beginning of a new word.
                if (i < text.Length - 1 && char.IsLower(text[i]) && char.IsUpper(text[i + 1]))
                {
                    newWord = true;
                }
            }

            // Return the result as a string.
            return result.ToString();
        }

        /// <summary>
        /// Splits a given camel case string into separate words using the specified separator.
        /// </summary>
        /// <param name="input">The camel case string to be split.</param>
        /// <param name="splitWith">The separator to be used. By default, a single space is used.</param>
        /// <returns>The resulting string with words separated by the specified separator.</returns>
        public static string SplitCamelCase(this string input, string splitWith = " ")
        {
            if (string.IsNullOrEmpty(input)) return input; // if input is null or empty, return it as it is.

            // Create a new instance of StringBuilder to store the output string
            StringBuilder result = new();
            // Flag to keep track of whether the previous character was an uppercase letter or not
            bool isPrevUpper = false;

            // Iterate over each character in the input string
            for (int i = 0; i < input.Length; i++)
            {
                // Get the current character
                char currentChar = input[i];

                // If the current character is uppercase and not the first character
                if (i > 0 && char.IsUpper(currentChar))
                {
                    // If the previous character was not uppercase or the next character is not uppercase
                    if (!isPrevUpper || (i < input.Length - 1 && !char.IsUpper(input[i + 1])))
                    {
                        // Append the separator to the output string
                        result.Append(splitWith);
                    }
                }

                // Append the current character to the output string
                result.Append(currentChar);
                // Update the flag to reflect whether the current character is uppercase or not
                isPrevUpper = char.IsUpper(currentChar);
            }

            // Return the resulting string with words separated by the specified separator
            return result.ToString();
        }

        #endregion

        #region Is Type

        /// <summary>
        /// User -> true
        /// string -> false
        /// List<User> -> false
        /// </summary>
        public static bool IsManyToOneType(this string type)
        {
            if (type.IsEnumerable())
                return false;

            if (type.IsBaseDataType())
                return false;

            return true;
        }

        /// <summary>
        /// Enum-aware overload. A property whose type is a <c>[SpiderlyEnum]</c>-decorated enum is a scalar value,
        /// not a navigation target — short-circuit M2O classification before falling through to the string-based check.
        /// Generators that have a <see cref="SpiderlyProperty"/> in scope should prefer this overload.
        /// </summary>
        public static bool IsManyToOneType(this SpiderlyProperty property)
        {
            if (property.IsEnum)
                return false;

            if (property.HasWithOneAttribute()) // one-to-one dependent is NOT many-to-one
                return false;

            return property.Type.Raw.IsManyToOneType();
        }

        // ----- SpiderlyTypeRef overloads of the type-classification checks -----
        // Collection detection routes through the parser (IsCollection); the name-set checks (base data type,
        // date/time) delegate to the string implementations on Raw. Either way the logic lives in one place, so
        // generators holding a SpiderlyTypeRef read naturally (property.Type.IsEnumerable()) without re-parsing.

        public static bool IsManyToOneType(this SpiderlyTypeRef type) => type?.Raw.IsManyToOneType() ?? false;

        public static bool IsEnumerable(this SpiderlyTypeRef type) => type?.IsCollection ?? false;

        public static bool IsOneToManyType(this SpiderlyTypeRef type) => type?.Raw.IsOneToManyType() ?? false;

        public static bool IsBaseDataType(this SpiderlyTypeRef type) => type?.Raw.IsBaseDataType() ?? false;

        public static bool IsDateTime(this SpiderlyTypeRef type) => type?.Raw.IsDateTime() ?? false;

        public static bool IsDateOnly(this SpiderlyTypeRef type) => type?.Raw.IsDateOnly() ?? false;

        public static bool IsTimeOnly(this SpiderlyTypeRef type) => type?.Raw.IsTimeOnly() ?? false;

        public static bool IsEnum(this SpiderlyTypeRef type, ImmutableArray<string> spiderlyEnumNames) => type?.Raw.IsEnum(spiderlyEnumNames) ?? false;

        /// <summary>
        /// Routes through the single parser (<see cref="SpiderlyTypeRef.IsCollection"/>) so there is exactly one
        /// notion of "is this a collection" — exact outer-type-name matching, not a loose <c>Contains("List")</c>
        /// that would also match a type literally named e.g. <c>Listing</c>.
        /// </summary>
        public static bool IsEnumerable(this string type)
        {
            return SpiderlyTypeRef.Parse(type)?.IsCollection ?? false;
        }

        public static bool IsOneToManyType(this string type)
        {
            if (!type.IsEnumerable())
                return false;

            // Extract inner generic type (e.g., "long" from "List<long>")
            string extractedType = Helpers.ExtractTypeFromGenericType(type);
            return !extractedType.IsBaseDataType();
        }

        public static bool IsBaseDataType(this string propType)
        {
            return
                propType == "string" ||
                propType == "bool" ||
                propType == "bool?" ||
                propType == "DateTime" ||
                propType == "DateTime?" ||
                propType == "System.DateTime" ||
                propType == "System.DateTime?" ||
                propType == "DateOnly" ||
                propType == "DateOnly?" ||
                propType == "System.DateOnly" ||
                propType == "System.DateOnly?" ||
                propType == "TimeOnly" ||
                propType == "TimeOnly?" ||
                propType == "System.TimeOnly" ||
                propType == "System.TimeOnly?" ||
                propType == "long" ||
                propType == "long?" ||
                propType == "int" ||
                propType == "int?" ||
                propType == "decimal" ||
                propType == "decimal?" ||
                propType == "float" ||
                propType == "float?" ||
                propType == "double" ||
                propType == "double?" ||
                propType == "byte" ||
                propType == "byte?" ||
                propType == "System.Guid" ||
                propType == "System.Guid?" ||
                propType == "Guid" ||
                propType == "Guid?";
        }

        public static bool IsDateTime(this string propType) =>
            propType == "DateTime" || propType == "DateTime?" ||
            propType == "System.DateTime" || propType == "System.DateTime?";

        public static bool IsDateOnly(this string propType) =>
            propType == "DateOnly" || propType == "DateOnly?" ||
            propType == "System.DateOnly" || propType == "System.DateOnly?";

        public static bool IsTimeOnly(this string propType) =>
            propType == "TimeOnly" || propType == "TimeOnly?" ||
            propType == "System.TimeOnly" || propType == "System.TimeOnly?";

        public static bool IsManyToMany(this SpiderlyClass c)
        {
            if (c.BaseType == null)
                return true;

            return false;
        }

        public static bool IsAbstract(this ClassDeclarationSyntax c)
        {
            return c.Modifiers.Any(x => x.Text == "abstract");
        }

        /// <summary>
        /// User : BusinessObject<long> -> true
        /// User : ReadonlyObject<long> -> false
        /// </summary>
        public static bool IsBusinessObject(this SpiderlyClass c)
        {
            return c.BaseType?.Contains($"{Helpers.BusinessObject}<") == true;
        }

        public static bool IsReadonlyObject(this SpiderlyClass c)
        {
            return c.BaseType?.Contains($"{Helpers.ReadonlyObject}<") == true;
        }

        /// <summary>
        /// True when this entity gets a generated base-details component. The component emitter
        /// (<c>NgBaseDetailsGenerator</c>) and anything that imports/binds into that component (e.g. the enum-helper
        /// import emitter) must agree on this set, so the filter lives here as a single source of truth rather than
        /// being copy-pasted — a drift between the two silently ships a missing or unused import.
        /// </summary>
        public static bool GeneratesDetailsComponent(this SpiderlyClass entity)
        {
            return entity.HasUIDoNotGenerateAttribute() == false
                && entity.IsReadonlyObject() == false
                && entity.IsManyToMany() == false;
        }

        /// <summary>
        /// Strips the trailing nullability marker from a stringified type name.
        /// <c>"int?"</c> -> <c>"int"</c>; <c>"OrderStatusCodes"</c> -> <c>"OrderStatusCodes"</c>.
        /// Tolerates trailing whitespace from upstream type-syntax stringification.
        /// </summary>
        public static string WithoutNullableSuffix(this string typeName)
        {
            return typeName?.TrimEnd().TrimEnd('?');
        }


        /// <summary>
        /// Registry-aware enum check. A type is treated as an enum when its (unwrapped, non-nullable) name
        /// appears in <paramref name="spiderlyEnumNames"/> — the set of <c>[SpiderlyEnum]</c>-decorated enum
        /// type names collected by <see cref="PipelineFactory.GetSpiderlyEnumNamesProvider"/>.
        /// <c>[SpiderlyEnum]</c> is the single source of truth — the type name is irrelevant.
        /// </summary>
        public static bool IsEnum(this string type, ImmutableArray<string> spiderlyEnumNames)
        {
            if (type == null || spiderlyEnumNames.IsDefaultOrEmpty)
                return false;

            // Unwrap nullability + collection/generic wrappers through the single parser, so this check
            // and every site that emits the enum name (e.g. the Angular enum import) agree on what the
            // underlying type is. Drift between the two was the original duplicate / "Foo?" import bug.
            return spiderlyEnumNames.Contains(SpiderlyTypeRef.Parse(type).CoreName);
        }

        /// <summary>
        /// A property is a blob when it carries any attribute whose simple name ends with
        /// <c>"Storage"</c> — the convention shared by all subclasses of
        /// <see cref="Spiderly.Shared.Attributes.Entity.StorageAttribute"/> (built-in
        /// <c>[DiskStorage]</c>, <c>[S3PublicStorage]</c>, <c>[S3PrivateStorage]</c>, and any
        /// custom subclass a consumer ships).
        /// </summary>
        public static bool IsBlob(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name != null && x.Name.EndsWith("Storage"));
        }

        public static int GetImageWidth(this SpiderlyProperty property)
        {
            SpiderlyAttribute attribute = property.Attributes.FirstOrDefault(x => x.Name == "ImageWidth");

            if (attribute == null || string.IsNullOrEmpty(attribute.Value))
                return 0;

            int.TryParse(attribute.Value, out int width);
            return width;
        }

        public static int GetImageHeight(this SpiderlyProperty property)
        {
            SpiderlyAttribute attribute = property.Attributes.FirstOrDefault(x => x.Name == "ImageHeight");

            if (attribute == null || string.IsNullOrEmpty(attribute.Value))
                return 0;

            int.TryParse(attribute.Value, out int height);
            return height;
        }

        public static List<string> GetAcceptedFileTypes(this SpiderlyProperty property)
        {
            SpiderlyAttribute attribute = property.Attributes.FirstOrDefault(x => x.Name == "AcceptedFileTypes");

            if (attribute == null || string.IsNullOrEmpty(attribute.Value))
                return null;

            return attribute.Value.Split(',').Select(x => x.Trim()).ToList();
        }

        public static int GetMaxFileSize(this SpiderlyProperty property)
        {
            SpiderlyAttribute attribute = property.Attributes.FirstOrDefault(x => x.Name == "MaxFileSize");

            if (attribute == null || string.IsNullOrEmpty(attribute.Value))
                return 0;

            int.TryParse(attribute.Value, out int maxFileSize);
            return maxFileSize;
        }

        public static List<UITableColumn> GetUITableColumns(this SpiderlyProperty property)
        {
            List<UITableColumn> result = new List<UITableColumn>();

            foreach (SpiderlyAttribute attribute in property.Attributes)
            {
                if (attribute.Name == "UITableColumn")
                {
                    List<string> attributeValues = attribute.Value.Split(',').Select(v => v.Trim()).ToList();
                    string field = attributeValues[0];
                    string translationKey = attributeValues.Count > 1 ? attributeValues[1] : null;

                    result.Add(new UITableColumn
                    {
                        Field = field,
                        TranslationKey = translationKey ?? field.Replace("DisplayName", ""),
                    });
                }
            }

            return result;
        }

        public static bool ShouldGenerateDropdownControllerMethod(this SpiderlyProperty property)
        {
            return property.IsDropdownControlType() || property.IsMultiSelectControlType() || property.HasGenerateCommaSeparatedDisplayNameAttribute();
        }

        public static bool ShouldGenerateAutocompleteControllerMethod(this SpiderlyProperty property)
        {
            // Enum-typed properties get a static client-side dropdown bound to the TS enum,
            // so the backend doesn't need to expose an autocomplete endpoint for them.
            if (
                property.IsManyToOneType() &&
                property.Attributes.Any(x => x.Name == "UIControlType") == false
            )
            {
                return true;
            }

            return property.IsAutocompleteControlType() || property.IsMultiAutocompleteControlType();
        }

        #region Has Attribute

        public static bool HasUIDoNotGenerateAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "UIDoNotGenerate");
        }

        public static bool HasDoNotAuthorizeAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "DoNotAuthorize");
        }

        public static bool HasSpiderlyEntityAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "SpiderlyEntity");
        }

        public static bool HasSpiderlyDTOAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "SpiderlyDTO");
        }

        public static bool HasSpiderlyControllerAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "SpiderlyController");
        }

        public static bool HasSpiderlyDataMapperAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "SpiderlyDataMapper");
        }

        public static bool HasSpiderlyServiceAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "SpiderlyService");
        }

        public static bool HasSpiderlyEnumAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "SpiderlyEnum");
        }

        public static bool HasDisplayNameAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "DisplayName");
        }

        public static bool HasM2MAttribute(this SpiderlyClass entity)
        {
            return entity.Attributes.Any(x => x.Name == "M2M");
        }

        public static bool HasRequiredAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "Required");
        }

        /// <summary>
        /// Returns true when a navigation or FK scalar should be treated as non-nullable in the
        /// generated model — either the user wrote <c>[Required]</c>, or the navigation is on an
        /// M2M junction where <c>[M2MWithMany]</c> is an implicit "required" signal (a junction
        /// row without both sides is meaningless, and Spiderly's M2M template never emits
        /// <c>[Required]</c> on these, so callers must not gate on <c>HasRequiredAttribute</c> alone).
        /// </summary>
        public static bool IsEffectivelyRequired(this SpiderlyProperty property)
        {
            return property.HasRequiredAttribute() || property.HasM2MWithManyAttribute();
        }

        public static bool HasUIOrderedOneToManyAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "UIOrderedOneToMany");
        }

        public static bool HasSimpleManyToManyTableLazyLoadAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "SimpleManyToManyTableLazyLoad");
        }

        public static bool HasComplexManyToManyReadonlyTableAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "ComplexManyToManyReadonlyTable");
        }

        public static bool HasComplexManyToManyListAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "ComplexManyToManyList");
        }

        public static bool HasM2MWithManyAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "M2MWithMany");
        }

        public static bool HasWithManyAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "WithMany");
        }

        /// <summary>True when the property carries [WithOne] — i.e. it is the dependent side of a one-to-one.</summary>
        public static bool HasWithOneAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "WithOne");
        }

        /// <summary>
        /// True when the property is the dependent side of a one-to-one: a single reference nav (not enum,
        /// not base type, not collection) carrying [WithOne].
        /// </summary>
        public static bool IsOneToOneType(this SpiderlyProperty property)
        {
            if (property.HasWithOneAttribute() == false)
                return false;

            if (property.IsEnum)
                return false;

            return property.Type.Raw.IsManyToOneType(); // string overload: not enumerable, not base type
        }

        /// <summary>
        /// The inverse-nav name from [WithOne("...")], or null for a unidirectional 1-1.
        /// Mirrors <see cref="WithMany"/>'s reading of the [WithMany] ctor argument.
        /// </summary>
        public static string GetWithOneInverseName(this SpiderlyProperty property)
        {
            return property.Attributes.Where(x => x.Name == "WithOne").Select(x => x.Value).SingleOrDefault();
        }

        public static bool HasGenerateCommaSeparatedDisplayNameAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "GenerateCommaSeparatedDisplayName");
        }

        public static bool HasIncludeInDTOAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "IncludeInDTO");
        }

        public static bool HasExcludeServiceMethodsFromGenerationAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "ExcludeServiceMethodsFromGeneration");
        }

        public static bool HasFromFormAttribute(this SpiderParameter parameter)
        {
            return parameter.Attributes.Any(x => x.Name == "FromForm");
        }

        public static bool HasUIDoNotGenerateAttribute(this SpiderlyMethod method)
        {
            return method.Attributes.Any(x => x.Name == "UIDoNotGenerate");
        }

        public static bool HasUIDoNotGenerateAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "UIDoNotGenerate");
        }

        public static bool HasS3PublicStorageAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "S3PublicStorage");
        }

        public static bool HasS3PrivateStorageAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "S3PrivateStorage");
        }

        public static bool HasDiskStorageAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "DiskStorage");
        }

        public static bool HasForeignKeyAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "ForeignKey");
        }

        public static string GetForeignKeyAttributeValue(this SpiderlyProperty property)
        {
            return property.Attributes.Where(x => x.Name == "ForeignKey").Select(x => x.Value).SingleOrDefault();
        }

        /// <summary>
        /// True when the column stores a directly URL-addressable value (a CDN URL) rather
        /// than an opaque key requiring a signed-URL or proxy round-trip. Today this is
        /// equivalent to <see cref="HasS3PublicStorageAttribute"/>; custom adapters that
        /// also store full URLs are not auto-detected here.
        /// </summary>
        public static bool IsPublicUrl(this SpiderlyProperty property)
        {
            return property.HasS3PublicStorageAttribute();
        }

        #endregion

        #region Foreign Key Resolution

        /// <summary>
        /// Resolves the explicit foreign key scalar property paired with a many-to-one navigation,
        /// or returns null when the navigation uses the shadow FK pattern.
        ///
        /// Priority:
        /// 1. [ForeignKey(nameof(X))] on the navigation → X
        /// 2. [ForeignKey(nameof(Nav))] on a scalar property → that scalar's name
        /// 3. Convention: scalar property named "{NavName}Id" with a base data type → its name
        /// 4. None of the above → null (shadow FK — legacy Spiderly behavior)
        /// </summary>
        /// <example>
        /// public long? ParentCategoryId { get; set; }
        /// [ForeignKey(nameof(ParentCategoryId))]
        /// public virtual Category ParentCategory { get; set; }
        /// // ResolveExplicitForeignKeyName(ParentCategory) => "ParentCategoryId"
        /// </example>
        public static string ResolveExplicitForeignKeyName(this SpiderlyProperty navigation, SpiderlyClass entity)
        {
            // FK resolution is identical for many-to-one and one-to-one ([WithOne]) navigations
            // ([ForeignKey] → scalar [ForeignKey] → {Nav}Id convention). Since [WithOne] navs are
            // deliberately not many-to-one, accept either to resolve their FK as well.
            if (navigation.IsManyToOneType() == false && navigation.IsOneToOneType() == false)
                return null;

            string fkFromNavAttribute = navigation.GetForeignKeyAttributeValue();
            if (fkFromNavAttribute != null && entity.Properties.Any(p => p.Name == fkFromNavAttribute))
                return fkFromNavAttribute;

            SpiderlyProperty scalarWithFkAttribute = entity.Properties
                .FirstOrDefault(p => p.Type.IsBaseDataType() && p.GetForeignKeyAttributeValue() == navigation.Name);

            if (scalarWithFkAttribute != null)
                return scalarWithFkAttribute.Name;

            string conventionName = $"{navigation.Name}Id";
            SpiderlyProperty conventionMatch = entity.Properties
                .FirstOrDefault(p => p.Name == conventionName && p.Type.IsBaseDataType());

            if (conventionMatch != null)
                return conventionMatch.Name;

            return null;
        }

        /// <summary>
        /// Returns the names of scalar properties that are the explicit FK of a many-to-one navigation
        /// on the same entity. Useful for UI generators that want to suppress the redundant numeric
        /// input that would otherwise be rendered next to the navigation's autocomplete picker.
        /// </summary>
        public static HashSet<string> GetPairedForeignKeyNames(this SpiderlyClass entity)
        {
            HashSet<string> result = new();

            foreach (SpiderlyProperty nav in entity.Properties)
            {
                if (nav.HasWithManyAttribute() == false)
                    continue;

                string fkName = nav.ResolveExplicitForeignKeyName(entity);
                if (fkName != null)
                    result.Add(fkName);
            }

            return result;
        }

        /// <summary>
        /// Returns the LINQ expression fragment used to read a FK value inside an IQueryable.
        /// Avoids the `{param}.Nav.Id` shape because EF Core still emits a JOIN when it sees
        /// `nav.Id` access — unresolved since 2019 (https://github.com/dotnet/efcore/issues/15826).
        ///
        /// - Explicit FK declared → `{param}.FkName`
        /// - Shadow fallback → `EF.Property&lt;{idType}&gt;({param}, "{Nav}Id")`
        ///
        /// The generic type is non-nullable to match the original `{param}.Nav.Id` semantics
        /// (so `List&lt;long&gt;.Contains(...)` overload resolution stays intact). Nullable-FK
        /// filter semantics are preserved because EF translates this to a column predicate —
        /// the generic annotation is compile-time only; SQL `WHERE col = @id` returns false
        /// for null cells without materializing.
        ///
        /// Generated code that uses the shadow branch must `using Microsoft.EntityFrameworkCore;`.
        /// </summary>
        public static string GetForeignKeyAccessExpression(
            this SpiderlyProperty navigation,
            SpiderlyClass entity,
            List<SpiderlyClass> entities,
            string parameterName = "x")
        {
            string fkName = navigation.ResolveExplicitForeignKeyName(entity);
            if (fkName != null)
                return $"{parameterName}.{fkName}";

            SpiderlyClass target = entities.FirstOrDefault(c => c.Name == navigation.Type.Raw);
            string idType = target != null ? target.GetIdType(entities) : "long";
            return $"EF.Property<{idType}>({parameterName}, \"{navigation.Name}Id\")";
        }

        #endregion

        #region IsControlType

        public static bool IsColorControlType(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "UIControlType" && x.Value == UIControlTypeCodes.ColorPicker.ToString());
        }

        public static bool IsMultiSelectControlType(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "UIControlType" && x.Value == UIControlTypeCodes.MultiSelect.ToString());
        }

        public static bool IsMultiAutocompleteControlType(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "UIControlType" && x.Value == UIControlTypeCodes.MultiAutocomplete.ToString());
        }

        public static bool IsDropdownControlType(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "UIControlType" && x.Value == UIControlTypeCodes.Dropdown.ToString());
        }

        public static bool IsAutocompleteControlType(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "UIControlType" && x.Value == UIControlTypeCodes.Autocomplete.ToString());
        }

        public static bool IsEditorControlType(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "UIControlType" && x.Value == UIControlTypeCodes.Editor.ToString());
        }

        public static bool IsMarkdownControlType(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "UIControlType" && x.Value == UIControlTypeCodes.Markdown.ToString());
        }

        #endregion

        #endregion

        #region Source Generator

        /// <summary>
        /// The same method is built in .NET8 linq, but source generator is .NET2
        /// </summary>
        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> property)
        {
            return items.GroupBy(property).Select(x => x.First());
        }

        public static string GetDTOBaseType(this SpiderlyClass c)
        {
            string baseClass = c.BaseType;
            if (baseClass == null)
                return null;
            else if (baseClass.Contains("<"))
                return baseClass.Replace("<", "DTO<");
            else
                return $"{baseClass}DTO";
        }

        public static string GetTableFilterAdditionalFilterPropertyName(this string idType)
        {
            if (idType == "int" || idType == "int?")
            {
                return "AdditionalFilterIdInt";
            }
            else if (idType == "long" || idType == "long?")
            {
                return "AdditionalFilterIdLong";
            }
            else if (idType == "byte" || idType == "byte?")
            {
                return "AdditionalFilterIdByte";
            }

            return null;
        }

        public static string GetBaseType(this ClassDeclarationSyntax c)
        {
            TypeSyntax baseType = c.BaseList?.Types.FirstOrDefault()?.Type; //BaseClass<long>

            if (baseType != null)
                return baseType.ToString();

            return null; // FT: many to many doesn't have base class
        }

        public static string GetNamespace(this BaseTypeDeclarationSyntax baseTypeDeclarationSyntax)
        {
            return baseTypeDeclarationSyntax
                .Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .Select(ns => ns.Name.ToString())
                .FirstOrDefault();
        }

        /// <summary>Gets the file path the source generator was called from.</summary>
        /// <param name="context">The context of the Generator's Execute method.</param>
        /// <returns>The file path the generator was called from.</returns>
        public static IncrementalValueProvider<string> GetCallingPath(this IncrementalGeneratorInitializationContext context)
        {
            return context.AnalyzerConfigOptionsProvider
                .Select((provider, _) =>
                {
                    return provider.GlobalOptions.TryGetValue("build_property.projectdir", out var result)
                        ? result
                        : null;
                });
        }

        /// <summary>Gets the parsed spiderly.json configuration.</summary>
        /// <param name="context">The context of the Generator's initialization.</param>
        /// <returns>The parsed <see cref="SpiderlyConfig"/> instance.</returns>
        public static IncrementalValueProvider<SpiderlyConfig> GetSpiderlyConfig(this IncrementalGeneratorInitializationContext context)
        {
            return context.AdditionalTextsProvider
                .Where(file => file.Path.EndsWith("spiderly.json"))
                .Select((text, cancellationToken) => text.GetText(cancellationToken)?.ToString() ?? string.Empty)
                .Collect()
                .Select((texts, _) => SpiderlyConfig.Parse(texts.FirstOrDefault()));
        }

        public static string GetDecimalScale(this SpiderlyProperty property)
        {
            SpiderlyAttribute precissionAttribute = property.Attributes.Where(x => x.Name == "Precision").SingleOrDefault();

            if (precissionAttribute == null)
                return null;

            return precissionAttribute.Value.Split(',').Last();
        }

        public static string WithMany(this SpiderlyProperty property)
        {
            return property.Attributes.Where(x => x.Name == "WithMany").Select(x => x.Value).SingleOrDefault();
        }

        public static SpiderlyProperty GetManyToOnePropertyWithManyAttribute(this SpiderlyClass entity, string manyToOneType, string withMany)
        {
            return entity.Properties.SingleOrDefault(x => x.Type.Raw == manyToOneType && x.WithMany() == withMany);
        }

        /// <summary>
        /// Single source of truth for "does this property render in the details UI?".
        /// Every details-UI sub-generator (field blocks, option vars, search methods, table
        /// cols/vars, ordered-one-to-many and complex-many-to-many panels) must gate on this so
        /// inclusion is decided once and dangling references become structurally impossible.
        /// Property-scoped only — it does NOT decide whether the entity gets a page at all
        /// (that is the class-level <see cref="HasUIDoNotGenerateAttribute(SpiderlyClass)"/> switch).
        /// </summary>
        public static bool IsIncludedInDetailsUi(this SpiderlyProperty property, SpiderlyClass entity)
        {
            if (property.Name == "Version" ||
                property.Name == "Id" ||
                property.Name == "CreatedAt" ||
                property.Name == "ModifiedAt")
                return false;

            bool isRenderableShape =
                property.Type.IsEnumerable() == false ||
                property.HasUIOrderedOneToManyAttribute() ||
                property.IsMultiSelectControlType() ||
                property.IsMultiAutocompleteControlType() ||
                property.HasSimpleManyToManyTableLazyLoadAttribute() ||
                property.HasComplexManyToManyReadonlyTableAttribute() ||
                property.HasComplexManyToManyListAttribute();

            if (isRenderableShape == false)
                return false;

            if (property.HasUIDoNotGenerateAttribute())
                return false;

            // [ExcludeFromDTO] removes the backing SaveBody/MainUIForm DTO field (see
            // GetSaveBodyDTOProperties / GetMainUIFormDTOProperties), so the control has nothing to
            // bind to — keep the UI gate in lockstep with the DTO gate or the generated Angular
            // dangles on getControl('selected{P}Ids') / controls.selected{P}Ids.setValue(...).
            if (property.HasExcludeFromDTOAttribute())
                return false;

            if (entity.GetPairedForeignKeyNames().Contains(property.Name))
                return false;

            return true;
        }

        public static List<SpiderlyProperty> GetOrderedOneToManyProperties(this SpiderlyClass entity)
        {
            return entity.Properties.Where(x => x.IsIncludedInDetailsUi(entity) && x.HasUIOrderedOneToManyAttribute()).ToList();
        }

        public static List<SpiderlyProperty> GetComplexManyToManyListProperties(this SpiderlyClass entity)
        {
            return entity.Properties.Where(x => x.IsIncludedInDetailsUi(entity) && x.HasComplexManyToManyListAttribute()).ToList();
        }

        public static string GetIdType(this SpiderlyClass c, List<SpiderlyClass> classes)
        {
            if (c == null)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.EntityMissingBusinessObjectBase,
                    null,
                    "<unknown>", "<null>");
            }

            if (c.IsManyToMany())
                return null;

            string baseType = c.BaseType; //BaseClass<long>

            while (baseType != null && baseType.Contains("<") == false)
            {
                SpiderlyClass baseClass = classes.SingleOrDefault(x => x.Name == baseType);

                if (baseClass == null)
                {
                    throw SpiderlyDiagnostics.Create(
                        SpiderlyDiagnostics.EntityMissingBusinessObjectBase,
                        c.Location,
                        c.Name, baseType);
                }

                baseType = baseClass.BaseType; //BaseClass<long>
            }

            if (baseType != null && baseType.Contains("<"))
            {
                string idType = Helpers.ExtractTypeFromGenericType(baseType);
                string baseClassName = baseType.Substring(0, baseType.IndexOf('<'));

                if (!idType.IsAllowedPrimaryKeyType())
                {
                    throw SpiderlyDiagnostics.Create(
                        SpiderlyDiagnostics.UnsupportedPrimaryKeyType,
                        c.Location,
                        c.Name, baseClassName, idType);
                }

                return idType;
            }

            throw SpiderlyDiagnostics.Create(
                SpiderlyDiagnostics.EntityMissingBusinessObjectBase,
                c.Location,
                c.Name, baseType ?? "<none>");
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="idType"/> is one of the C# types
        /// permitted as a Spiderly entity primary key (<c>int</c>, <c>long</c>, <c>byte</c>).
        /// Backs the SPIDERLY018 diagnostic emitted by <see cref="GetIdType"/>.
        /// </summary>
        public static bool IsAllowedPrimaryKeyType(this string idType)
        {
            return idType == "int" || idType == "long" || idType == "byte";
        }

        /// <summary>
        /// True when the property carries <c>[ExcludeFromDTO]</c>. Narrow attribute check only —
        /// use this (NOT <see cref="ShouldSkipPropertyInDTO"/>) when filtering the SaveBody /
        /// MainUIForm collection loops, where <see cref="ShouldSkipPropertyInDTO"/> would wrongly
        /// drop every ordered-one-to-many / many-to-many property (they are one-to-many types
        /// without <c>[GenerateCommaSeparatedDisplayName]</c>/<c>[IncludeInDTO]</c>).
        /// </summary>
        public static bool HasExcludeFromDTOAttribute(this SpiderlyProperty property)
        {
            return property.Attributes.Any(x => x.Name == "ExcludeFromDTO");
        }

        public static bool ShouldSkipPropertyInDTO(this SpiderlyProperty property)
        {
            if (property.HasExcludeFromDTOAttribute() || (
                property.Type.IsOneToManyType() &&
                !property.HasGenerateCommaSeparatedDisplayNameAttribute() &&
                !property.HasIncludeInDTOAttribute()
            ))
            {
                return true;
            }

            return false;
        }

        #endregion

        #region Helpers

        public static string GetRootPath(this string callingProjectDirectory, string backendFolderName = "Backend")
        {
            DirectoryInfo dir = new DirectoryInfo(callingProjectDirectory);

            while (dir != null && dir.Name != backendFolderName)
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.BackendFolderNotFound,
                    null,
                    backendFolderName, callingProjectDirectory);
            }

            return dir.Parent?.FullName;
        }

        public static string ReplaceEverythingAfterLast(this string source, string keyForReplace, string valueToInsert)
        {
            if (string.IsNullOrEmpty(source))
                return null;

            int index = source.LastIndexOf(keyForReplace, StringComparison.Ordinal);

            if (index == -1)
                return source;

            // Get the part before the key and append the new value.
            return $"{source.Substring(0, index)}{valueToInsert}";
        }

        public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> source)
        {
            using (var e = source.GetEnumerator())
            {
                if (e.MoveNext())
                {
                    for (var value = e.Current; e.MoveNext(); value = e.Current)
                    {
                        yield return value;
                    }
                }
            }
        }

        public static List<string> Split(this string input, string splitter)
        {
            return input.Split([splitter], StringSplitOptions.None).ToList();
        }

        #endregion
    }
}
