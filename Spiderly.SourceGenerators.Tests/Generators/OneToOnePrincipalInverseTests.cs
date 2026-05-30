using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// A valid bidirectional one-to-one has the dependent side carry <c>[WithOne]</c> and the principal
/// side expose a bare reference nav (no <c>[WithMany]</c>, no <c>[WithOne]</c>). That bare principal
/// inverse is M2O-shaped, so the M2O machinery used to misclassify it: SPIDERLY015 ("missing
/// [WithMany]") fired and the build broke for a perfectly valid 1-1; the DTO would also gain a bogus
/// <c>Conversation</c>/<c>ConversationId</c>/<c>ConversationDisplayName</c> column for an FK that lives
/// on the dependent, not the principal. These tests pin both the diagnostic and the DTO shape.
///
/// SPIDERLY015 is surfaced by <see cref="EntitiesToDTOGenerator"/> (it calls
/// <c>Validations.ValidateWithManyAttributes</c> before emitting the DTO list); when that validation
/// fails the generator returns early and emits no DTO source at all. So the DTO-shape test below also
/// implicitly depends on SPIDERLY015 staying silent.
/// </summary>
public class OneToOnePrincipalInverseTests
{
    // Valid bidirectional 1-1: Conversation (dependent, [WithOne] + FK scalar) <-> TaskItem (principal,
    // bare inverse nav). A [SpiderlyDataMapper] class is included so the full validation/DTO path runs.
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

    // Test A (the build-breaker): the valid 1-1 must NOT emit SPIDERLY015 (or any SPIDERLY error).
    // FAILS before the fix because the bare principal inverse TaskItem.Conversation is classified as a
    // many-to-one missing [WithMany].
    [Fact]
    public void ValidBidirectionalOneToOne_DoesNotEmitMissingWithMany()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<EntitiesToDTOGenerator>(ValidBidirectionalOneToOne);
        var diagnostics = driver.GetRunResult().Diagnostics;

        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIDERLY015");
        Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("SPIDERLY") && d.Severity == DiagnosticSeverity.Error));
    }

    // Test B (DTO shape): the principal DTO must NOT flatten its bare inverse nav into
    // Conversation / ConversationId / ConversationDisplayName columns. The DTO is only emitted once
    // SPIDERLY015 is silenced, so this also exercises the validation carve-out.
    [Fact]
    public void PrincipalInverse_IsExcludedFromGeneratedDTO()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<EntitiesToDTOGenerator>(ValidBidirectionalOneToOne);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("DTOList.generated.cs"));
        string dtoSource = generated.ToString();

        // The principal DTO must exist (proves the generator ran to completion, not aborted by SPIDERLY015)...
        Assert.Contains("class TaskItemDTO", dtoSource);

        // ...but must carry no flattened column for the bare principal inverse nav.
        Assert.DoesNotContain("Conversation ", dtoSource);
        Assert.DoesNotContain("ConversationId", dtoSource);
        Assert.DoesNotContain("ConversationDisplayName", dtoSource);
    }
}
