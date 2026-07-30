using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// The dependent side of a one-to-one (the entity carrying <c>[WithOne]</c> + the FK scalar) is
/// treated identically to a many-to-one nav (design decision D4): its DTO flattens to
/// <c>{Nav}Id</c> + <c>{Nav}DisplayName</c>, and the Mapster config maps the DisplayName from the
/// principal's <c>[DisplayName]</c> property. Before this support, <c>OwningTaskItem</c> fell through
/// the Scalar branch and emitted a raw <c>public TaskItem OwningTaskItem</c> DTO member with no
/// DisplayName — wrong. These tests pin the flattened shape and guard against the explicit-FK
/// <c>{Nav}Id</c> column being double-emitted (once by the 1-1 branch, once by the scalar branch).
/// </summary>
public class OneToOneDependentDtoTests
{
    // Valid bidirectional 1-1: Conversation (dependent, [WithOne] + explicit FK scalar) <-> TaskItem
    // (principal, bare inverse nav with [DisplayName] = Title). A [SpiderlyDataMapper] class is included
    // so the full DTO + mapper path runs.
    private const string ValidBidirectionalOneToOne = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Conversation : BusinessObject<long>
            {
                public long? OwningTaskItemId { get; set; }

                [WithOne(nameof(TaskItem.Conversation))]
                public virtual TaskItem OwningTaskItem { get; set; }
            }

            [SpiderlyEntity]
            public class TaskItem : BusinessObject<long>
            {
                [DisplayName]
                public string Title { get; set; }

                public virtual Conversation Conversation { get; set; }
            }
        }

        namespace TestApp.Business.DataMappers
        {
            [SpiderlyDataMapper]
            public partial class Mapper { }
        }
        """;

    // DTO shape: the dependent ConversationDTO must flatten OwningTaskItem into OwningTaskItemId
    // (the FK scalar, exactly once) + OwningTaskItemDisplayName, with NO raw TaskItem nav member.
    [Fact]
    public void DependentNav_IsFlattenedToIdAndDisplayName()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<EntitiesToDTOGenerator>(ValidBidirectionalOneToOne);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("DTOList.generated.cs"));
        string dtoSource = generated.ToString();

        // The dependent DTO must exist (proves the generator ran to completion).
        Assert.Contains("class ConversationDTO", dtoSource);

        // Flattened M2O-shaped columns: FK scalar + DisplayName.
        Assert.Contains("public long? OwningTaskItemId { get; set; }", dtoSource);
        Assert.Contains("public string OwningTaskItemDisplayName { get; set; }", dtoSource);

        // No raw navigation member leaking into the DTO.
        Assert.DoesNotContain("TaskItem OwningTaskItem ", dtoSource);

        // The explicit FK scalar must appear exactly once — not duplicated by the scalar branch
        // AND the 1-1 branch both emitting OwningTaskItemId.
        int idCount = CountOccurrences(dtoSource, "public long? OwningTaskItemId { get; set; }");
        Assert.Equal(1, idCount);
    }

    // Mapper shape: the dependent gets the M2O-style .Map(...) lines, with DisplayName sourced from
    // the principal's [DisplayName] property (TaskItem.Title).
    [Fact]
    public void DependentNav_EmitsManyToOneStyleMapperLines()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<MapperGenerator>(ValidBidirectionalOneToOne);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("Mapper.generated.cs"));
        string mapperSource = generated.ToString();

        // DisplayName mapped from the principal's [DisplayName] prop (Title), through the nav. `!` because
        // the nav is optional and both Mapster and EF handle a null chain; it is erased at compile time.
        Assert.Contains(".Map(dest => dest.OwningTaskItemDisplayName, src => src.OwningTaskItem!.Title)", mapperSource);

        // FK mapped from the explicit scalar directly (avoids EF's spurious JOIN on src.Nav.Id).
        Assert.Contains(".Map(dest => dest.OwningTaskItemId, src => src.OwningTaskItemId)", mapperSource);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index)) != -1)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
