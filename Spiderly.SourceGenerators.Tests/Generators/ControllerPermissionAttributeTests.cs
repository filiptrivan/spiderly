using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class ControllerPermissionAttributeTests
{
    // Pins the security-relevant decision behind the controller's boundary authorization: an authorize-able
    // entity yields [HasPermission("{Crud}{Entity}")] on its CRUD actions, while a [DoNotAuthorize] entity
    // yields nothing (public endpoints). A dropped/wrong attribute is a silent authz hole.
    //
    // This pins the emission *logic* (Helpers.GetPermissionAttribute) rather than a full controller snapshot:
    // ControllerGenerator gates on a ".WebAPI" calling path that GeneratorTestHarness does not supply, so it
    // emits nothing under the harness. End-to-end generated-controller output is exercised by the CI e2e job.

    [Fact]
    public void AuthorizeableEntity_EmitsHasPermissionAttribute()
    {
        SpiderlyClass entity = new() { Name = "Product" };

        Assert.Equal("[HasPermission(\"ReadProduct\")]\n        ", Helpers.GetPermissionAttribute(entity, "Read"));
        Assert.Equal("[HasPermission(\"DeleteProduct\")]\n        ", Helpers.GetPermissionAttribute(entity, "Delete"));
    }

    [Fact]
    public void DoNotAuthorizeEntity_EmitsNothing()
    {
        SpiderlyClass entity = new()
        {
            Name = "PublicLookup",
            Attributes = new() { new SpiderlyAttribute { Name = "DoNotAuthorize" } },
        };

        Assert.Equal("", Helpers.GetPermissionAttribute(entity, "Read"));
        Assert.Equal("", Helpers.GetPermissionAttribute(entity, "Delete"));
    }
}
