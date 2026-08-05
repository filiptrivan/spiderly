using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Pins that multi-control M2M selections on a [UIOrderedOneToMany] CHILD are applied when the child is
/// saved through its PARENT's save — the only save path the generated UI actually uses for ordered
/// children (their multiselects render inline on the parent form; nothing routes to the child's own
/// standalone save). Regression: PACMS IntegrationRuleGroup.Brands/Categories/Tags — the admin selected
/// brands on an integration's rule group, Save returned 200, and the selections were silently dropped:
/// the ordered-children loop saved the child via the scalars-only Save{Child}AndReturnDTO and emitted the
/// ComplexManyToManyList updates but not the MultiSelect/MultiAutocomplete ones, and its response DTO
/// omitted the id lists, so the form also visually cleared the selection on save.
/// </summary>
public class OrderedOneToManyChildM2MTests
{
    /// <summary>
    /// The PACMS shape, reduced: ordered child (BoardLane) carrying one MultiSelect M2M (Labels) and one
    /// MultiAutocomplete M2M (Members), each over a keyless [M2M] junction.
    /// </summary>
    private const string OrderedChildWithMultiControlM2MSource = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Board : BusinessObject<long>
            {
                [DisplayName]
                public string Name { get; set; }

                [UIOrderedOneToMany]
                public virtual List<BoardLane> Lanes { get; } = new();
            }

            [SpiderlyEntity]
            public class BoardLane : BusinessObject<long>
            {
                [UIDoNotGenerate]
                [Required]
                public int OrderNumber { get; set; }

                [DisplayName]
                public string Name { get; set; }

                public long BoardId { get; set; }
                [Required]
                [CascadeDelete]
                [WithMany(nameof(Board.Lanes))]
                public virtual Board Board { get; set; }

                [UIControlType(nameof(UIControlTypeCodes.MultiSelect))]
                public virtual List<Label> Labels { get; } = new();

                [UIControlType(nameof(UIControlTypeCodes.MultiAutocomplete))]
                public virtual List<Member> Members { get; } = new();
            }

            [SpiderlyEntity]
            public class Label : BusinessObject<int>
            {
                [DisplayName]
                public string Name { get; set; }

                public virtual List<BoardLane> Lanes { get; } = new();
            }

            [SpiderlyEntity]
            public class Member : BusinessObject<long>
            {
                [DisplayName]
                public string Name { get; set; }

                public virtual List<BoardLane> Lanes { get; } = new();
            }

            [M2M]
            [SpiderlyEntity]
            public class BoardLaneLabel
            {
                [M2MWithMany(nameof(BoardLane.Labels))]
                public virtual BoardLane BoardLane { get; set; }

                [M2MWithMany(nameof(Label.Lanes))]
                public virtual Label Label { get; set; }
            }

            [M2M]
            [SpiderlyEntity]
            public class BoardLaneMember
            {
                [M2MWithMany(nameof(BoardLane.Members))]
                public virtual BoardLane BoardLane { get; set; }

