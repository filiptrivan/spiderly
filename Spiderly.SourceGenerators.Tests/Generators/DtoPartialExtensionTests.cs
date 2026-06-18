using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using Xunit;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// A hand-written `partial class {Entity}DTO` that extends a generated DTO must contribute its members to
/// codegen even when the author forgot `[SpiderlyDTO]`. Before the fix the unmarked partial was silently
/// dropped from every artifact generator (entities.generated.ts, validators, ...): the field compiled and
/// serialized but never reached the generated frontend type. (spiderly#258)
/// </summary>
public class DtoPartialExtensionTests
{
    /// <summary>
    /// Collection half: an unmarked `partial class {X}DTO` must be enrolled for the DTO category, so it
    /// reaches <see cref="SpiderlyClassFactory.GetDTOClasses"/>. A *non-partial* unmarked `*DTO` is NOT
    /// enrolled — a standalone DTO still needs `[SpiderlyDTO]`.
    /// </summary>
    [Fact]
    public void IsClassSyntaxTargetForGeneration_UnmarkedPartialDtoClass_IsEnrolledForDtoCategory()
    {
        List<ClassCategoryCodes> dtoCategory = new() { ClassCategoryCodes.DTO };

        Assert.True(SyntaxTargets("public partial class OrderItemDTO { public int ProductId { get; set; } }"));
        Assert.False(SyntaxTargets("public class StandaloneDTO { public int X { get; set; } }"));   // non-partial, unmarked -> still needs [SpiderlyDTO]
        Assert.False(SyntaxTargets("public partial class OrderItem { public int Quantity { get; set; } }")); // not a *DTO

        bool SyntaxTargets(string source)
        {
            ClassDeclarationSyntax node = CSharpSyntaxTree.ParseText(source)
                .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            return PipelineFactory.IsClassSyntaxTargetForGeneration(node, dtoCategory);
        }
    }

    /// <summary>
    /// Merge half: given an entity (which generates {Entity}DTO) plus an UNMARKED `partial class {Entity}DTO`
    /// that adds a property, <see cref="SpiderlyClassFactory.GetDTOClasses"/> must surface that property under
    /// the DTO's name — the same already-handled "two same-named entries" shape a `[SpiderlyDTO]` partial produces.
    /// </summary>
    [Fact]
    public void GetDTOClasses_UnmarkedPartialExtensionOfGeneratedDTO_MergesItsProperties()
    {
        SpiderlyClass entity = new()
        {
            Name = "OrderItem",
            Namespace = "Test.Business.Entities",
            BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty> { new() { Name = "Quantity", Type = "int" } },
        };

        // Hand-written extension that adds ProductId but FORGOT [SpiderlyDTO].
        SpiderlyClass unmarkedPartial = new()
        {
            Name = "OrderItemDTO",
            Namespace = "Test.Business.DTO",
            Attributes = new List<SpiderlyAttribute>(),
            Properties = new List<SpiderlyProperty> { new() { Name = "ProductId", Type = "int?" } },
        };

        List<SpiderlyClass> input = new() { entity, unmarkedPartial };

        List<SpiderlyClass> dtoClasses = SpiderlyClassFactory.GetDTOClasses(input, input);

        // NgEntitiesGenerator groups DTO entries by name and concatenates their properties; the merged
        // OrderItemDTO must therefore contain ProductId.
        List<string> mergedOrderItemDtoProps = dtoClasses
            .Where(d => d.Name == "OrderItemDTO")
            .SelectMany(d => d.Properties)
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("ProductId", mergedOrderItemDtoProps);
    }
}
