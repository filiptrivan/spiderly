using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// <c>[Required]</c> means what ASP.NET Core's <c>RequiredAttribute</c> means: it refuses null, and on a
/// string also an empty or whitespace string. It never refuses <c>false</c>, <c>0</c>, a date or
/// <c>[]</c>, which are legal answers. A many-to-one relation's id also refuses <c>0</c>, because no row
/// has that id. Each shape is pinned on the server rule (<see cref="FluentValidationGenerator"/>) and the
/// client rule (<see cref="NgValidatorsGenerator"/>).
/// </summary>
public class RequiredAttributeValidationTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.ComponentModel.DataAnnotations;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Shipment : BusinessObject<long>
            {
                [Required]
                public bool IsFragile { get; set; }

                [Required]
                public int ParcelCount { get; set; }

                [Required]
                public decimal Weight { get; set; }

                [Required]
                public DateTime PickupDate { get; set; }

                [Required]
                [StringLength(100, MinimumLength = 1)]
                public string Recipient { get; set; }

                public long CourierId { get; set; }
                [Required]
                [WithMany(nameof(Courier.Shipments))]
                public virtual Courier Courier { get; set; }
            }

            [SpiderlyEntity]
            public class Courier : BusinessObject<long>
            {
                [DisplayName]
                public string Name { get; set; }

                public virtual List<Shipment> Shipments { get; } = new();
            }
        }

        namespace TestApp.Business.DTO
        {
            [SpiderlyDTO]
            public class ShipmentBatchDTO
            {
                [Required]
                public List<long> ShipmentIds { get; set; } = new();
            }
        }
        """;

    // A [Required] ordered list, which the admin saves through its parent.
    private const string RequiredOrderedListSource = """
        using System.Collections.Generic;
        using System.ComponentModel.DataAnnotations;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Route : BusinessObject<long>
            {
                [DisplayName]
                public string Name { get; set; }

                [Required]
                [UIOrderedOneToMany]
                public virtual List<RouteStop> Stops { get; } = new();
            }

            [SpiderlyEntity]
            public class RouteStop : BusinessObject<long>
            {
                [UIDoNotGenerate]
                [Required]
                public int OrderNumber { get; set; }

                [DisplayName]
                public string Name { get; set; }

                public long RouteId { get; set; }
                [Required]
                [CascadeDelete]
                [WithMany(nameof(Route.Stops))]
                public virtual Route Route { get; set; }
            }
        }

        namespace TestApp.Business.DataMappers
        {
            [SpiderlyDataMapper]
            public partial class Mapper { }
        }

        namespace TestApp.Business.Services
        {
            [SpiderlyService] public class RouteService : RouteServiceGenerated { }
            [SpiderlyService] public class RouteStopService : RouteStopServiceGenerated { }
        }
        """;

    private static string GeneratedFile<TGenerator>(string source, string fileNameEnding)
        where TGenerator : IIncrementalGenerator, new()
    {
        GeneratorDriver driver = GeneratorTestHarness.Run<TGenerator>(source);

        return driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith(fileNameEnding))
            .ToString();
    }

    private static SpiderlyAttribute Attribute(string name, string? value = null) => new() { Name = name, Value = value };

    private static SpiderlyProperty Property(string name, string type, params SpiderlyAttribute[] attributes) =>
        new() { Name = name, Type = type, Attributes = attributes.ToList() };

    // What a .WebAPI project reads from its referenced Business assembly: the entity with its attributes,
    // and the compiled DTOs, whose value types are nullable and whose relation is flattened to {Nav}Id.
    private static string ClientValidatorsFile() => NgValidatorsGenerator.GetValidatorsFileContent(
    [
        new SpiderlyClass
        {
            Name = "Shipment",
            Attributes = [Attribute("SpiderlyEntity")],
            Properties =
            [
                Property("IsFragile", "bool", Attribute("Required")),
                Property("ParcelCount", "int", Attribute("Required")),
                Property("Weight", "decimal", Attribute("Required")),
                Property("PickupDate", "DateTime", Attribute("Required")),
                Property("Recipient", "string", Attribute("Required"), Attribute("StringLength", "100, MinimumLength=1")),
                Property("CourierId", "long"),
                Property("Courier", "Courier", Attribute("Required"), Attribute("WithMany", "Shipments")),
            ],
        },
        new SpiderlyClass
        {
            Name = "ShipmentDTO",
            Attributes = [Attribute("SpiderlyDTO")],
            Properties =
            [
                Property("IsFragile", "bool?"),
                Property("ParcelCount", "int?"),
                Property("Weight", "decimal?"),
                Property("PickupDate", "DateTime?"),
                Property("Recipient", "string"),
                Property("CourierId", "long?"),
                Property("CourierDisplayName", "string"),
            ],
        },
        new SpiderlyClass
        {
            Name = "ShipmentBatchDTO",
            Attributes = [Attribute("SpiderlyDTO")],
            Properties = [Property("ShipmentIds", "List<long>", Attribute("Required"))],
        },
    ]);

    private static string ClientValidator(string file, string methodName)
    {
        int start = file.IndexOf($"    {methodName} = (control: SpiderlyFormControl)", StringComparison.Ordinal);
        Assert.True(start >= 0, $"No client validator {methodName} in:\n{file}");

        return file[start..file.IndexOf("return validator;", start, StringComparison.Ordinal)];
    }

    // The check a client form runs for a missing value: null, undefined, or the '' of an empty input.
    // false, 0 and a date pass it.
    private const string ClientRequiredCheck = "const notEmptyRule = typeof value !== 'undefined' && value !== null && value !== '';";

    [Theory]
    [InlineData("IsFragile")]
    [InlineData("ParcelCount")]
    [InlineData("Weight")]
    [InlineData("PickupDate")]
    [InlineData("ShipmentIds")]
    public void RequiredNonString_ServerRefusesOnlyNull(string property)
    {
        string rules = GeneratedFile<FluentValidationGenerator>(Source, "ValidationRules.generated.cs");

        Assert.Contains($"RuleFor(x => x.{property}).NotNull();", rules);
    }

    [Fact]
    public void RequiredString_ServerRefusesAnEmptyOrWhitespaceString()
    {
        string rules = GeneratedFile<FluentValidationGenerator>(Source, "ValidationRules.generated.cs");

        Assert.Contains("RuleFor(x => x.Recipient).NotEmpty().Length(1, 100);", rules);
    }

    [Fact]
    public void RequiredRelation_ServerRefusesIdZero()
    {
        string rules = GeneratedFile<FluentValidationGenerator>(Source, "ValidationRules.generated.cs");

        Assert.Contains("RuleFor(x => x.CourierId).NotEmpty();", rules);
    }

    [Theory]
    [InlineData("isFragileShipmentValidator")]
    [InlineData("parcelCountShipmentValidator")]
    [InlineData("weightShipmentValidator")]
    [InlineData("pickupDateShipmentValidator")]
    [InlineData("courierIdShipmentValidator")]
    public void RequiredNonString_ClientRefusesOnlyAMissingValue(string methodName)
    {
        string validator = ClientValidator(ClientValidatorsFile(), methodName);

        Assert.Contains(ClientRequiredCheck, validator);
        Assert.Contains("const valid = notEmptyRule;", validator);
        // The translation key a consumer already ships, and the required marker the form shows.
        Assert.Contains("translate('NotEmpty', {})", validator);
        Assert.Contains("control.required = true;", validator);
    }

    [Fact]
    public void RequiredString_ClientRefusesAnEmptyString()
    {
        string validator = ClientValidator(ClientValidatorsFile(), "recipientShipmentValidator");

        Assert.Contains(ClientRequiredCheck, validator);
        Assert.Contains("const valid = notEmptyRule && stringLengthRule;", validator);
    }

    // A form array is never null, so the client has no check for a [Required] list.
    [Fact]
    public void RequiredList_ClientAcceptsAnEmptyList()
    {
        string file = ClientValidatorsFile();

        Assert.DoesNotContain("shipmentIdsShipmentBatch", file);
        Assert.DoesNotContain("isFormArrayEmpty", file);
    }

    [Fact]
    public void RequiredOrderedList_SaveAcceptsAnEmptyList()
    {
        string rules = GeneratedFile<FluentValidationGenerator>(RequiredOrderedListSource, "ValidationRules.generated.cs");
        string service = GeneratedFile<ServicesGenerator>(RequiredOrderedListSource, "RouteService.generated.cs");

        Assert.Contains("RuleFor(x => x.OrderedStopsSaveBodyDTO).NotNull();", rules);
        Assert.Contains("UpdateOrderedStopsForRoute", service);
        Assert.DoesNotContain("orderedItemIds.Count == 0", service);
    }
}
