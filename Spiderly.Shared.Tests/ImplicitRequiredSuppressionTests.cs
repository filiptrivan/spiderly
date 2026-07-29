using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Extensions;

namespace Spiderly.Shared.Tests;

/// <summary>
/// Under <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c>, MVC treats every non-nullable reference-typed
/// property as implicitly <c>[Required]</c> and 400s when the client sends <c>null</c> for it. That makes
/// an NRT annotation — a compile-time assertion — silently change what the API accepts at runtime.
/// <para>
/// It shipped: enabling NRT in the <c>spiderly init</c> scaffold made the admin data-table's own request
/// fail, because it posts <c>"multiSortMeta": null</c> and <c>FilterDTO.MultiSortMeta</c> is a
/// non-nullable <c>List&lt;FilterSortMetaDTO&gt;</c> — 400 "The MultiSortMeta field is required."
/// </para>
/// <para>
/// Spiderly's contract is that requiredness comes from <c>[Required]</c> and is enforced by the generated
/// FluentValidation rules, which return a 422 carrying <c>ApiErrorDTO.fieldErrors</c> the admin can render
/// per field. An implicit 400 from model state bypasses that layer entirely and produces an error shape no
/// client knows how to display.
/// </para>
/// </summary>
public class ImplicitRequiredSuppressionTests
{
    [Fact]
    public void NonNullableReferenceProperties_AreNotImplicitlyRequired()
    {
        ServiceCollection services = new();
        services.SpiderlyAddControllers();

        MvcOptions options = services.BuildServiceProvider().GetRequiredService<IOptions<MvcOptions>>().Value;

        Assert.True(options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes);
    }
}
