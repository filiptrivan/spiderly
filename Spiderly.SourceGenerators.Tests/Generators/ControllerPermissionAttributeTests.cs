using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class ControllerPermissionAttributeTests
{
    // Pins the security-relevant decision behind the controller's boundary authorization: an authorize-able
    // entity yields [AuthGuard("{Crud}{Entity}")] on its CRUD actions, while a [DoNotAuthorize] entity still
    // yields a bare [AuthGuard]. A dropped/wrong attribute is a silent authz hole.
    //
    // This pins the emission *logic* (Helpers.GetAuthGuardAttribute) rather than a full controller snapshot:
    // ControllerGenerator gates on a ".WebAPI" calling path that GeneratorTestHarness does not supply, so it
    // emits nothing under the harness. End-to-end generated-controller output is exercised by the CI e2e job.

    [Fact]
    public void AuthorizeableEntity_EmitsAuthGuardCarryingThePermissionCode()
    {
        SpiderlyClass entity = new() { Name = "Product" };

        Assert.Equal("[AuthGuard(\"ReadProduct\")]\n        ", Helpers.GetAuthGuardAttribute(entity, CrudCodes.Read));
        Assert.Equal("[AuthGuard(\"DeleteProduct\")]\n        ", Helpers.GetAuthGuardAttribute(entity, CrudCodes.Delete));
    }

    // The merged attribute made this case expressible, and the old emission was wrong for it: [DoNotAuthorize]
    // used to emit an empty string, which was only safe because the generator ALSO emitted a separate literal
    // [AuthGuard] line above it. With one attribute there is no second line to fall back on, so opting out of the
    // PERMISSION check must explicitly keep authentication — otherwise the entity's CRUD endpoints would become
    // anonymous, turning an authorization opt-out into an authentication hole.
    [Fact]
    public void DoNotAuthorizeEntity_StillEmitsBareAuthGuard()
    {
        SpiderlyClass entity = new()
        {
            Name = "PublicLookup",
            Attributes = new() { new SpiderlyAttribute { Name = "DoNotAuthorize" } },
        };

        Assert.Equal("[AuthGuard]\n        ", Helpers.GetAuthGuardAttribute(entity, CrudCodes.Read));
        Assert.Equal("[AuthGuard]\n        ", Helpers.GetAuthGuardAttribute(entity, CrudCodes.Delete));
    }
}
