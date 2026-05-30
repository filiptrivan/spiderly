using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Task 1.8 — the one-to-one dependent must be treated identically to a many-to-one nav across the
/// UI, save-hydration, FluentValidation, and FK-validation sites (design decision D4/D5), not just in
/// the DTO/mapper/cascade generators where the <c>|| IsOneToOneType()</c> reuse already lived.
///
/// Before this fix a valid bidirectional 1-1 rendered a BROKEN admin page: the dependent nav resolved
/// to <c>UIControlType.None</c> (no autocomplete control, no <c>{Nav}Id</c> form binding), its backend
/// autocomplete endpoint was never generated, a shadow-FK dependent silently dropped its FK on save,
/// the required dependent got no NotEmpty validation rule, and the dependent FK skipped the
/// SPIDERLY004/006 type/nullability checks. These tests pin the M2O-equivalent behavior.
/// </summary>
public class OneToOneUiSaveValidationTests
{
    // Valid bidirectional 1-1 with an EXPLICIT FK scalar (OwningTaskItemId): Conversation (dependent,
    // [WithOne]) <-> TaskItem (principal, bare inverse nav with [DisplayName] = Title). Includes a
    // [SpiderlyService] so the Net save/read path generates, and a [SpiderlyDataMapper] so the DTO +
    // mapper path runs.
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

        namespace TestApp.Business.Services
        {
            [SpiderlyService] public class ConversationService : ConversationServiceGenerated { }
            [SpiderlyService] public class TaskItemService : TaskItemServiceGenerated { }
        }
        """;

    // Same shape but the dependent uses a SHADOW FK (no OwningTaskItemId scalar). The DTO still
    // flattens to {Nav}Id (a synthesized column), and the save path must hydrate the nav from
    // dto.OwningTaskItemId — otherwise the FK is silently dropped on insert/update.
    private const string ShadowFkOneToOne = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Conversation : BusinessObject<long>
            {
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

        namespace TestApp.Business.Services
        {
            [SpiderlyService] public class ConversationService : ConversationServiceGenerated { }
            [SpiderlyService] public class TaskItemService : TaskItemServiceGenerated { }
        }
        """;

    // The dependent nav as the analyzer would shape it: a single reference type carrying [WithOne].
    // GetUIControlType / GetFormControlName depend only on IsOneToOneType()/IsManyToOneType(), which
    // read this attribute + the type — so the resolvers can be unit-tested directly, no disk emit.
    private static SpiderlyProperty DependentNav() => new()
    {
        Name = "OwningTaskItem",
        Type = "TaskItem",
        Attributes = new List<SpiderlyAttribute>
        {
            new() { Name = "WithOne", Value = "TaskItem.Conversation" },
        },
    };

    // -----------------------------------------------------------------------
    // Issue 1 — the dependent resolves to an Autocomplete bound to {Nav}Id, NOT UIControlType.None.
    // These are the two resolvers that drive the Angular base-details control emission. A None here
    // is what surfaced as the broken admin page (the "Unknown UIControlType" sentinel + no control).
    // -----------------------------------------------------------------------
    [Fact]
    public void DependentNav_ResolvesToAutocompleteControl()
    {
        Assert.Equal(UIControlTypeCodes.Autocomplete, NgDetailsPropertyBlockGenerator.GetUIControlType(DependentNav()));
    }

    [Fact]
    public void DependentNav_FormControlBindsToFkName()
    {
        // Must bind to the FK the DTO actually carries (owningTaskItemId), exactly like an M2O —
        // not to the raw nav name (owningTaskItem), which the DTO never exposes.
        Assert.Equal("owningTaskItemId", NgDetailsPropertyBlockGenerator.GetFormControlName(DependentNav()));
    }

    // Issue 1 (table filter) — the dependent's grid column filters as text, like an M2O DisplayName.
    [Fact]
    public void DependentNav_TableColFilterIsText()
    {
        Assert.Equal("text", NgDetailsDataGenerator.GetTableColFilterType(DependentNav()));
    }

