using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using Xunit;

namespace Spiderly.SourceGenerators.Tests;

/// <summary>
/// Sites that declared a non-nullable return and then handed back <c>null!</c> anyway. Each is unreachable
/// through today's call sites, but "unreachable" that is asserted rather than enforced is one refactor away
/// from being wrong — and a returned null here does not fault, it flows into string interpolation and writes
/// the literal text "null" into generated C#.
/// </summary>
public class LatentNullContractTests
{
    [Fact]
    public void GetIdType_OnAManyToManyJunction_ReturnsNull()
    {
        // Making this throw a located diagnostic looked obviously right and was wrong: generators iterate
        // EVERY entity, junctions included, and skip the result on the junction branch — throwing killed
        // ComplexManyToManyList generation outright. Null is the contract, so pin it rather than assert it
        // in a comment.
        SpiderlyClass junction = new()
        {
            Name = "CourseStudent",
            Namespace = "Test.Entities",
            Properties = new List<SpiderlyProperty>(),
            Attributes = new List<SpiderlyAttribute>
            {
                new() { Name = "SpiderlyEntity" },
                new() { Name = "M2M" },
            },
        };

        Assert.Null(junction.GetIdType(new List<SpiderlyClass> { junction }));
    }

    [Fact]
    public void GetValidationTargetSymbol_WithNoType_ReturnsTheUnknownSentinel()
    {
        // Feeds a diagnostic message, so a null would render as "type ''" — the sentinel is the vocabulary
        // the diagnostics in Extensions already use.
        Assert.Equal("<unknown>", AngularTypeMapper.GetValidationTargetSymbol((SpiderlyTypeRef?)null, ImmutableArrayOfNoEnums));
    }

    [Fact]
    public void GenericPropertyOnAnInProjectBaseClass_DoesNotFaultAnalysis()
    {
        // A consumer defining their own generic base entity INSIDE their project: the walk resolves the base
        // class locally, so it takes the branch that never captured the type argument, and a T-typed property
        // on that base dereferences a null. Spiderly's own BusinessObject<T> avoids this only by living in a
        // referenced assembly.
        const string source = """
            namespace TestApp.Business.Entities
            {
                public class OrderBase<T> : BusinessObject<T>
                {
                    public T ExtraKey { get; set; }
                }

                [SpiderlyEntity]
                public class Order : OrderBase<long>
                {
                    public string Name { get; set; }
                }
            }
            """;

        List<ClassDeclarationSyntax> classes = CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

        ClassDeclarationSyntax order = classes.Single(c => c.Identifier.Text == "Order");

        SpiderlyGenerationException exception = Assert.Throws<SpiderlyGenerationException>(
            () => ClassAnalyzer.GetAllPropertiesOfTheClass(order, classes, new List<SpiderlyClass>()));

        Assert.Equal("SPIDERLY027", exception.Diagnostic.Id);
        Assert.Contains("ExtraKey", exception.Diagnostic.GetMessage());
    }

    private static System.Collections.Immutable.ImmutableArray<string> ImmutableArrayOfNoEnums =>
        System.Collections.Immutable.ImmutableArray<string>.Empty;
}
