using System.Collections.Generic;
using System.Collections.Immutable;
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

        Assert.Null(junction.GetIdTypeOrNull(new List<SpiderlyClass> { junction }));
    }

    /// <summary>
    /// "Has no primary key" and "forgot to declare a base class" are different facts, and
    /// <c>GetIdTypeOrNull</c> keys its junction early-return on <c>IsManyToMany()</c>, which is literally
    /// <c>BaseType == null</c>. So a consumer who simply omits <c>: BusinessObject&lt;long&gt;</c> is judged a
    /// junction, and the <c>SPIDERLY010</c> throw at the BOTTOM of that method — which exists for exactly
    /// this and says the right thing — is unreachable for the case it was written for. Wrapped by
    /// SPIDERLY024, the consumer reads "This is a bug in Spiderly — please report it" for their own typo.
    /// </summary>
    [Fact]
    public void GetIdType_OnAnEntityThatForgotItsBase_ReportsTheMissingBase_NotAJunction()
    {
        SpiderlyClass forgotBase = new()
        {
            Name = "ForgotBase",
            Namespace = "Test.Entities",
            Properties = new List<SpiderlyProperty>(),
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        };

        SpiderlyGenerationException exception = Assert.Throws<SpiderlyGenerationException>(
            () => forgotBase.GetIdType(new List<SpiderlyClass> { forgotBase }));

        Assert.Equal(SpiderlyDiagnostics.EntityMissingBusinessObjectBase.Id, exception.Diagnostic.Id);
    }

    /// <summary>
    /// A DECLARED junction can still carry a key — the repo's own e2e fixture <c>ProjectMember</c> is
    /// <c>[M2M]</c> and <c>BusinessObject&lt;long&gt;</c>. Green today; it is the guard that stops the fix for
    /// the test above from being "key the early-return on [M2M] instead", which would null out a real id.
    /// </summary>
    [Fact]
    public void GetIdType_OnAKeyedManyToManyJunction_ReturnsItsDeclaredIdType()
    {
        SpiderlyClass keyedJunction = new()
        {
            Name = "ProjectMember",
            Namespace = "Test.Entities",
            BaseType = "BusinessObject<long>",
            Properties = new List<SpiderlyProperty>(),
            Attributes = new List<SpiderlyAttribute>
            {
                new() { Name = "SpiderlyEntity" },
                new() { Name = "M2M" },
            },
        };

        Assert.Equal("long", keyedJunction.GetIdType(new List<SpiderlyClass> { keyedJunction }));
    }

    [Fact]
    public void GetValidationTargetSymbol_WithNoType_ReturnsNull()
    {
        // Declared string? rather than handing back a sentinel: the one caller already skips reporting when
        // it cannot name a type, and a sentinel would bypass that guard and raise SPIDERLY001 instead.
        Assert.Null(AngularTypeMapper.GetValidationTargetSymbol((SpiderlyTypeRef?)null, ImmutableArray<string>.Empty));
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
}
