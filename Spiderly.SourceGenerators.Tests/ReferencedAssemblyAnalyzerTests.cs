using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests;

/// <summary>
/// Covers the referenced-assembly (metadata) path — entities in a referenced project, as in PACMS.WebAPI
/// referencing PACMS.Business. The inline generator tests only exercise the in-project syntax path, so this
/// symbol-to-string conversion was untested and shipped two bugs:
///   1. a generic base <c>BusinessObject&lt;long&gt;</c> was mangled to <c>BusinessObject&lt;long</c> (closing
///      bracket stripped), so <see cref="Extensions.GetIdType"/> read the PK type as "BusinessObject" and threw a
///      false SPIDERLY018 for every referenced entity;
///   2. a <c>List&lt;Foo&gt;</c> one-to-many property collapsed to <c>Foo</c> (collection-ness lost), latent only
///      because the build died on bug #1 first.
/// Both are asserted directly on the extracted strings, plus the end-to-end <c>GetIdType</c> result.
/// </summary>
public class ReferencedAssemblyAnalyzerTests
{
    // Compiles to its own assembly and is referenced by the main compilation, so the analyzer reads it as metadata
    // symbols. It must compile on its own, hence the inline marker attribute and base class.
    private const string ReferencedSource = """
        using System.Collections.Generic;

        public class SpiderlyEntityAttribute : System.Attribute { }

        public class BusinessObject<T>
        {
            public T Id { get; set; }
        }

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Brand : BusinessObject<long>
            {
                public string Name { get; set; }
                public List<Product> Products { get; set; }
            }

            [SpiderlyEntity]
            public class Product : BusinessObject<long>
            {
                public string Name { get; set; }
                public Brand Brand { get; set; }
            }
        }
        """;

    // The analyzer only reads referenced assemblies, so the main compilation just needs to compile.
    private const string MainSource = "namespace MainApp { public class Anchor { } }";

    private static List<SpiderlyClass> GetReferencedEntities()
    {
        Compilation compilation = GeneratorTestHarness.CreateCompilationWithReference(MainSource, ReferencedSource);

        return ReferencedAssemblyAnalyzer.GetClassesFromCompilation(
            compilation,
            new List<ClassCategoryCodes> { ClassCategoryCodes.Entities });
    }

    [Fact]
    public void GenericBase_KeepsClosingBracket_AndKeywordKeyType()
    {
        SpiderlyClass brand = GetReferencedEntities().Single(c => c.Name == "Brand");

        Assert.Equal("BusinessObject<long>", brand.BaseType);
    }

    [Fact]
    public void OneToManyProperty_KeepsCollectionType()
    {
        SpiderlyClass brand = GetReferencedEntities().Single(c => c.Name == "Brand");

        SpiderlyProperty products = brand.Properties.Single(p => p.Name == "Products");
        Assert.Equal("List<Product>", products.Type.Raw);
    }

    [Fact]
    public void GetIdType_OnReferencedEntity_ResolvesInsteadOfThrowingSPIDERLY018()
    {
        List<SpiderlyClass> entities = GetReferencedEntities();
        SpiderlyClass brand = entities.Single(c => c.Name == "Brand");

        Assert.Equal("long", brand.GetIdType(entities));
    }

    [Fact]
    public void ReferencedAssemblyComparer_TreatsEquivalentMetadataAsEqual()
    {
        List<SpiderlyClass> first = GetReferencedEntities();
        List<SpiderlyClass> second = GetReferencedEntities();

        Assert.True(ReferencedSpiderlyClassListComparer.Instance.Equals(first, second));
        Assert.Equal(
            ReferencedSpiderlyClassListComparer.Instance.GetHashCode(first),
            ReferencedSpiderlyClassListComparer.Instance.GetHashCode(second));
    }
}