    // Issue 1 (backend endpoint) — the autocomplete read method for the dependent must be generated,
    // so the admin autocomplete actually has a data source to query.
    [Fact]
    public void DependentNav_GeneratesAutocompleteServiceMethod()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<ServicesGenerator>(ValidBidirectionalOneToOne);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("ConversationService.generated.cs"));
        string service = generated.ToString();

        Assert.Contains("GetOwningTaskItemAutocompleteListForConversation", service);
    }

    // -----------------------------------------------------------------------
    // Issue 2 — the save path hydrates the dependent's nav from dto.{Nav}Id, exactly like an M2O.
    // For the EXPLICIT-FK dependent this proves the FK is referenced (parity with M2O FindAsync);
    // for the SHADOW-FK dependent it proves the FK isn't silently dropped on save.
    // -----------------------------------------------------------------------
    [Fact]
    public void ExplicitFkDependent_HydratesNavFromDtoFkOnSave()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<ServicesGenerator>(ValidBidirectionalOneToOne);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("ConversationService.generated.cs"));
        string service = generated.ToString();

        Assert.Contains("if (dto.OwningTaskItemId > 0)", service);
        Assert.Contains("poco.OwningTaskItem = await GetInstanceAsync<", service);
    }

    [Fact]
    public void ShadowFkDependent_HydratesNavFromDtoFkOnSave()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<ServicesGenerator>(ShadowFkOneToOne);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("ConversationService.generated.cs"));
        string service = generated.ToString();

        // Even without an explicit FK scalar, the synthesized DTO column dto.OwningTaskItemId must
        // be used to hydrate the nav — the FK must NOT be dropped on save.
        Assert.Contains("if (dto.OwningTaskItemId > 0)", service);
        Assert.Contains("poco.OwningTaskItem = await GetInstanceAsync<", service);
    }

    // -----------------------------------------------------------------------
    // Issue 3 — a [Required] 1-1 dependent gets a NotEmpty FluentValidation rule, mapped to its FK
    // ({Nav}Id), exactly like a [Required] M2O. Before the fix the dependent (which has [WithOne],
    // not [WithMany]) fell through GetRulePropertyName and emitted no NotEmpty rule.
    // -----------------------------------------------------------------------
    [Fact]
    public void RequiredDependent_EmitsNotEmptyRuleOnFk()
    {
        const string requiredDependent = """
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Conversation : BusinessObject<long>
                {
                    public long? OwningTaskItemId { get; set; }

                    [Required]
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

        GeneratorDriver driver = GeneratorTestHarness.Run<FluentValidationGenerator>(requiredDependent);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("ValidationRules.generated.cs"));
        string rules = generated.ToString();

        // The NotEmpty rule must target the flattened FK (OwningTaskItemId), not the raw nav.
        Assert.Contains("RuleFor(x => x.OwningTaskItemId).NotEmpty();", rules);
        Assert.DoesNotContain("RuleFor(x => x.OwningTaskItem)", rules);
    }

    // -----------------------------------------------------------------------
    // Issue 4 — running a VALID bidirectional 1-1 through BOTH the DTO generator and the mapper
    // generator must yield ZERO SPIDERLY-prefixed error diagnostics. The mapper generator now also
    // runs ForeignKeyValidator over the dependent's FK; this confirms that adding the dependent to
    // FK validation does not introduce a (double-)diagnostic on a valid 1-1.
    // -----------------------------------------------------------------------
    [Fact]
    public void ValidBidirectionalOneToOne_EmitsNoSpiderlyErrors_AcrossDtoAndMapper()
    {
        GeneratorDriver dtoDriver = GeneratorTestHarness.Run<EntitiesToDTOGenerator>(ValidBidirectionalOneToOne);
        GeneratorDriver mapperDriver = GeneratorTestHarness.Run<MapperGenerator>(ValidBidirectionalOneToOne);

        var dtoErrors = dtoDriver.GetRunResult().Diagnostics
            .Where(d => d.Id.StartsWith("SPIDERLY") && d.Severity == DiagnosticSeverity.Error);
        var mapperErrors = mapperDriver.GetRunResult().Diagnostics
            .Where(d => d.Id.StartsWith("SPIDERLY") && d.Severity == DiagnosticSeverity.Error);

        Assert.Empty(dtoErrors);
        Assert.Empty(mapperErrors);
    }
}
