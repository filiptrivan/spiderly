using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

// ---------------------------------------------------------------------------
// Task 0.1 — characterization of the cascade-delete walker (NO fix here).
//
// Step-1 finding: what edges does Helpers.GetCascadeDeleteProperties collect?
//
//   It runs `entities.SelectMany(x => x.Properties)` and keeps a property only when ALL hold:
//     1. prop.IsManyToOneType()                         — type is a navigation reference, not a scalar/collection
//     2. prop.Attributes.Any(a => a.Name == "CascadeDelete")  — [CascadeDelete] sits ON that nav property
//     3. prop.Type.Raw == entityName                    — the nav points at the entity being deleted
//
//   So a cascade edge is ONLY discovered when a *navigation property* carries [CascadeDelete].
//
//   No-nav (explicit-FK-only) edges are NOT collected: a bare scalar FK column such as
//   `public long TeamId { get; set; }` (no `Team` nav) fails check #1 — IsManyToOneType() returns
//   false for a base data type (`long`) — so even with [CascadeDelete] on it the edge is invisible to
//   the walker. GetForeignKeyAccessExpression CAN emit the `EF.Property<>(x, "{Nav}Id")` fallback for a
//   nav that lacks an explicit [ForeignKey], but that SQL-emission path only runs for edges that were
//   already collected (i.e. that still have a nav property). There is no separate scan of scalar FK
//   columns for [CascadeDelete].
//
//   => The defect class is "edge not collected" for no-nav FK pointers. For NAV-based chains the edges
//      ARE collected; this test pins down whether the collected chain is *ordered* correctly.
//
// This test feeds a 3-level NAV-based cascade chain (Org <- Team <- Member) and snapshots the
// generated DeleteOrg / DeleteOrgList bodies so Task 0.2's fix produces a visible diff.
// ---------------------------------------------------------------------------

public class CascadeDeleteWalkerTests
{
    // Org <-(CascadeDelete)- Team <-(CascadeDelete)- Member
    // Deleting Org must delete Members BEFORE Teams (child->parent order).
    [Fact]
    public Task MultiLevelCascade_OrdersGrandchildBeforeChild()
    {
        const string source = """
            using System.Collections.Generic;
            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Org : BusinessObject<long>
                {
                    [DisplayName] public string Name { get; set; }
                    public virtual List<Team> Teams { get; } = new();
                }
                [SpiderlyEntity]
                public class Team : BusinessObject<long>
                {
                    [DisplayName] public string Name { get; set; }
                    [CascadeDelete]
                    [WithMany(nameof(Org.Teams))]
                    public virtual Org Org { get; set; }
                    public virtual List<Member> Members { get; } = new();
                }
                [SpiderlyEntity]
                public class Member : BusinessObject<long>
                {
                    [DisplayName] public string Name { get; set; }
                    [CascadeDelete]
                    [WithMany(nameof(Team.Members))]
                    public virtual Team Team { get; set; }
                }
            }
            namespace TestApp.Business.Services
            {
                [SpiderlyService] public class OrgService : OrgServiceGenerated { }
            }
            """;

        var driver = GeneratorTestHarness.Run<ServicesGenerator>(source);
        return Verify(driver);
    }
}
