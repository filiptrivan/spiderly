using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using System.Collections.Immutable;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Each test runs <see cref="EntityValidationGenerator"/> over a minimal entity graph that exhibits
/// exactly one [WithOne] one-to-one violation, then asserts the matching SPIDERLY### diagnostic is emitted.
/// It surfaces SPIDERLY019-022 via <c>SpiderlyEntityValidator.Validate</c>; the thrown
/// <c>SpiderlyGenerationException</c> is caught per entity and reported as a diagnostic.
/// <para>
/// These used to run <c>MapperGenerator</c>, which hosted the validators — so they also silently depended
/// on a <c>[SpiderlyDataMapper]</c> class being present in every fixture, and would have gone quiet for any
/// consumer who disabled that generator. The fixtures still declare one because the pipeline collects the
/// same categories, but validation no longer rides on mapper emission.
/// </para>
/// </summary>
public class OneToOneDiagnosticsTests
{
    private static void AssertEmits(string expectedId, string source)
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<EntityValidationGenerator>(source);
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

    [Fact]
    public void SelfReferential_IsStillReported_WhenEveryMapperPairIsHandWritten()
    {
        // The second hole the move closed, in its measured shape. These diagnostics used to be reached only
        // through MapperGenerator.GetToDTOConfig, which early-returns on HasCustomPair — so hand-writing all
        // THREE *ToDTOConfig pairs exempted the entity from one-to-one and foreign-key validation entirely.
        // Hand-writing a mapper is a mapping decision, not a waiver on entity shape.
        //
        // All three, not one: that helper is called three times per entity, so a single custom pair still
        // left two calls reaching the validator. The redundant invocation was accidentally masking this —
        // which means collapsing it to once per entity would have OPENED the hole had validation stayed
        // inside the mapper. Moving it out is what makes running it once safe.
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
                public partial class Mapper
                {
                    public static TypeAdapterConfig NodeToDTOConfig() => null;
                    public static TypeAdapterConfig NodeProjectToConfig() => null;
                    public static TypeAdapterConfig NodeExcelProjectToConfig() => null;
                }
            }
            """;

        AssertEmits("SPIDERLY022", source);
    }
}
