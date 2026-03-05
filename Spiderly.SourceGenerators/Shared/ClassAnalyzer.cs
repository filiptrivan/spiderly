using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Spiderly.SourceGenerators.Shared
{
    public static class ClassAnalyzer
    {
        /// <summary>
        /// Getting all properties of the single class <paramref name="c"/>, including inherited ones.
        /// The inherited properties doesn't have any attributes
        /// </summary>
        public static List<SpiderlyProperty> GetAllPropertiesOfTheClass(ClassDeclarationSyntax c, IList<ClassDeclarationSyntax> currentProjectClasses, List<SpiderlyClass> referencedProjectsClasses)
        {
            TypeSyntax baseType = c.BaseList?.Types.FirstOrDefault()?.Type; // BaseClass<long>
            ClassDeclarationSyntax baseClass = GetClass(baseType, currentProjectClasses);

            List<SpiderlyProperty> properties = GetPropsOfCurrentClass(c);

            TypeSyntax typeGeneric = null;

            while (baseType != null)
            {
                if (baseType is GenericNameSyntax genericNameSyntax && baseClass == null)
                {
                    typeGeneric = genericNameSyntax.TypeArgumentList.Arguments.FirstOrDefault(); // long
                    properties.AddRange(GetPropertiesForBaseClasses(baseType.ToString(), typeGeneric.ToString()));
                    break;
                }
                else if (baseClass == null)
                {
                    SpiderlyClass spiderlyBaseClass = referencedProjectsClasses.SingleOrDefault(x => x.Name == c.Identifier.Text);

                    if (spiderlyBaseClass != null)
                        properties.AddRange(spiderlyBaseClass.Properties);

                    break;
                }
                else
                {
                    foreach (PropertyDeclarationSyntax prop in baseClass.Members.OfType<PropertyDeclarationSyntax>())
                    {
                        properties.Add(GetPropWithModifiedT(prop, typeGeneric, baseClass));
                    }
                }

                baseType = baseClass.BaseList?.Types.FirstOrDefault()?.Type;
                baseClass = GetClass(baseType, currentProjectClasses);
            }

            return properties;
        }

        public static List<SpiderlyAttribute> GetAllAttributesOfTheClass(ClassDeclarationSyntax c, IList<ClassDeclarationSyntax> currentProjectClasses, List<SpiderlyClass> allClasses)
        {
            if (c == null) return null;

            ClassDeclarationSyntax cHelper = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ClassDeclaration(c.Identifier).WithBaseList(c.BaseList).WithAttributeLists(c.AttributeLists); // Doing this because of reference type, we don't want to change c
            List<SpiderlyAttribute> attributes = new List<SpiderlyAttribute>();

            TypeSyntax baseType = cHelper.BaseList?.Types.FirstOrDefault()?.Type; // BaseClass
            // Getting the attributes for all base classes also
            do
            {
                attributes.AddRange(cHelper.AttributeLists
                    .SelectMany(x => x.Attributes)
                    .Select(GetSpiderAttribute)
                    .ToList());

                cHelper = currentProjectClasses.SingleOrDefault(x => x.Identifier.Text == baseType?.ToString());

                if (baseType != null && cHelper == null)
                {
                    SpiderlyClass baseClass = allClasses.SingleOrDefault(x => x.Name == c.Identifier.Text || $"{x.Name}DTO" == c.Identifier.Text);

                    if (baseClass != null)
                        attributes.AddRange(baseClass.Attributes);

                    break;
                }

                baseType = cHelper?.BaseList?.Types.FirstOrDefault()?.Type;
            }
            while (baseType != null);

            return attributes;
        }

        /// <summary>
        /// Using this method only when getting all properties of the class, for other situations, we should search SpiderClass.
        /// </summary>
        private static ClassDeclarationSyntax GetClass(TypeSyntax type, IEnumerable<ClassDeclarationSyntax> classes)
        {
            string typeName = "";

            if (type is GenericNameSyntax genericNameSyntax)
            {
                typeName = genericNameSyntax.Identifier.Text; // BaseClass<T>
            }
            else if (type is NameSyntax nameSyntax)
            {
                typeName = nameSyntax.ToString();
            }

            return classes.SingleOrDefault(x => x.Identifier.Text == typeName);
        }

        /// <summary>
        /// Without inherited properties
        /// </summary>
        public static List<SpiderlyProperty> GetPropsOfCurrentClass(ClassDeclarationSyntax c)
        {
            List<SpiderlyProperty> properties = c.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(prop => prop.ExplicitInterfaceSpecifier == null)
                .Select(prop => new SpiderlyProperty()
                {
                    Type = prop.Type.ToString(),
                    Name = prop.Identifier.Text,
                    StringValue = prop.Initializer?.Value?.ToString()?.Trim('"'), // Trimming because: "\"John\"" --> "John"
                    EntityName = c.Identifier.Text,
                    Attributes = prop.AttributeLists
                        .SelectMany(x => x.Attributes)
                        .Select(x =>
                        {
                            return GetSpiderAttribute(x);
                        })
                        .ToList()
                })
                .ToList();

            return properties;
        }

        public static List<SpiderlyMethod> GetMethodsOfCurrentClass(ClassDeclarationSyntax c)
        {
            List<SpiderlyMethod> methods = c.Members.OfType<MethodDeclarationSyntax>()
                .Select(method => new SpiderlyMethod()
                {
                    Name = method.Identifier.Text,
                    ReturnType = method.ReturnType.ToString(),
                    Body = method.Body?.ToString(), // FT: CreateHostBuilder method inside Program.cs has no body
                    Parameters = method.ParameterList.Parameters
                        .Select(parameter => new SpiderParameter
                        {
                            Name = parameter.Identifier.Text,
                            Type = parameter.Type.ToString(),
                            Attributes = parameter.AttributeLists.SelectMany(x => x.Attributes).Select(x => GetSpiderAttribute(x)).ToList()
                        })
                        .ToList(),
                    DescendantNodes = method.DescendantNodes(),
                    Attributes = method.AttributeLists.SelectMany(x => x.Attributes).Select(x => GetSpiderAttribute(x)).ToList()
                })
                .ToList();

            return methods;
        }

        public static string GetDisplayNameProperty(SpiderlyClass entity)
        {
            SpiderlyAttribute entityDisplayNameAttribute = entity.Attributes.SingleOrDefault(x => x.Name == "DisplayName");

            if (entityDisplayNameAttribute != null)
                return entityDisplayNameAttribute.Value;

            SpiderlyProperty displayNamePropForClass = entity.Properties.SingleOrDefault(x => x.Attributes.Any(x => x.Name == Helpers.DisplayNameAttribute));

            if (displayNamePropForClass == null)
                return $"Id.ToString()";

            if (displayNamePropForClass.Type != "string")
                return $"{displayNamePropForClass.Name}.ToString()";

            return displayNamePropForClass.Name;
        }

        internal static SpiderlyAttribute GetSpiderAttribute(AttributeSyntax a)
        {
            string argumentValue = a?.ArgumentList?.Arguments != null && a.ArgumentList.Arguments.Any()
                    ? string.Join(", ", a.ArgumentList.Arguments.Select(arg => arg?.ToString()))
                    : null; // FT: Doing this because of Range(0, 5) (long tail because of null pointer exception)

            argumentValue = GetFormatedAttributeValue(argumentValue);

            return new SpiderlyAttribute
            {
                Name = a.Name.ToString(),
                Value = argumentValue,
            };
        }

        internal static string GetFormatedAttributeValue(string value)
        {
            value = value?.Replace("\"", "").Replace("@", "");

            string pattern = @"nameof\((?:[^.]*\.)?([^.)]*)\)"; // nameof(abc.def.ghi) => ghi
            value = value != null ? Regex.Replace(value, pattern, "$1") : null;

            return value;
        }

        private static SpiderlyProperty GetPropWithModifiedT(PropertyDeclarationSyntax prop, TypeSyntax typeGeneric, ClassDeclarationSyntax baseClass)
        {
            List<SpiderlyAttribute> attributes = GetAllAttributesOfTheMember(prop);
            SpiderlyProperty newProp = new SpiderlyProperty
            {
                Type = prop.Type.ToString(),
                Name = prop.Identifier.Text,
                EntityName = baseClass.Identifier.Text,
                Attributes = attributes,
            };

            if (prop.Type.ToString() == "T") // If some property has type of T, we change it to long for example
            {
                newProp.Type = typeGeneric.ToString();
                return newProp;
            }

            return newProp;
        }

        private static List<SpiderlyAttribute> GetAllAttributesOfTheMember(MemberDeclarationSyntax prop)
        {
            List<SpiderlyAttribute> attributes = new List<SpiderlyAttribute>();
            attributes = prop.AttributeLists
                .SelectMany(x => x.Attributes)
                .Select(GetSpiderAttribute)
                .ToList();
            return attributes;
        }

        private static List<SpiderlyProperty> GetPropertiesForBaseClasses(string typeName, string idType)
        {
            if (typeName.StartsWith($"{Helpers.BusinessObject}"))
            {
                if (typeName.Contains("DTO"))
                {
                    return new List<SpiderlyProperty>()
                    {
                        new SpiderlyProperty{ Type = "int?", Name = "Version" },
                        new SpiderlyProperty{ Type = idType, Name = "Id" },
                        new SpiderlyProperty{ Type = "DateTime?", Name = "CreatedAt" },
                        new SpiderlyProperty{ Type = "DateTime?", Name = "ModifiedAt" },
                    };
                }
                else
                {
                    return new List<SpiderlyProperty>()
                    {
                        new SpiderlyProperty{ Type = "int", Name = "Version" },
                        new SpiderlyProperty{ Type = idType, Name = "Id" },
                        new SpiderlyProperty{ Type = "DateTime", Name = "CreatedAt" },
                        new SpiderlyProperty{ Type = "DateTime", Name = "ModifiedAt" },
                    };
                }
            }
            else if (typeName.StartsWith($"{Helpers.ReadonlyObject}"))
            {
                if (typeName.Contains("DTO"))
                {
                    return new List<SpiderlyProperty>()
                    {
                        new SpiderlyProperty { Type = idType, Name = "Id" },
                        //new SpiderProperty { Type = "DateTime?", IdentifierText = "CreatedAt" },
                    };
                }
                else
                {
                    return new List<SpiderlyProperty>()
                    {
                        new SpiderlyProperty { Type = idType, Name = "Id" },
                        //new SpiderProperty { Type = "DateTime", IdentifierText = "CreatedAt" },
                    };
                }
            }
            else
            {
                return new List<SpiderlyProperty>() { };
            }
        }
    }
}
