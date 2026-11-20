using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Models;
using System;
using System.Globalization;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    internal static class SpiderlyAttributeMapper
    {
        internal static SpiderlyAttribute Map(AttributeData attribute)
        {
            INamedTypeSymbol type = attribute.AttributeClass!;
            string name = type.Name;
            if (name.EndsWith("Attribute", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "Attribute".Length);

            if (name == "MaxFileSize" && type.ContainingNamespace.ToDisplayString() == "Spiderly.Shared.Attributes.Entity")
            {
                int? size = attribute.ConstructorArguments.Length == 1
                    && attribute.ConstructorArguments[0] is { Kind: TypedConstantKind.Primitive, Value: int bytes }
                    && bytes > 0 ? bytes : null;
                return new SpiderlyAttribute { Name = name, Value = size?.ToString(CultureInfo.InvariantCulture), Int32Value = size };
            }

            if (name == "StringLength")
                return new SpiderlyAttribute { Name = name, Value = GetStringLengthValue(attribute) };

            var values = attribute.ConstructorArguments.Select(GetConstantValue).Where(value => value != null)
                .Concat(attribute.NamedArguments.Select(arg => $"{arg.Key}={GetConstantValue(arg.Value)}")).ToArray();
            return new SpiderlyAttribute
            {
                Name = name,
                Value = values.Length == 0 ? null : ClassAnalyzer.GetFormatedAttributeValue(string.Join(", ", values)),
            };
        }

        private static string? GetConstantValue(TypedConstant constant)
        {
            if (constant.IsNull || constant.Kind == TypedConstantKind.Error)
                return null;

            return constant.Kind == TypedConstantKind.Array
                ? string.Join(", ", constant.Values.Select(GetConstantValue))
                : Convert.ToString(constant.Value, CultureInfo.InvariantCulture);
        }

        private static string? GetStringLengthValue(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not int max)
                return null;

            var minimum = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == "MinimumLength").Value;
            return minimum.Value is int min
                ? FormattableString.Invariant($"{max}, MinimumLength={min}")
                : max.ToString(CultureInfo.InvariantCulture);
        }
    }
}
