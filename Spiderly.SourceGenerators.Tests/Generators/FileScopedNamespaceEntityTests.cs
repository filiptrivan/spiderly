using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using Xunit;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// <c>namespace Foo;</c> parses to <c>FileScopedNamespaceDeclarationSyntax</c>, which does NOT derive from
/// <c>NamespaceDeclarationSyntax</c> — both derive from <c>BaseNamespaceDeclarationSyntax</c>. Namespace
/// resolution filtered on the block-scoped type alone, so a file-scoped entity resolved to no namespace at
/// all. Everything Spiderly scaffolds uses block-scoped namespaces, which is why this never showed up; but
/// file-scoped is what an IDE writes by default for a hand-added entity, and the two forms are otherwise
/// interchangeable C#.
/// </summary>
public class FileScopedNamespaceEntityTests
{
    private const string FileScopedSource = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities;

        [SpiderlyEntity]
        public class Item : BusinessObject<long>
        {
            [DisplayName]
            public string Name { get; set; }
        }
        """;

    private const string BlockScopedSource = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Item : BusinessObject<long>
            {
                [DisplayName]
                public string Name { get; set; }
            }
        }
        """;

    [Fact]
    public void FileScopedNamespace_DoesNotFaultTheGenerator()
    {
        GeneratorRunResult result = GeneratorTestHarness.Run<PaginatedResultGenerator>(FileScopedSource)
            .GetRunResult().Results.Single();

        // Assert on the diagnostic, not on result.Exception: SPIDERLY024 now catches escapes, so an
        // exception-only assertion would pass while the generator still dies and emits nothing.
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SPIDERLY024");
        Assert.Null(result.Exception);
    }

    [Fact]
    public void FileScopedNamespace_EmitsTheSameOutputAsBlockScoped()
    {
        // The two declaration forms are the same program, so they must generate the same code — including
        // the namespace the generated Filtering class is emitted into.
        Assert.Equal(GeneratedFrom(BlockScopedSource), GeneratedFrom(FileScopedSource));
    }

    private static string GeneratedFrom(string source) =>
        GeneratorTestHarness.Run<PaginatedResultGenerator>(source).GetRunResult().Results.Single()
            .GeneratedSources.Single().SourceText.ToString();
}
