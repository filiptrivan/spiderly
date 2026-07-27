using System.Collections.Immutable;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Characterization tests pinning the C#-type -> Angular-target dispatch in the (otherwise un-snapshotted)
/// Angular generators. They lock the CURRENT output of every scalar bucket so a later refactor that
/// centralizes the bucket membership (SpiderlyTypeRef.ScalarKind) is provably behavior-preserving.
/// The four methods deliberately group the types differently (e.g. DateOnly is "string" for the TS type
/// but "date" for the table filter) — these tests encode exactly those differences.
/// </summary>
public class AngularTypeDispatchCharacterizationTests
{
    private static readonly ImmutableArray<string> Enums = ImmutableArray.Create("MyEnum");

    private static SpiderlyProperty Prop(string type) => new() { Name = "P", Type = type };

    [Theory]
    [InlineData("string", "string")]
    [InlineData("bool", "boolean")]
    [InlineData("bool?", "boolean")]
    [InlineData("DateTime", "Date")]
    [InlineData("DateTime?", "Date")]
    [InlineData("DateOnly", "string")]
    [InlineData("DateOnly?", "string")]
    [InlineData("TimeOnly", "string")]
    [InlineData("int", "number")]
    [InlineData("int?", "number")]
    [InlineData("long", "number")]
    [InlineData("decimal?", "number")]
    [InlineData("double", "number")]
    [InlineData("byte", "number")]
    [InlineData("MyEnum", "MyEnum")]
    // C# nullability must never reach emitted TS — `MyEnum?` is invalid TypeScript in type position.
    [InlineData("MyEnum?", "MyEnum")]
    [InlineData("List<MyEnum>", "MyEnum[]")]
    [InlineData("List<MyEnum>?", "MyEnum[]")]
    [InlineData("List<long>", "number[]")]
    [InlineData("UserDTO", "User")]
    [InlineData("List<UserDTO>", "User[]")]
    [InlineData("Guid", "any")]
    // Transport wrappers are unwrapped to the awaited body; collections under them keep their "[]".
    [InlineData("Task<UserDTO>", "User")]
    [InlineData("Task<List<UserDTO>>", "User[]")]
    [InlineData("Task<List<MyEnum>>", "MyEnum[]")]
    [InlineData("ActionResult<List<UserDTO>>", "User[]")]
    [InlineData("Task<PaginatedResultDTO<UserDTO>>", "PaginatedResult<User>")]
    [InlineData("Task<List<NamebookDTO<long>>>", "Namebook[]")]
    [InlineData("Task<string>", "string")]
    [InlineData("Task<int>", "number")]
    [InlineData("ValueTask<List<UserDTO>>", "User[]")]
    [InlineData("IActionResult", "any")]
    // Bare special-form DTOs (unwrapped variants of the Task<...> rows above).
    [InlineData("PaginatedResultDTO<UserDTO>", "PaginatedResult<User>")]
    [InlineData("NamebookDTO<long>", "Namebook")]
    [InlineData("CodebookDTO<long>", "Codebook")]
    [InlineData("LazyLoadSelectedIdsResultDTO", "LazyLoadSelectedIdsResult")]
    // A nullable DTO reference must not leak the C# '?' into TS (same TS17019 class as nullable enums).
    [InlineData("UserDTO?", "User")]
    // A user DTO whose name merely CONTAINS a framework special form is that user's type, not the
    // framework one — the legacy parser's Contains sniffing silently collapses it.
    [InlineData("BrandNamebookDTO", "BrandNamebook")]
    [InlineData("ProductPaginatedResultDTO", "ProductPaginatedResult")]
    // The PaginatedResult type argument is a TS type, so a scalar argument maps like any other
    // scalar — the legacy parser emitted the raw C# name ("PaginatedResult<long>").
    [InlineData("PaginatedResultDTO<long>", "PaginatedResult<number>")]
    public void GetAngularType(string cSharp, string expected)
        => Assert.Equal(expected, AngularTypeMapper.GetAngularType(cSharp, Enums));

    [Theory]
    [InlineData("string", UIControlTypeCodes.TextBox)]
    [InlineData("bool", UIControlTypeCodes.CheckBox)]
    [InlineData("bool?", UIControlTypeCodes.CheckBox)]
    [InlineData("DateTime", UIControlTypeCodes.Calendar)]
    [InlineData("DateOnly?", UIControlTypeCodes.Calendar)]
    [InlineData("TimeOnly", UIControlTypeCodes.Calendar)]
    [InlineData("decimal", UIControlTypeCodes.Decimal)]
    [InlineData("double?", UIControlTypeCodes.Decimal)]
    [InlineData("int", UIControlTypeCodes.Integer)]
    [InlineData("long?", UIControlTypeCodes.Integer)]
    [InlineData("byte", UIControlTypeCodes.Integer)]
    [InlineData("Guid", UIControlTypeCodes.None)]
    public void GetUIControlType(string type, UIControlTypeCodes expected)
        => Assert.Equal(expected, NgDetailsPropertyBlockGenerator.GetUIControlType(Prop(type)));

    [Theory]
    [InlineData("string", "text")]
    [InlineData("bool", "boolean")]
    [InlineData("bool?", "boolean")]
    [InlineData("DateTime", "date")]
    [InlineData("DateOnly?", "date")]
    [InlineData("TimeOnly", "text")]
    [InlineData("TimeOnly?", "text")]
    [InlineData("int", "numeric")]
    [InlineData("decimal?", "numeric")]
    [InlineData("long", "numeric")]
    [InlineData("byte?", "numeric")]
    [InlineData("Guid", null)]
    public void GetTableColFilterType(string type, string expected)
        => Assert.Equal(expected, NgDetailsDataGenerator.GetTableColFilterType(Prop(type)));

    [Theory]
    [InlineData("DateTime", ", showMatchModes: true")]
    [InlineData("DateOnly?", ", showMatchModes: true")]
    [InlineData("TimeOnly", ", showMatchModes: true")]
    [InlineData("decimal", ", showMatchModes: true")]
    [InlineData("double?", ", showMatchModes: true")]
    [InlineData("int", ", showMatchModes: true")]
    [InlineData("long?", ", showMatchModes: true")]
    [InlineData("byte", ", showMatchModes: true")]
    [InlineData("string", null)]
    [InlineData("bool", null)]
    [InlineData("Guid", null)]
    public void GetTableColAdditionalProperties(string type, string expected)
        => Assert.Equal(expected, NgDetailsDataGenerator.GetTableColAdditionalProperties(Prop(type), new SpiderlyClass()));
}
