using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Shared
{
    public static class Helpers
    {
        public static string DisplayNameAttribute { get; set; } = "DisplayName";
        public static string BusinessObject { get; set; } = "BusinessObject";
        public static string ReadonlyObject { get; set; } = "ReadonlyObject";
        public static string EntitiesNamespaceEnding { get; set; } = "Entities";
        public static string DTONamespaceEnding { get; set; } = "DTO";

        public static List<string> BaseClassNames { get; set; } = new()
        {
            "Filter",
            "LazyTableSelection",
            "Namebook",
            "Codebook",
            "SimpleSaveResult",
            "BusinessObject",
            "ReadonlyObject",
            "ExcelReportOptions",
            "UserRole",
            "PaginatedResult",
            "FilterRule",
            "FilterSortMeta",
            "LazyLoadSelectedIdsResult",
            "EmailVerifyUI",
            "ApiError"
        };

        #region Source Generator

        public static string[] GetNamespacePartsWithoutLastElement(string namespaceValue)
        {
            string[] namespaceParts = namespaceValue.Split('.');
            string[] namespacePartsWithoutLastElement = namespaceParts.Take(namespaceParts.Length - 1).ToArray();

            return namespacePartsWithoutLastElement; // eg. Spiderly, Generator, Security
        }

        public static string GetBasePartOfNamespace(string namespaceValue)
        {
            return string.Join(".", GetNamespacePartsWithoutLastElement(namespaceValue));  // eg. Spiderly.Security
        }

        public static string GetProjectName(string namespaceValue)
        {
            string[] namespacePartsWithoutLastElement = GetNamespacePartsWithoutLastElement(namespaceValue);

            return namespacePartsWithoutLastElement[namespacePartsWithoutLastElement.Length - 1]; // eg. Security
        }

        public static List<SpiderlyProperty> GetCascadeDeleteProperties(string entityName, List<SpiderlyClass> entities)
        {
            return entities
                .SelectMany(x => x.Properties)
                .Where(prop =>
                    prop.IsManyToOneType() &&
                    prop.Attributes.Any(x => x.Name == "CascadeDelete") &&
                    prop.Type == entityName
                )
                .ToList();
        }

        /// <summary>
        /// List<long> -> long
        /// </summary>
        public static string ExtractTypeFromGenericType(string input)
        {
            if (input == null)
                return null;

            string[] parts = input.Split('<'); // List, long>
            string result = parts.Last().Replace(">", "");

            return result;
        }

        public static SpiderlyProperty GetOppositeManyToManyProperty(SpiderlyProperty oneToManyProperty, SpiderlyClass extractedPropertyEntity, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            if (oneToManyProperty.Name == "Tags")
            {

            }
            SpiderlyClass manyToManyEntity = GetManyToManyEntityWithAttributeValue(oneToManyProperty.Name, entity, entities); // Categories, Product => ProductCategory

            if (manyToManyEntity == null)
                return null;

            List<SpiderlyProperty> m2mWithManyProperties = manyToManyEntity.Properties
                .Where(x => x.Attributes.Any(x => x.Name == "M2MWithMany"))
                .ToList();

            if (m2mWithManyProperties.Count != 2)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ManyToManyRequiresExactlyTwoWithMany,
                    manyToManyEntity.Location,
                    manyToManyEntity.Name, m2mWithManyProperties.Count);
            }

            SpiderlyProperty m2mWithManyOppositeProperty = m2mWithManyProperties // Category
                .Single(x => x.Attributes
                    .Any(x => x.Name == "M2MWithMany" && x.Value != oneToManyProperty.Name));

            string propertyName = m2mWithManyOppositeProperty.Attributes.Where(x => x.Name == "M2MWithMany").Select(x => x.Value).Single(); // Products

            return extractedPropertyEntity.Properties.SingleOrDefault(x => x.Name == propertyName); // List<Product> Products
        }

        /// <param name="entity">Main entity from which we get one to many property</param>
        public static SpiderlyClass GetManyToManyEntityWithAttributeValue(string attributeValue, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            return entities
                .SingleOrDefault(x => x.HasM2MAttribute() && x.Properties
                    .Any(x => x.Type == entity.Name && x.Attributes
                        .Any(x => x.Name == "M2MWithMany" && x.Value == attributeValue)));
        }

        #endregion

        #region Angular

        public static List<SpiderlyProperty> GetUIOrderedOneToManyProperties(SpiderlyClass entity)
        {
            return entity.Properties.Where(x => x.Attributes.Any(x => x.Name == "UIOrderedOneToMany")).ToList();
        }

        #endregion

        #region Permissions

        public static List<SpiderlyEnumItem> GetEnumItems(EnumDeclarationSyntax enume)
        {
            List<SpiderlyEnumItem> enumMembers = new();

            foreach (EnumMemberDeclarationSyntax member in enume.Members)
            {
                string name = member.Identifier.Text;
                string value = member.EqualsValue != null ? member.EqualsValue.Value.ToString() : null;
                enumMembers.Add(new SpiderlyEnumItem { Name = name, Value = value });
            }

            return enumMembers;
        }

        public static List<string> GetPermissionCodesForEntites(List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyClass entity in entities)
            {
                result.Add($"Read{entity.Name}");
                result.Add($"Update{entity.Name}");
                result.Add($"Insert{entity.Name}");
                result.Add($"Delete{entity.Name}");
            }

            return result;
        }

        public static string GetAuthorizeEntityMethodName(string entityName, CrudCodes crudCode)
        {
            return $"Authorize{entityName}{crudCode}AndThrow";
        }

        public static bool ShouldAuthorizeEntity(SpiderlyClass entity)
        {
            return !entity.HasDoNotAuthorizeAttribute();
        }

        public static string GetShouldAuthorizeEntityString(SpiderlyClass entity)
        {
            return ShouldAuthorizeEntity(entity).ToString().ToLower();
        }

        #endregion

        #region Mapper

        /// <summary>
        /// Getting non generated partial mapper class.
        /// </summary>
        public static SpiderlyClass GetManualyWrittenMapperClass(List<SpiderlyClass> classes)
        {
            return classes
                .Where(x => x.HasSpiderlyDataMapperAttribute())
                .SingleOrDefault(); // FT: It should allways be only one or none
        }

        #endregion

        #region Blobs

        public static List<SpiderlyProperty> GetBlobProperties(List<SpiderlyProperty> properties)
        {
            return properties.Where(x => x.Attributes.Any(x => x.Name == "BlobName")).ToList();
        }

        public static List<SpiderlyProperty> GetEditorImageProperties(List<SpiderlyProperty> properties)
        {
            return properties
                .Where(x => x.IsEditorControlType() && x.HasS3PublicUrlAttribute())
                .ToList();
        }

        #endregion

        #region Entity Lookup

        public static SpiderlyClass GetEntityByPropertyType(SpiderlyProperty property, List<SpiderlyClass> entities)
        {
            return entities.SingleOrDefault(x => x.Name == ExtractTypeFromGenericType(property.Type));
        }

        #endregion

        #region Namespace

        public static string GetAppName(string namespaceValue)
        {
            return namespaceValue.Split('.')[0];
        }

        #endregion

        #region Populate hacks

        // FT: Maybe ill need it in the future, for now im using only for the current class
        //private static List<SpiderMethod> GetAllMethodsOfTheClass(ClassDeclarationSyntax c, IEnumerable<ClassDeclarationSyntax> allClasses,)
        //{
        //    TypeSyntax baseType = c.BaseList?.Types.FirstOrDefault()?.Type; //BaseClass<long>
        //    ClassDeclarationSyntax baseClass = GetClass(baseType, allClasses);

        //    string s = c.Identifier.Text;

        //    List<SpiderMethod> properties = GetMethodsOfCurrentClass(c);

        //    TypeSyntax typeGeneric = null;

        //    while (baseType != null)
        //    {
        //        baseType = baseClass.BaseList?.Types.FirstOrDefault()?.Type;
        //        baseClass = GetClass(baseType, allClasses);
        //    }

        //    return properties;
        //}

        #endregion

        #region Helpers

        public static void WriteToTheFile(string data, string path)
        {
            if (data != null)
            {
                data = data.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
                using StreamWriter sw = new StreamWriter(path, false);
                sw.Write(data);
            }
        }

        public static void WriteToTheFile(StringBuilder data, string path)
        {
            if (data != null)
            {
                WriteToTheFile(data.ToString(), path);
            }
        }

        #endregion
    }
}


