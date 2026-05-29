using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using System.Collections.Immutable;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Each test runs <see cref="MapperGenerator"/> over a minimal entity graph that exhibits exactly
/// one [WithOne] one-to-one violation, then asserts the matching SPIDERLY### diagnostic is emitted.
/// The MapperGenerator surfaces SPIDERLY019-022 because it calls
/// <c>OneToOneValidator.ValidateEntity</c> (beside <c>ForeignKeyValidator.ValidateEntity</c>) during
/// mapper-config generation; the thrown <c>SpiderlyGenerationException</c> is caught per-entity and
/// reported as a diagnostic. A <c>[SpiderlyDataMapper]</c> class is required in each fixture so the
/// generator reaches the validation path.
/// </summary>
public class OneToOneDiagnosticsTests
{
    private static void AssertEmits(string expectedId, string source)
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<MapperGenerator>(source);
        ImmutableArray<Diagnostic> diagnostics = driver.GetRunResult().Diagnostics;

        Assert.Contains(diagnostics, d => d.Id == expectedId);
    }

    [Fact]
    public void OneToOneOnBothSides_EmitsSPIDERLY019()
    {
        // Principal side (TaskItem.Conversation) ALSO carries [WithOne] — illegal, exactly one side may.
        const string source = """
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

                    [WithOne(nameof(Conversation.OwningTaskItem))]
                    public virtual Conversation Conversation { get; set; }
                }
            }

            namespace TestApp.Business.DataMappers
            {
                [SpiderlyDataMapper]
                public partial class Mapper { }
            }
            """;

        AssertEmits("SPIDERLY019", source);
    }

    [Fact]
    public void InverseNavNotFound_EmitsSPIDERLY020()
    {
        // [WithOne] points at an inverse nav name that does not exist on the principal.
        const string source = """
            using System.Collections.Generic;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Conversation : BusinessObject<long>
                {
                    public long? OwningTaskItemId { get; set; }

                    [WithOne("DoesNotExist")]
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

        AssertEmits("SPIDERLY020", source);
    }

    [Fact]
    public void RequiredOnPrincipal_EmitsSPIDERLY021()
    {
        // [Required] on the principal-side nav is unenforceable for a 1-1.
        const string source = """
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;

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

                    [Required]
                    public virtual Conversation Conversation { get; set; }
                }
            }

            namespace TestApp.Business.DataMappers
            {
                [SpiderlyDataMapper]
                public partial class Mapper { }
            }
            """;

        AssertEmits("SPIDERLY021", source);
    }

    [Fact]
    public void SelfReferential_EmitsSPIDERLY022()
    {
        // [WithOne] targets the declaring entity — self-referential 1-1 is unsupported.
        const string source = """
            using System.Collections.Generic;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Node : BusinessObject<long>
                {
                    [DisplayName]
                    public string Name { get; set; }

                    public long? OtherId { get; set; }

                    [WithOne(nameof(Node.Other))]
                    public virtual Node Other { get; set; }
                }
            }

            namespace TestApp.Business.DataMappers
            {
                [SpiderlyDataMapper]
                public partial class Mapper { }
            }
            """;

        AssertEmits("SPIDERLY022", source);
    }
}
