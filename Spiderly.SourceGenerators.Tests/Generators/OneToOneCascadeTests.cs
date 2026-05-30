using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

// ---------------------------------------------------------------------------
// Task 1.6 — cascade-delete walker recognizes one-to-one edges.
//
// A one-to-one dependent's [WithOne] nav carrying [CascadeDelete] must produce a cascade
// edge so that deleting the principal deletes the dependent first, in the same transaction.
//
// Before the fix, Helpers.GetCascadeDeleteProperties filtered collected edges on
// IsManyToOneType() only. A [WithOne] nav is deliberately NOT many-to-one
// (IsManyToOneType() == false), so the [CascadeDelete] edge was dropped and
// DeleteTaskItem / DeleteTaskItemList did NOT delete the dependent Conversation —
// leaving an orphan / FK violation.
//
// The fix widens the collection filter with `|| IsOneToOneType()` so the 1-1 edge is
// collected. The downstream GetForeignKeyAccessExpression already resolves the [WithOne]
// FK (OwningTaskItemId) via ResolveExplicitForeignKeyName, so the delete query generates
// correctly once the edge is present.
//
// This snapshot pins the DeleteTaskItem / DeleteTaskItemList bodies: the Conversation rows
// (filtered by OwningTaskItemId) must be deleted BEFORE the TaskItem row, inside the single
// WithTransactionAsync.
// ---------------------------------------------------------------------------

public class OneToOneCascadeTests
{
    // Conversation (dependent, [WithOne] + [CascadeDelete] + explicit FK scalar OwningTaskItemId)
    // <-> TaskItem (principal, bare inverse nav with [DisplayName] = Title).
    // Deleting a TaskItem must delete its Conversation first (dependent before principal).
    [Fact]
    public Task CascadeDelete_OnWithOneNav_DeletesDependentBeforePrincipal()
    {
        const string source = """
            using System.Collections.Generic;
            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Conversation : BusinessObject<long>
                {
                    public long? OwningTaskItemId { get; set; }

                    [WithOne(nameof(TaskItem.Conversation))]
                    [CascadeDelete]
                    public virtual TaskItem OwningTaskItem { get; set; }
                }
                [SpiderlyEntity]
                public class TaskItem : BusinessObject<long>
                {
                    [DisplayName] public string Title { get; set; }
                    public virtual Conversation Conversation { get; set; }
                }
            }
            namespace TestApp.Business.Services
            {
                [SpiderlyService] public class TaskItemService : TaskItemServiceGenerated { }
            }
            """;

        var driver = GeneratorTestHarness.Run<ServicesGenerator>(source);
        return Verify(driver);
    }
}
