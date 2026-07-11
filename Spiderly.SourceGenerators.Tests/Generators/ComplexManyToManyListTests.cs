using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Pins the ComplexManyToManyList service template. The generated Get/GetDefault methods
/// deliberately emit placeholder junction DTOs (null FKs, null additional columns) for every
/// other-side entity without a record, and the generated form posts them back verbatim — so the
/// generated Update method must treat all-null rows as "no record" and must never read the
/// current-side FK from the DTO (the id parameter is the truth). Regression: PACMS SaveProduct
/// 500'd with "Nullable object must have a value" on dto.ProductVariantId.Value for exactly
/// such a placeholder row.
/// </summary>
public class ComplexManyToManyListTests
{
    [Fact]
    public Task JunctionWithSingleAdditionalField_UpdateSkipsPlaceholderRowsAndIgnoresDtoParentFk()
    {
        const string source = """
            using System.Collections.Generic;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Item : BusinessObject<long>
                {
                    [DisplayName]
                    public string Name { get; set; }

                    [ComplexManyToManyList]
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

        var driver = GeneratorTestHarness.Run<ServicesGenerator>(source);
        return Verify(driver);
    }

    [Fact]
    public void JunctionWithoutAdditionalFields_ReportsSPIDERLY023()
    {
        // Data-less junction: the update method couldn't tell a linked row from a placeholder
        // (both carry only FKs), so the generator must refuse the shape instead of emitting
        // link-everything semantics — simple many-to-many is the correct attribute there.
        const string source = """
            using System.Collections.Generic;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Playlist : BusinessObject<long>
                {
                    [DisplayName]
                    public string Name { get; set; }

                    [ComplexManyToManyList]
                    public virtual List<PlaylistSong> PlaylistSongs { get; } = new();
                }

                [SpiderlyEntity]
                public class Song : BusinessObject<long>
                {
                    [DisplayName]
                    public string Name { get; set; }

                    public virtual List<PlaylistSong> PlaylistSongs { get; } = new();
                }

                [M2M]
                [SpiderlyEntity]
                public class PlaylistSong
                {
                    public long PlaylistId { get; set; }
                    [M2MWithMany(nameof(Playlist.PlaylistSongs))]
                    public virtual Playlist Playlist { get; set; }

                    public long SongId { get; set; }
                    [M2MWithMany(nameof(Song.PlaylistSongs))]
                    public virtual Song Song { get; set; }
                }
            }
            """;

        var driver = GeneratorTestHarness.Run<ServicesGenerator>(source);

        var diagnostic = driver.GetRunResult().Diagnostics.Single(d => d.Id == "SPIDERLY023");
        Assert.Contains("PlaylistSong", diagnostic.GetMessage());
        Assert.Contains("simple many-to-many", diagnostic.GetMessage());
    }

    [Fact]
    public Task JunctionWithMultipleAdditionalFields_UpdateGuardsRequiredFieldsWith422NotNullCrash()
    {
        const string source = """
            using System.Collections.Generic;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Course : BusinessObject<long>
                {
                    [DisplayName]
                    public string Name { get; set; }

                    [ComplexManyToManyList]
                    public virtual List<CourseStudent> CourseStudents { get; } = new();
                }

                [SpiderlyEntity]
                public class Student : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    public string Name { get; set; }

                    public virtual List<CourseStudent> CourseStudents { get; } = new();
                }

                [M2M]
                [SpiderlyEntity]
                public class CourseStudent
                {
                    public long CourseId { get; set; }
                    [M2MWithMany(nameof(Course.CourseStudents))]
                    public virtual Course Course { get; set; }

                    public long StudentId { get; set; }
                    [M2MWithMany(nameof(Student.CourseStudents))]
                    public virtual Student Student { get; set; }

                    public int Grade { get; set; }

                    public string Note { get; set; }
                }
            }
            """;

        var driver = GeneratorTestHarness.Run<ServicesGenerator>(source);
        return Verify(driver);
    }
}
