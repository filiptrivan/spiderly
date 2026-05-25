using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

// Characterization net for NgBaseDetailsGenerator. Base-details was previously untested and is the most
// complex generator; this locks its CURRENT output so the upcoming fragment/shell/config redesign can only
// change the snapshot intentionally. It snapshots the pure BuildBaseDetailsOutput string (the generator
// writes to disk, not AddSource, so the driver snapshot can't see it).
public class NgBaseDetailsGeneratorTests
{
    private static SpiderlyProperty Prop(string name, string type, string entityName, params (string Name, string? Value)[] attributes) =>
        new()
        {
            Name = name,
            Type = type,
            EntityName = entityName,
            Attributes = attributes.Select(a => new SpiderlyAttribute { Name = a.Name, Value = a.Value }).ToList(),
        };

    [Fact]
    public Task CurrentOutput_CommonControlTypes_Characterization()
    {
        SpiderlyClass brand = new()
        {
            Name = "Brand",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                Prop("Name", "string", "Brand"),
                Prop("Price", "decimal", "Brand"),
                Prop("IsActive", "bool?", "Brand"),
            },
        };

        List<SpiderlyClass> entities = new() { brand };

        string result = NgBaseDetailsGenerator.BuildBaseDetailsOutput(
            customDTOClasses: new List<SpiderlyClass>(),
            currentProjectEntities: entities,
            allEntities: entities);

        return Verify(result);
    }
}
