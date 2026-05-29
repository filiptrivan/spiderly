using System.Collections.Generic;
using System.Linq;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

// Regression: a plain enum entity property (e.g. Announcement.Severity : AnnouncementSeverityCodes)
// declares a `severityOptionsForAnnouncement: Namebook[]` variable and binds a <spiderly-dropdown> to it,
// but nothing ever assigned the variable, so the dropdown rendered empty. The FK populate path gates on
// the explicit [UIControlType("Dropdown")] attribute (IsDropdownControlType), which an enum never carries,
// so enums fell through with no population at all. This locks in the client-side, no-API-round-trip
// population built from the generated TS enum helper.
public class EnumDropdownOptionsTests
{
    private static SpiderlyProperty EnumProp(string name, string enumType) =>
        new() { Name = name, Type = enumType, IsEnum = true };

    // BaseType must be set: IsManyToMany() treats a null-BaseType class as a junction, which the import
    // generator's entity filter would exclude.
    private static SpiderlyClass EntityWith(string entityName, params SpiderlyProperty[] properties) =>
        new() { Name = entityName, BaseType = "BusinessObject<long>", Properties = properties.ToList() };

    [Fact]
    public void EnumProperty_AssignsOptionsFromGeneratedEnumNamebookHelper()
    {
        SpiderlyClass entity = EntityWith("Announcement", EnumProp("Severity", "AnnouncementSeverityCodes"));

        List<string> statements = NgDetailsDataGenerator.GetEnumDropdownOptionsInitializations(
            entity, new List<SpiderlyClass>(), new List<SpiderlyClass>());

        Assert.Contains(
            "this.severityOptionsForAnnouncement = getAnnouncementSeverityCodesNamebookList(this.translocoService);",
            string.Join("\n", statements));
    }

    // The options array lives in one generated builder per enum (reused by the form dropdown and any
    // list-table enum filter), with a literal `translate('Member')` per member so transloco-keys-manager's
    // static extraction picks the keys up. Key = member name; the id is the strongly-typed enum reference.
    [Fact]
    public void EnumNamebookListFunction_EmitsTranslatedOptionPerMember()
    {
        List<SpiderlyEnumItem> items = new()
        {
            new() { Name = "Info", Value = "1" },
            new() { Name = "Warning", Value = "2" },
            new() { Name = "Critical", Value = "3" },
        };

        string fn = NgEnumsGenerator.GetEnumNamebookListFunction("AnnouncementSeverityCodes", items);

        Assert.Contains(
            "export function getAnnouncementSeverityCodesNamebookList(translocoService: TranslocoService): Namebook[]",
            fn);
        // marker('X') wraps the key so transloco-keys-manager extracts it from this standalone builder
        // (a plain translocoService.translate('X') here is not scanned); marker returns its arg, so
        // translate() still localizes at runtime.
        Assert.Contains(
            "{ id: AnnouncementSeverityCodes.Info, displayName: translocoService.translate(marker('Info')) }",
            fn);
        Assert.Contains(
            "{ id: AnnouncementSeverityCodes.Critical, displayName: translocoService.translate(marker('Critical')) }",
            fn);
    }

    // The populate path and the import path both derive from the same GetEnumDropdownContexts, but the import
    // wiring (path + helper name) has no other guard here: the admin `ng build` is the usual net for a missing
    // import, and in this workspace it's broken by an unrelated dual-@angular/router link. Without this test, a
    // dropped/renamed import would silently ship base-details referencing an unimported function.
    [Fact]
    public void EntityWithEnumDropdown_ImportsTheGeneratedEnumHelper()
    {
        SpiderlyClass entity = EntityWith("Announcement", EnumProp("Severity", "AnnouncementSeverityCodes"));

        List<string> imports = NgDetailsImportGenerator.GetEnumNamebookListImports(
            new List<SpiderlyClass> { entity }, new List<SpiderlyClass> { entity }, new List<SpiderlyClass>());

        Assert.Contains(
            "import { getAnnouncementSeverityCodesNamebookList } from '../enums/enums.generated';",
            imports);
    }
}
