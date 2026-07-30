using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using System.Collections.Immutable;
using System.Linq;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// SPIDERLY029: [GenerateCommaSeparatedDisplayName] over a KEYLESS [M2M] junction. The generated table
/// filter matches the collection by child Id, which such a junction does not have, so the shape cannot be
/// supported — the consumer needs to be told which property is at fault.
/// <para>
/// Hosted by <see cref="EntityValidationGenerator"/> like every other entity diagnostic, NOT by
/// <c>PaginatedResultGenerator</c> where the shape actually breaks. It was written there first, and that is
/// the failure <see cref="SpiderlyEntityValidator"/>'s docblock warns about: the diagnostic is delivered by
/// throwing, so it aborted that generator's entire Execute — the Filtering file was never emitted, every
/// generated service lost <c>Build()</c>, and the located error was buried under a wall of CS0103. Here the
/// throw is caught per entity and the rest of the model still generates.
/// </para>
/// </summary>
public class CommaSeparatedDisplayNameDiagnosticTests
{
    private const string KeylessJunctionTarget = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Item : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; }

                [GenerateCommaSeparatedDisplayName]
                public virtual List<ItemWarehouse> ItemWarehouses { get; } = new();
            }

            [SpiderlyEntity]
            public class Warehouse : BusinessObject<byte>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; }

                public virtual List<ItemWarehouse> ItemWarehouses { get; } = new();
            }

            [M2M]
            [SpiderlyEntity]
            public class ItemWarehouse
            {
                public long ItemId { get; set; }
                [M2MWithMany(nameof(Item.ItemWarehouses))]
                public virtual Item Item { get; set; }

                public byte WarehouseId { get; set; }
                [M2MWithMany(nameof(Warehouse.ItemWarehouses))]
                public virtual Warehouse Warehouse { get; set; }

                public int Stock { get; set; }
            }
        }
        """;

    [Fact]
    public void CommaSeparatedDisplayName_OverAKeylessJunction_EmitsSPIDERLY029AtTheProperty()
    {
        ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.Run<EntityValidationGenerator>(KeylessJunctionTarget)
            .GetRunResult().Diagnostics;

        Diagnostic diagnostic = Assert.Single(diagnostics);

        Assert.Equal("SPIDERLY029", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("ItemWarehouses", diagnostic.GetMessage());
        // Located at the consumer's property — the whole point, versus SPIDERLY024's locationless fault.
        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    [Fact]
    public void CommaSeparatedDisplayName_OverAKeyedChild_IsNotReported()
    {
        // Negative control: the same attribute over a child that HAS an id is the attribute's normal use, so
        // the validator must stay silent. Without this, a validator that fired unconditionally would pass the
        // test above and look correct.
        const string keyedTarget = """
            using System.Collections.Generic;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Project : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    public string Name { get; set; }

                    [GenerateCommaSeparatedDisplayName]
                    public virtual List<Member> Members { get; } = new();
                }

                [SpiderlyEntity]
                public class Member : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    public string Email { get; set; }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.Run<EntityValidationGenerator>(keyedTarget)
            .GetRunResult().Diagnostics;

        Assert.DoesNotContain(diagnostics, x => x.Id == "SPIDERLY029");
    }
}
