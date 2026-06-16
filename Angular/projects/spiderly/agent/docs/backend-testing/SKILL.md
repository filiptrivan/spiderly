---
name: backend-testing
description: Unit-test a Spiderly backend service against an EF Core InMemory database — covers the two InMemory pitfalls that silently corrupt assertions (the change tracker masking a missing SaveChanges, and WithTransactionAsync throwing TransactionIgnoredWarning). Use when writing xUnit/NUnit tests for entity services, business logic, or save/delete hooks against an InMemory DbContext, or when an InMemory-backed test passes/throws for reasons that don't match production.
---

# Backend Testing (EF Core InMemory)

Spiderly entity services run against a relational DbContext in production but are usually unit-tested against `Microsoft.EntityFrameworkCore.InMemory`. InMemory is *not* a relational database — two of its behavioral gaps will silently make a test lie unless you guard against them.

## Pitfall 1: the change tracker masks a missing `SaveChanges`

EF's change tracker serves **tracked-but-unsaved** entities back through later LINQ queries on the same context. So a method that adds/mutates an entity but forgets to call `SaveChangesAsync()` still appears to work — a follow-up `await _context.Set<T>().FirstOrDefaultAsync(...)` in the test returns the in-memory tracked instance, and the assertion passes. In production (separate request/context) the row was never persisted.

**If the method's contract is "I persist before returning," assert the flush happened — not just that the value is readable:**

```csharp
[Fact]
public async Task Activate_PersistsTheChange()
{
    using var context = NewContext();
    context.Product.Add(new Product { Id = 1, IsActive = false });
    await context.SaveChangesAsync();

    var service = new ProductService(context);
    await service.ActivateAsync(productId: 1);

    // ❌ This passes even if ActivateAsync never called SaveChangesAsync —
    //    the tracker hands back the mutated, unsaved instance.
    // var p = await context.Product.FirstAsync(x => x.Id == 1);
    // Assert.True(p.IsActive);

    // ✅ Assert the unit of work actually flushed.
    Assert.False(context.ChangeTracker.HasChanges());

    // For extra confidence, read through a FRESH context so nothing is served
    // from the tracker — this is the closest InMemory gets to a real round-trip:
    using var verify = NewContext();
    Assert.True((await verify.Product.FirstAsync(x => x.Id == 1)).IsActive);
}
```

`NewContext()` must reuse the **same** InMemory database name across both instances so the second context sees the first's saved data (see the shared options helper below).

## Pitfall 2: `WithTransactionAsync` throws `TransactionIgnoredWarning`

Spiderly service methods wrap their unit of work in `_context.WithTransactionAsync(...)`. The InMemory provider has no transaction support, so EF raises `InMemoryEventId.TransactionIgnoredWarning` — and by default warnings of this severity are promoted to **exceptions**, so the test throws before it reaches any assertion. Suppress that one warning when building the options:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

private static MyAppDbContext NewContext(string dbName) =>
    new(new DbContextOptionsBuilder<MyAppDbContext>()
        .UseInMemoryDatabase(dbName)
        // Without this, any code path that calls WithTransactionAsync throws:
        //   "An 'IServiceProvider' ... transaction ... was ignored ..."
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);
```

Pass a stable `dbName` (e.g. one per test, `Guid.NewGuid().ToString()`) so each test is isolated but a test's own multiple contexts share state.

## When InMemory isn't enough

InMemory ignores relational constraints (unique indexes, FK cascade, check constraints, `[Required]` at the DB level), provider-specific SQL, and real transaction semantics. If the behavior under test depends on any of those, prefer the **SQLite in-memory** provider (`UseSqlite("DataSource=:memory:")`, keep the connection open) or a disposable real database — don't assert relational guarantees against `UseInMemoryDatabase`.