                [M2MWithMany(nameof(Member.Lanes))]
                public virtual Member Member { get; set; }
            }
        }
        """;

    /// <summary>
    /// The behavioral pin, asserted as call-graph reachability over the emitted services rather than as
    /// emitted-text matching, so it survives reshaping the template (direct Update* calls in the loop and
    /// delegation to Save{Child}AndReturnMainUIFormDTO both pass; dropping the selections cannot).
    /// </summary>
    [Theory]
    [InlineData("UpdateLabelsForBoardLane")]
    [InlineData("UpdateMembersForBoardLane")]
    public void OrderedChildM2MSelections_AreAppliedOnTheParentOrderedSavePath(string junctionUpdateMethod)
    {
        GeneratorRunResult result = GeneratorTestHarness.Run<ServicesGenerator>(OrderedChildWithMultiControlM2MSource)
            .GetRunResult().Results.Single();

        Dictionary<string, HashSet<string>> callGraph = BuildCallGraph(result);

        Assert.True(
            Reaches(callGraph, from: "UpdateOrderedLanesForBoard", to: junctionUpdateMethod),
            $"The ordered-children save path (UpdateOrderedLanesForBoard) never reaches {junctionUpdateMethod} — " +
            "the child's multi-control M2M selections sent in its SaveBodyDTO are silently dropped.");
    }

    /// <summary>
    /// The form repopulates from the save response, so besides persisting, the ordered path's response
    /// must carry the child's M2M id lists — otherwise a successful save visually clears the selection.
    /// The assignment is not a call, so this one is pinned on the method's own text: however the ordered
    /// path produces its result (building the DTO itself or delegating), the selections must flow into it.
    /// </summary>
    [Fact]
    public void OrderedChildM2MSelections_AreEchoedInTheSaveResponse()
    {
        GeneratorRunResult result = GeneratorTestHarness.Run<ServicesGenerator>(OrderedChildWithMultiControlM2MSource)
            .GetRunResult().Results.Single();

        Dictionary<string, string> methodBodies = GetMethodBodies(result);

        HashSet<string> reachable = ReachableFrom(BuildCallGraph(result), "UpdateOrderedLanesForBoard");
        bool responseCarriesSelections = reachable
            .Where(methodBodies.ContainsKey)
            // MultiSelect echoes as {Property}Ids, MultiAutocomplete as {Property}NamebookDTOList.
            .Any(method => methodBodies[method].Contains("LabelsIds = ") && methodBodies[method].Contains("MembersNamebookDTOList = "));

        Assert.True(
            responseCarriesSelections,
            "No method on the ordered-children save path assigns LabelsIds/MembersNamebookDTOList into the " +
            "returned MainUIFormDTO — a successful save would repopulate the form with cleared selections.");
    }

    [Fact]
    public Task OrderedChildWithMultiControlM2M_EmittedServices()
    {
        var driver = GeneratorTestHarness.Run<ServicesGenerator>(OrderedChildWithMultiControlM2MSource);
        return Verify(driver);
    }

    #region Call-graph plumbing

    private static Dictionary<string, HashSet<string>> BuildCallGraph(GeneratorRunResult result)
    {
        Dictionary<string, HashSet<string>> graph = new();

        foreach (MethodDeclarationSyntax method in GeneratedMethods(result))
        {
            HashSet<string> callees = graph.TryGetValue(method.Identifier.Text, out HashSet<string>? existing)
                ? existing
                : graph[method.Identifier.Text] = new();

            foreach (InvocationExpressionSyntax invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string? callee = invocation.Expression switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.Text,
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                    _ => null,
                };

                if (callee != null)
                    callees.Add(callee);
            }
        }

        return graph;
    }

    private static Dictionary<string, string> GetMethodBodies(GeneratorRunResult result)
        => GeneratedMethods(result)
            // Overloads collapse onto one key; concatenating keeps the assertion conservative (any overload counts).
            .GroupBy(x => x.Identifier.Text)
            .ToDictionary(x => x.Key, x => string.Concat(x.Select(m => m.ToString())));

    private static IEnumerable<MethodDeclarationSyntax> GeneratedMethods(GeneratorRunResult result)
        => result.GeneratedSources
            .Select(source => CSharpSyntaxTree.ParseText(source.SourceText.ToString()))
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>());

    private static bool Reaches(Dictionary<string, HashSet<string>> graph, string from, string to)
        => ReachableFrom(graph, from).Contains(to);

    private static HashSet<string> ReachableFrom(Dictionary<string, HashSet<string>> graph, string start)
    {
        HashSet<string> visited = new();
        Queue<string> queue = new();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            if (visited.Add(current) == false || graph.TryGetValue(current, out HashSet<string>? callees) == false)
                continue;

            foreach (string callee in callees)
                queue.Enqueue(callee);
        }

        return visited;
    }

    #endregion
}
