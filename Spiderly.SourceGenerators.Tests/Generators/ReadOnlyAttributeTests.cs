using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// [ReadOnly] marks a server-owned property: the client may read it (it stays in the read
/// {Entity}DTO and in the list table), but can never write it. The read DTO is doubly used —
/// it's the GET response AND the write model nested inside {Entity}SaveBodyDTO — so a server-owned
/// scalar can't be removed from the write path by dropping it from the SaveBody DTO (scalars aren't
/// direct SaveBody members; they live in the nested read DTO). [ReadOnly] therefore works by:
///   - skipping the property in generated inbound validation (no unsatisfiable NotEmpty),
///   - emitting a Mapster .Ignore() so a payload can't write it,
///   - keeping it in the read DTO (display + API response),
///   - hiding its editable control in the details form while keeping its table column.
/// Motivating case: a DiscountCode.TimesUsed usage counter, incremented only by backend code.
/// See spiderly#337.
/// </summary>
public class ReadOnlyAttributeTests
{
    // A DiscountCode with an editable Code and a server-owned TimesUsed counter. [Required] on
    // TimesUsed documents the non-null DB column; [ReadOnly] says the client never supplies it.
    // Service + DataMapper markers so the Net save/read + mapper paths generate.
    private const string DiscountCodeWithReadOnlyCounter = """
        using System.Collections.Generic;
        using System.ComponentModel.DataAnnotations;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class DiscountCode : BusinessObject<int>
            {
                [DisplayName]
                [Required]
                [StringLength(50, MinimumLength = 1)]
                public string Code { get; set; }

                [ReadOnly]
                [Required]
                public int TimesUsed { get; set; }
            }
        }

        namespace TestApp.Business.DataMappers
        {
            [SpiderlyDataMapper]
            public partial class Mapper { }
        }

        namespace TestApp.Business.Services
        {
            [SpiderlyService] public class DiscountCodeService : DiscountCodeServiceGenerated { }
        }
        """;

    // A [ReadOnly] property must emit NO inbound FluentValidation rule — not even the NotEmpty that
    // [Required] would otherwise generate. The generated form never sends it, so a rule on it is
    // structurally unsatisfiable (the original spiderly#337 incident: every save 422'd). The
    // editable Code property must still be validated — proves the skip is scoped to [ReadOnly].
    [Fact]
    public void ReadOnly_Property_EmitsNoInboundValidationRule()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<FluentValidationGenerator>(DiscountCodeWithReadOnlyCounter);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("ValidationRules.generated.cs"));
        string rules = generated.ToString();

        Assert.DoesNotContain("RuleFor(x => x.TimesUsed)", rules);
        Assert.Contains("RuleFor(x => x.Code).NotEmpty()", rules);
    }

    // A [ReadOnly] scalar maps DTO->entity by Mapster convention (same name) unless ignored, so a
    // hand-crafted payload could overwrite the server-owned counter. The generated DTO->entity
    // config must .Ignore() it. (This replaces the hand-written .Ignore() consumers write today.)
    [Fact]
    public void ReadOnly_Property_IgnoredInDtoToEntityMapper()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<MapperGenerator>(DiscountCodeWithReadOnlyCounter);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("Mapper.generated.cs"));
        string mapper = generated.ToString();

        Assert.Contains(".Ignore(dest => dest.TimesUsed)", mapper);
    }

    // [ReadOnly] is not [ExcludeFromDTO]: the property stays in the read {Entity}DTO so it's still
    // returned by GET endpoints and shown in the list table. Only the WRITE path is closed.
    [Fact]
    public void ReadOnly_Property_KeptInReadDto()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<EntitiesToDTOGenerator>(DiscountCodeWithReadOnlyCounter);

        SyntaxTree generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("DTOList.generated.cs"));
        string dtos = generated.ToString();

        // Force-nullable scalar in the read DTO (GetFormatedDTOPropertyType: int -> int?), [Required]
        // notwithstanding — see that method for why value types stay nullable.
        Assert.Contains("public int? TimesUsed", dtos);
    }

    // A [ReadOnly] scalar has no editable control in the details form: with the write path closed
    // (mapper .Ignore) and no validation, an editable field would silently discard the admin's input.
    // IsIncludedInDetailsUi is the single gate every details-UI sub-generator honors, so excluding it
    // here drops the control everywhere. The value still reaches the admin via the read DTO (list
    // view / API). A plain scalar without [ReadOnly] must still be included — proves the scope.
    [Fact]
    public void ReadOnly_Scalar_ExcludedFromEditableDetailsForm()
    {
        SpiderlyProperty readOnlyProp = new()
        {
            Name = "TimesUsed",
            Type = "int",
            Attributes = new List<SpiderlyAttribute>
            {
                new() { Name = "ReadOnly" },
                new() { Name = "Required" },
            },
        };
        SpiderlyProperty editableProp = new() { Name = "Code", Type = "string" };
        SpiderlyClass entity = new()
        {
            Name = "DiscountCode",
            Properties = new List<SpiderlyProperty> { readOnlyProp, editableProp },
        };

        Assert.False(readOnlyProp.IsIncludedInDetailsUi(entity));
        Assert.True(editableProp.IsIncludedInDetailsUi(entity));
    }
}
