# Native One-to-One (1-1) Relationship Support — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a first-class 1-1 relationship primitive (`[WithOne]`) to Spiderly so a single-valued reference nav can declare a one-to-one directly — generating the EF `HasOne().WithOne().HasForeignKey()` mapping, an automatic unique index (multi-NULL-safe for optional), edge-aware cascade ordering, and dependent-side DTO flattening — replacing today's M2O-with-a-fake-collection + manual `OnModelCreating` + manual pre-delete hack.

**Architecture:** A new `[WithOne]` attribute (mirror of `[WithMany]`) is placed on the **dependent** (FK-holding) side; its *presence* designates the dependent. A new `IsOneToOneType()` predicate carves `[WithOne]` navs out of the existing M2O classification so M2O codegen is provably unchanged. A new `ConfigureOneToOneRelationships` EF pass emits `HasOne().WithOne().HasForeignKey()` + a declarative `.HasIndex().IsUnique()` (provider conventions handle multi-NULL). The cascade walker is taught to see 1-1 edges. DTO/UI reuse the M2O path on the dependent side; the principal side is opt-in. Delete stays **app-layer only** (no DB `ON DELETE CASCADE`). Required/optional is strictly dependent-FK nullability.

**Tech Stack:** .NET 9 Roslyn incremental source generators (`Spiderly.SourceGenerators`), `Spiderly.Shared` (attributes/diagnostics), `Spiderly.Infrastructure` (EF model config), xUnit + Verify snapshot tests (`Spiderly.SourceGenerators.Tests`), EF Core 9 (Npgsql / SQL Server).

---

## Design decisions (resolved — the "why" behind every task)

| # | Decision | Consequence in this plan |
|---|----------|--------------------------|
| D1 | 1-1 is a real need (Helmio: Task/Doc owns one comment-thread Conversation — two aggregate roots, **not** an owned type). | Build a true relationship, not `OwnsOne`. |
| D2 | `[WithOne(nameof(Principal.InverseNav))]` on the **dependent** side only; presence = dependent. | Slice 1 / Task 1.1–1.3. |
| D3 | Delete is **app-layer only**. No DB `ON DELETE CASCADE`. The walker becomes edge-aware (also fixes a pre-existing M2O bug). | Slice 0 (independent) + Task 1.6. |
| D4 | DTO is **asymmetric**: dependent flattens like M2O (`{Nav}Id`+`{Nav}DisplayName`); principal is **opt-in**; never nested-both-ways. | Task 1.5. Principal-side opt-in is **deferred (YAGNI)**. |
| D5 | UI reuses the M2O autocomplete (zero new UI). **Mandatory:** unique-violation → localized `BusinessException`, not a raw 500. `[UIDoNotGenerate]` for code-managed (Helmio). Inline sub-form **deferred**. | Task 1.7 (+ confirm autocomplete reuse in 1.4). |
| D6 | Unique index is **declarative only** (`HasIndex(fk).IsUnique()`). No raw SQL, no hardcoded `NULLS DISTINCT`, never `HasFilter(null)`. Provider conventions give multi-NULL free on PG (`NULLS DISTINCT`) + SQL Server (auto `IS NOT NULL` filter). | Task 1.3 + portability snapshot test. |
| D7 | "Required" = dependent-FK nullability **only**. Principal→dependent "has at least one" is **undeclarable / out of scope**. `[Required]` on the **principal** nav is a **hard error**. | Task 1.2 (diagnostic SPIDERLY021). |
| D8 | Explicit FK **optional** (shadow FK allowed, symmetric with `[WithMany]`). Helmio *chooses* explicit FK; framework doesn't force it. | Tasks resolve FK via existing `ResolveExplicitForeignKeyName` (handles both). |
| D9 | Unidirectional 1-1 **in** (`[WithOne]` no-arg → `.WithOne()`). Self-referential 1-1 **out**, hard error. | Task 1.1 (no-arg ctor) + diagnostic SPIDERLY022. |
| D10 | Slice 0 (cascade walker) ships **first and independently**. | Slice ordering below. |

**Deferred — do NOT implement (YAGNI, no task until a real case appears):** principal-side opt-in reverse-lookup DTO; inline embedded sub-form UI; self-referential 1-1.

---

## Grounding map (real files this plan touches)

- **Attribute:** `Spiderly.Shared/Attributes/Entity/WithManyAttribute.cs` → new sibling `WithOneAttribute.cs`.
- **Diagnostics:** `Spiderly.SourceGenerators/Shared/SpiderlyDiagnostics.cs` — next free IDs are **SPIDERLY019–SPIDERLY023**.
- **Detection:** `Spiderly.SourceGenerators/Shared/Extensions.cs` — `IsManyToOneType` (lines 164–186), `HasWithManyAttribute` (537), `ResolveExplicitForeignKeyName` (628), `GetForeignKeyAccessExpression` (691).
- **EF model config:** `Spiderly.Infrastructure/Extensions.cs` — `ConfigureManyToOneRelationships` (90–128), `ResolveForeignKeyName` (138); call site in `Spiderly.Infrastructure/ApplicationDbContext.cs`.
- **Mapper (DTO flatten):** `Spiderly.SourceGenerators/Net/MapperGenerator.cs` — M2O branch at line 220.
- **DTO columns:** `Spiderly.SourceGenerators/Shared/SpiderlyClassFactory.cs` — M2O branch at line 254 (emits `ManyToOneId`/`ManyToOneDisplayName`).
- **FK validation:** `Spiderly.SourceGenerators/Shared/ForeignKeyValidator.cs` — loop at line 17.
- **Cascade walker:** `Spiderly.SourceGenerators/Net/Services/ServiceDeleteGenerator.cs` + `Spiderly.SourceGenerators/Shared/Helpers.cs` `GetCascadeDeleteProperties` (63).
- **Save:** `Spiderly.SourceGenerators/Net/Services/ServiceSaveGenerator.cs` — M2O branch at line 429.
- **Angular details UI:** `Spiderly.SourceGenerators/Angular/BaseDetails/NgDetailsPropertyBlockGenerator.cs` (M2O autocomplete at 223/300), `NgDetailsDataGenerator.cs` (408).
- **Validators:** `Spiderly.SourceGenerators/Shared/ValidationRuleBuilder.cs` (65), `Validations.cs` (58).
- **Tests:** `Spiderly.SourceGenerators.Tests/` — harness `GeneratorTestHarness.Run<TGenerator>(source)` + `Verify(driver)` (snapshot); diagnostic-assertion pattern in `PrimaryKeyDiagnosticTests.cs`.
- **Docs/skills:** `claude-plugins/skills/entity-design/SKILL.md`, `claude-plugins/skills/ef-migrations/` (consumer-shipped); `spiderly-website/` sibling for public docs.

**Build/verify commands (run from `spiderly/`):**
- Generators: `dotnet build Spiderly.SourceGenerators/Spiderly.SourceGenerators.csproj`
- Tests: `dotnet test Spiderly.SourceGenerators.Tests/Spiderly.SourceGenerators.Tests.csproj`
- Single test: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~OneToOne"`
- Infra: `dotnet build Spiderly.Infrastructure/Spiderly.Infrastructure.csproj`

> **Snapshot tests (Verify):** the first run of a new `Verify(...)` test FAILS and writes a `*.received.txt`. Inspect it; if correct, rename to `*.verified.txt` (or run with the configured auto-accept) and re-run to green. "Expected: FAIL (no verified snapshot yet)" below refers to this.

---

# SLICE 0 — Edge-aware cascade walker (independent bugfix, ships first)

**Why first:** The bug — *hand-configured / no-nav FK edges are invisible to the generated `Delete*List` cascade, producing wrong teardown order and runtime FK violations* — exists **today for M2O**, independent of 1-1. It is also the foundation 1-1's cascade leans on. Ship it alone, with a regression test that fails on its own commit (per `spiderly/CLAUDE.md` "Regression tests must fail on the commit that adds them").

### Task 0.1: Characterize current cascade-edge collection

**Files:**
- Read: `Spiderly.SourceGenerators/Shared/Helpers.cs:63` (`GetCascadeDeleteProperties`)
- Read: `Spiderly.SourceGenerators/Net/Services/ServiceDeleteGenerator.cs:103` (`GetManyToOneDeleteQueries`)
- Test: `Spiderly.SourceGenerators.Tests/Generators/CascadeDeleteWalkerTests.cs` (create)

- [ ] **Step 1: Read both functions and write down, in the test file as a comment, exactly which edges `GetCascadeDeleteProperties` currently returns** (it scans entities for `[CascadeDelete]` navigations pointing at `entityName`). Confirm whether a `[CascadeDelete]` placed where there is **no navigation property** (explicit-FK-only pointer) is collected. This determines whether the bug is "edge not collected" or "edge collected but ordered wrong."

- [ ] **Step 2: Create the failing regression test** capturing a multi-level cascade whose correct teardown order the current walker gets wrong. Use a chain where a grandchild FK must be deleted before the child:

```csharp
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

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
```

- [ ] **Step 3: Run it and inspect the generated `Delete*List` order**

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~CascadeDeleteWalkerTests"`
Expected: FAIL (no verified snapshot yet). **Open the `.received.txt`** and confirm whether `DeleteOrg` deletes `Member` rows before `Team` rows. Record the verdict in a commit message in Step 4.

- [ ] **Step 4: Commit the characterization test (snapshot accepted as the *current* baseline)**

```bash
cd spiderly
git add Spiderly.SourceGenerators.Tests/Generators/CascadeDeleteWalkerTests.cs \
        Spiderly.SourceGenerators.Tests/Generators/*CascadeDeleteWalker*.verified.txt
git commit -m "test(cascade): characterize multi-level cascade delete ordering

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> If Step 3 shows the order is already correct for the *nav-based* chain, the real defect is narrower: it only manifests for **no-nav explicit-FK** edges. In that case, in Task 0.2 add a second test variant where `Team` drops the `Org` nav and keeps only `public long OrgId` configured via a `[CascadeDelete]`-equivalent, and make Slice 0 the work of teaching `GetCascadeDeleteProperties` to collect scalar-FK cascade edges. Decide based on the received snapshot — do not guess.

### Task 0.2: Fix the walker to collect every cascade FK edge in correct order

**Files:**
- Modify: `Spiderly.SourceGenerators/Shared/Helpers.cs` (`GetCascadeDeleteProperties`)
- Modify (if needed): `Spiderly.SourceGenerators/Net/Services/ServiceDeleteGenerator.cs` (`GetManyToOneDeleteQueries`)
- Test: `Spiderly.SourceGenerators.Tests/Generators/CascadeDeleteWalkerTests.cs`

- [ ] **Step 1: Implement the minimal fix** identified in Task 0.1 so the walker emits grandchild-before-child deletes for every cascade edge (nav-based *and*, per the note, explicit-FK pointers). Keep teardown inside the existing single `WithTransactionAsync` and keep using `GetForeignKeyAccessExpression` (Extensions.cs:691) so shadow/explicit FKs resolve uniformly.

- [ ] **Step 2: Update the snapshot to the corrected order**

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~CascadeDeleteWalkerTests"`
Expected: FAIL (received ≠ verified). Inspect `.received.txt`: confirm `Member` deletes now precede `Team` deletes. Accept the new snapshot.

- [ ] **Step 3: Re-run to green**

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~CascadeDeleteWalkerTests"`
Expected: PASS.

- [ ] **Step 4: Full generator-test regression** (prove no other snapshot moved)

Run: `dotnet test Spiderly.SourceGenerators.Tests/Spiderly.SourceGenerators.Tests.csproj`
Expected: PASS (only the intentionally-updated snapshot changed).

- [ ] **Step 5: Commit the fix**

```bash
cd spiderly
git add Spiderly.SourceGenerators/Shared/Helpers.cs \
        Spiderly.SourceGenerators/Net/Services/ServiceDeleteGenerator.cs \
        Spiderly.SourceGenerators.Tests/Generators/*CascadeDeleteWalker*.verified.txt
git commit -m "fix(cascade): order multi-level/no-nav FK cascade deletes child-first

The Delete*List walker mis-ordered (or skipped) cascade edges, causing
runtime FK violations. Walker now collects every cascade FK edge and emits
child->parent teardown in one transaction.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

**>>> SLICE 0 REVIEW CHECKPOINT — Slice 0 is independently shippable. Stop and review before Slice 1. <<<**

---

# SLICE 1 — One-to-one core

### Task 1.1: `[WithOne]` attribute

**Files:**
- Create: `Spiderly.Shared/Attributes/Entity/WithOneAttribute.cs`

- [ ] **Step 1: Write the attribute** (mirror `WithManyAttribute.cs`; allow a no-arg ctor for unidirectional per D9). All public members need `/// <summary>` (per `spiderly/CLAUDE.md`).

```csharp
using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Declares a one-to-one relationship. Place on the <b>dependent</b> (foreign-key-holding)
    /// side's single-valued reference navigation. Its presence designates this side as the dependent;
    /// the other side is the principal. <br/><br/>
    ///
    /// <b>Required vs optional:</b> add <c>[Required]</c> to make the dependent's FK non-nullable
    /// ("dependent must have a principal"). Omit it for an optional 1-1 (nullable FK, many NULLs allowed).
    /// The schema cannot enforce "principal must have a dependent" — that direction is always 0..1. <br/><br/>
    ///
    /// <b>Unidirectional:</b> use the parameterless constructor when the principal has no back-navigation. <br/><br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// public class Conversation : BusinessObject&lt;long&gt;
    /// {
    ///     public long? OwningTaskItemId { get; set; }          // explicit FK (recommended for code-managed)
    ///     [WithOne(nameof(TaskItem.Conversation))]
    ///     [CascadeDelete]
    ///     public virtual TaskItem OwningTaskItem { get; set; }
    /// }
    ///
    /// public class TaskItem : BusinessObject&lt;long&gt;
    /// {
    ///     public virtual Conversation Conversation { get; set; } // principal side, no attribute
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class WithOneAttribute : Attribute
    {
        /// <summary>The name of the inverse single-valued navigation on the principal entity, or null for a unidirectional 1-1.</summary>
        public string WithOne { get; set; }

        /// <param name="withOne">The name of the inverse navigation on the principal entity. Omit for unidirectional.</param>
        public WithOneAttribute(string withOne = null)
        {
            WithOne = withOne;
        }
    }
}
```

- [ ] **Step 2: Build the Shared project**

Run: `dotnet build Spiderly.Shared/Spiderly.Shared.csproj`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
cd spiderly
git add Spiderly.Shared/Attributes/Entity/WithOneAttribute.cs
git commit -m "feat(attr): add [WithOne] for one-to-one relationships

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.2: Detection predicates + diagnostics (the safety carve-out)

**Files:**
- Modify: `Spiderly.SourceGenerators/Shared/Extensions.cs` (add `HasWithOneAttribute`, `IsOneToOneType`; exclude `[WithOne]` from `IsManyToOneType(SpiderlyProperty)`)
- Modify: `Spiderly.SourceGenerators/Shared/SpiderlyDiagnostics.cs` (add SPIDERLY019–022)
- Test: `Spiderly.SourceGenerators.Tests/Generators/OneToOneDiagnosticsTests.cs` (create; mirror `PrimaryKeyDiagnosticTests.cs`)

- [ ] **Step 1: Add the diagnostics** to `SpiderlyDiagnostics.cs` (after SPIDERLY018):

```csharp
public static readonly DiagnosticDescriptor OneToOneOnBothSides = new(
    id: "SPIDERLY019",
    title: "One-to-one declared with [WithOne] on both sides",
    messageFormat: "Both '{0}.{1}' and '{2}.{3}' carry [WithOne]. Exactly one side (the dependent / FK holder) may carry it; remove [WithOne] from the principal and declare a plain single-valued navigation there.",
    category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

public static readonly DiagnosticDescriptor OneToOneInverseNavNotFound = new(
    id: "SPIDERLY020",
    title: "[WithOne] inverse navigation does not exist on the principal",
    messageFormat: "[WithOne(\"{0}\")] on '{1}.{2}' requires '{3}' to declare a single-valued 'public virtual {1} {0}' navigation. Add it to '{3}', or use the parameterless [WithOne] for a unidirectional 1-1.",
    category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

public static readonly DiagnosticDescriptor OneToOneRequiredOnPrincipal = new(
    id: "SPIDERLY021",
    title: "[Required] on the principal side of a one-to-one is unenforceable",
    messageFormat: "[Required] on principal navigation '{0}.{1}' is unenforceable: a unique FK index guarantees at most one dependent, never at least one. Configure requiredness on the dependent ([WithOne]) side instead, or enforce 'principal always has a dependent' in an OnAfterInsert hook.",
    category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

public static readonly DiagnosticDescriptor OneToOneSelfReferential = new(
    id: "SPIDERLY022",
    title: "Self-referential one-to-one is not supported",
    messageFormat: "[WithOne] on '{0}.{1}' targets the declaring entity '{0}'. Self-referential 1-1 is not supported in this version.",
    category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);
```

- [ ] **Step 2: Add detection predicates** to `Extensions.cs` (mirror `HasWithManyAttribute` at :537; reuse the same attribute-reading helper it uses):

```csharp
/// <summary>True when the property carries [WithOne] — i.e. it is the dependent side of a one-to-one.</summary>
public static bool HasWithOneAttribute(this SpiderlyProperty property)
{
    // Mirror HasWithManyAttribute (Extensions.cs:537) — read attributes the same way.
    return property.Attributes.Any(a => a.Name == "WithOne");
}

/// <summary>
/// True when the property is the dependent side of a one-to-one (single reference nav carrying [WithOne]).
/// A non-enum, non-base, non-collection reference type that carries [WithOne].
/// </summary>
public static bool IsOneToOneType(this SpiderlyProperty property)
{
    if (property.HasWithOneAttribute() == false)
        return false;

    if (property.IsEnum)
        return false;

    return property.Type.Raw.IsManyToOneType(); // reuses the string predicate: not enumerable, not base type
}
```

- [ ] **Step 3: Exclude `[WithOne]` from M2O classification** — edit `IsManyToOneType(this SpiderlyProperty property)` (Extensions.cs:180):

```csharp
public static bool IsManyToOneType(this SpiderlyProperty property)
{
    if (property.IsEnum)
        return false;

    if (property.HasWithOneAttribute()) // one-to-one dependent side is NOT many-to-one
        return false;

    return property.Type.Raw.IsManyToOneType();
}
```

> NOTE: the string/`SpiderlyTypeRef` overloads of `IsManyToOneType` cannot see attributes. Audit every `.Type.IsManyToOneType()` call site (Grep `IsManyToOneType`) — the principal-side nav of a 1-1 is a bare reference type and will still pass those. Principal-side handling is governed by D4 (excluded from DTO by default) and D5 (no UI by default); confirm each call site treats a bare principal nav as "no M2O codegen." Where a site iterates `entity.Properties.Where(p => p.IsManyToOneType())` (the `SpiderlyProperty` overload — e.g. `ForeignKeyValidator.cs:17`, `ServiceSaveGenerator.cs:429`), the carve-out from Step 3 already excludes dependents correctly.

- [ ] **Step 4: Write the four diagnostic tests** (mirror `PrimaryKeyDiagnosticTests.cs` assertion style — assert the driver's diagnostics contain the expected id):

```csharp
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class OneToOneDiagnosticsTests
{
    [Fact] public void BothSidesWithOne_ReportsSPIDERLY019() =>
        GeneratorTestHarness.AssertDiagnostic<MapperGenerator>(BothSidesSource, "SPIDERLY019");

    [Fact] public void InverseNavMissing_ReportsSPIDERLY020() =>
        GeneratorTestHarness.AssertDiagnostic<MapperGenerator>(MissingInverseSource, "SPIDERLY020");

    [Fact] public void RequiredOnPrincipal_ReportsSPIDERLY021() =>
        GeneratorTestHarness.AssertDiagnostic<MapperGenerator>(RequiredOnPrincipalSource, "SPIDERLY021");

    [Fact] public void SelfReferential_ReportsSPIDERLY022() =>
        GeneratorTestHarness.AssertDiagnostic<MapperGenerator>(SelfRefSource, "SPIDERLY022");

    // ... const strings: each is a minimal two-entity (or one-entity for self-ref) [SpiderlyEntity] pair
    // exhibiting exactly one violation. Build them by editing the valid Helmio pair from Task 1.3's test.
}
```

> If `GeneratorTestHarness` has no `AssertDiagnostic` helper, add one next to `Run` in `Spiderly.SourceGenerators.Tests/Infrastructure/` that runs the generator and asserts `driver.GetRunResult().Diagnostics.Any(d => d.Id == expectedId)`. Follow the exact pattern already used by `PrimaryKeyDiagnosticTests.cs` (read it first).

- [ ] **Step 5: Run the diagnostic tests — verify they FAIL (validation not wired yet)**

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~OneToOneDiagnosticsTests"`
Expected: FAIL (diagnostics not yet emitted). These go green in Task 1.3 Step 4, after the O2O validator is added.

- [ ] **Step 6: Build + commit predicates and diagnostics**

Run: `dotnet build Spiderly.SourceGenerators/Spiderly.SourceGenerators.csproj`
Expected: PASS.

```bash
cd spiderly
git add Spiderly.SourceGenerators/Shared/Extensions.cs \
        Spiderly.SourceGenerators/Shared/SpiderlyDiagnostics.cs \
        Spiderly.SourceGenerators.Tests/Generators/OneToOneDiagnosticsTests.cs \
        Spiderly.SourceGenerators.Tests/Infrastructure/
git commit -m "feat(o2o): add [WithOne] detection predicates + SPIDERLY019-022 diagnostics (tests red)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.3: O2O validation + EF model config + unique index

**Files:**
- Create: `Spiderly.SourceGenerators/Shared/OneToOneValidator.cs` (mirror `ForeignKeyValidator.cs`)
- Modify: caller of `ForeignKeyValidator.ValidateEntity` to also invoke `OneToOneValidator.ValidateEntity` (Grep `ValidateEntity` to find it)
- Create: `Spiderly.Infrastructure/Extensions.cs` → `ConfigureOneToOneRelationships`
- Modify: `Spiderly.Infrastructure/ApplicationDbContext.cs` (call the new pass alongside `ConfigureManyToOneRelationships`)
- Test: `Spiderly.SourceGenerators.Tests/Generators/OneToOneRelationshipTests.cs` (create)

- [ ] **Step 1: Write the O2O validator** — enforce SPIDERLY019–022. Reuse `ResolveExplicitForeignKeyName` and the nullability/type checks already in `ForeignKeyValidator`:

```csharp
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>Compile-time validation for [WithOne] one-to-one declarations (SPIDERLY019-022).</summary>
    public static class OneToOneValidator
    {
        public static void ValidateEntity(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            foreach (SpiderlyProperty nav in entity.Properties.Where(p => p.IsOneToOneType()))
            {
                string targetTypeName = nav.Type.Raw;

                // SPIDERLY022 — self-referential 1-1 unsupported
                if (targetTypeName == entity.Name)
                    throw SpiderlyDiagnostics.Create(SpiderlyDiagnostics.OneToOneSelfReferential,
                        nav.Location ?? entity.Location, entity.Name, nav.Name);

                SpiderlyClass principal = allEntities.FirstOrDefault(c => c.Name == targetTypeName);
                if (principal == null)
                    continue; // referenced type from another project; EF resolves at runtime

                string inverseName = nav.GetWithOneInverseName(); // null for unidirectional

                // SPIDERLY020 — declared inverse nav must exist and be single-valued of this entity's type
                if (inverseName != null)
                {
                    SpiderlyProperty inverse = principal.Properties.FirstOrDefault(p => p.Name == inverseName);
                    if (inverse == null || inverse.Type.Raw != entity.Name)
                        throw SpiderlyDiagnostics.Create(SpiderlyDiagnostics.OneToOneInverseNavNotFound,
                            nav.Location ?? entity.Location, inverseName, entity.Name, nav.Name, principal.Name);

                    // SPIDERLY019 — both sides carry [WithOne]
                    if (inverse.HasWithOneAttribute())
                        throw SpiderlyDiagnostics.Create(SpiderlyDiagnostics.OneToOneOnBothSides,
                            nav.Location ?? entity.Location, entity.Name, nav.Name, principal.Name, inverse.Name);

                    // SPIDERLY021 — [Required] on the principal-side nav is unenforceable
                    if (inverse.IsEffectivelyRequired())
                        throw SpiderlyDiagnostics.Create(SpiderlyDiagnostics.OneToOneRequiredOnPrincipal,
                            inverse.Location ?? principal.Location, principal.Name, inverse.Name);
                }

                // Reuse FK nullability/type alignment (mirror ForeignKeyValidator) for the explicit-FK case.
                string fkName = nav.ResolveExplicitForeignKeyName(entity);
                if (fkName != null)
                {
                    SpiderlyProperty fk = entity.Properties.First(p => p.Name == fkName);
                    // call the same private checks ForeignKeyValidator uses, or duplicate the two guards inline
                }
            }
        }
    }
}
```

> Add `GetWithOneInverseName()` to `Extensions.cs` reading the `[WithOne]` ctor arg (mirror how `[WithMany]` arg is read by `ResolveExplicitForeignKeyName`/`HasWithManyAttribute`). Returns null for the no-arg form.

- [ ] **Step 2: Wire `OneToOneValidator.ValidateEntity` into the same place `ForeignKeyValidator.ValidateEntity` is called.** Grep `ForeignKeyValidator.ValidateEntity` to find the call site and add the O2O call beside it.

- [ ] **Step 3: Write `ConfigureOneToOneRelationships`** in `Spiderly.Infrastructure/Extensions.cs` (mirror `ConfigureManyToOneRelationships` at :90; reuse `ResolveForeignKeyName` at :138). **Declarative index only (D6).**

```csharp
/// <summary>
/// Configures one-to-one relationships declared with [WithOne] on the dependent side.
/// Emits HasOne().WithOne().HasForeignKey() and a declarative unique index on the FK.
/// The unique index is left to provider conventions for NULL handling (PostgreSQL NULLS DISTINCT;
/// SQL Server auto 'IS NOT NULL' filter) — never raw SQL, never HasFilter(null).
/// </summary>
public static void ConfigureOneToOneRelationships(this List<IMutableEntityType> mutableEntityTypes, ModelBuilder modelBuilder)
{
    foreach (IMutableEntityType entityType in mutableEntityTypes)
    {
        Type clrType = entityType.ClrType;

        foreach (PropertyInfo property in clrType.GetProperties())
        {
            WithOneAttribute withOne = property.GetCustomAttribute<WithOneAttribute>();
            if (withOne == null)
                continue;

            RequiredAttribute requiredAttribute = property.GetCustomAttribute<RequiredAttribute>();
            SetNullAttribute setNullAttribute = property.GetCustomAttribute<SetNullAttribute>();
            DeleteBehavior deleteBehavior = setNullAttribute == null ? DeleteBehavior.NoAction : DeleteBehavior.SetNull;

            string foreignKeyName = ResolveForeignKeyName(property, clrType);

            var refRef = modelBuilder.Entity(clrType)
                .HasOne(property.PropertyType, property.Name)
                .WithOne(withOne.WithOne) // null -> unidirectional .WithOne()
                .HasForeignKey(clrType, foreignKeyName)
                .OnDelete(deleteBehavior)
                .IsRequired(requiredAttribute != null);

            // Declarative unique index — provider conventions handle NULLs (D6). No HasFilter(null).
            modelBuilder.Entity(clrType).HasIndex(foreignKeyName).IsUnique();
        }
    }
}
```

> Verify the exact EF Core 9 overloads at build time: `ReferenceReferenceBuilder.HasForeignKey(Type dependentEntityType, params string[] foreignKeyPropertyNames)` and `EntityTypeBuilder.HasIndex(params string[] propertyNames)`. Adjust if the compiler points to a different overload.

- [ ] **Step 4: Call the new pass** in `ApplicationDbContext.cs` right after `ConfigureManyToOneRelationships(modelBuilder)` (Grep `ConfigureManyToOneRelationships` to find the call).

- [ ] **Step 5: Run the diagnostic tests from Task 1.2 — now GREEN**

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~OneToOneDiagnosticsTests"`
Expected: PASS (all four diagnostics now emitted).

- [ ] **Step 6: Write the happy-path EF snapshot test** (valid Helmio pair) and a portability guard:

```csharp
[Fact]
public Task OptionalOneToOne_EmitsUniqueIndexWithoutHasFilterNull()
{
    const string source = """
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
        """;
    var driver = GeneratorTestHarness.Run<MapperGenerator>(source);
    return Verify(driver);
}
```

> If unique-index emission is purely runtime (in `Spiderly.Infrastructure`, not source-generated), assert it with an EF model test instead: build a tiny `DbContext` over these entities in the test, call `Model.FindEntityType("Conversation").GetIndexes()`, and assert exactly one unique index over the FK with `GetFilter()` **not** explicitly set to a NULL-collapsing filter. Pick whichever layer actually owns the index based on Step 3's location.

- [ ] **Step 7: Build both projects + commit**

Run: `dotnet build Spiderly.SourceGenerators/Spiderly.SourceGenerators.csproj && dotnet build Spiderly.Infrastructure/Spiderly.Infrastructure.csproj`
Expected: PASS.

```bash
cd spiderly
git add Spiderly.SourceGenerators/Shared/OneToOneValidator.cs \
        Spiderly.SourceGenerators/Shared/Extensions.cs \
        Spiderly.Infrastructure/Extensions.cs \
        Spiderly.Infrastructure/ApplicationDbContext.cs \
        Spiderly.SourceGenerators.Tests/Generators/OneToOneRelationshipTests.cs \
        Spiderly.SourceGenerators.Tests/Generators/*OneToOne*.verified.txt
git commit -m "feat(o2o): EF HasOne().WithOne() + declarative unique index + validation

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.4: Prove M2O codegen is byte-identical (non-regression)

**Files:**
- Test: `Spiderly.SourceGenerators.Tests/Generators/ManyToOneNonRegressionTests.cs` (create)

- [ ] **Step 1: Add a snapshot test for a representative M2O entity** (the `Product`/`Category`/`Brand` shape from the entity-design skill) across all affected generators (Mapper, Services, Ng details). This snapshot is the contract that 1-1 work did not perturb M2O.

```csharp
[Fact]
public Task ManyToOne_MapperOutput_Unchanged()
{
    const string source = """
        using System.Collections.Generic;
        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Category : BusinessObject<long>
            {
                [DisplayName] public string Name { get; set; }
                public virtual List<Product> Products { get; } = new();
            }
            [SpiderlyEntity]
            public class Product : BusinessObject<long>
            {
                [DisplayName] public string Title { get; set; }
                [Required]
                [WithMany(nameof(Category.Products))]
                public virtual Category Category { get; set; }
            }
        }
        """;
    var driver = GeneratorTestHarness.Run<MapperGenerator>(source);
    return Verify(driver);
}
```

- [ ] **Step 2: Run and accept the snapshot, then re-run green**

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~ManyToOneNonRegression"`
Expected: FAIL (no snapshot) → accept → PASS. The accepted snapshot must match the pre-feature M2O output — diff it against `git show HEAD~N` output if in doubt.

- [ ] **Step 3: Commit**

```bash
cd spiderly
git add Spiderly.SourceGenerators.Tests/Generators/ManyToOneNonRegressionTests.cs \
        Spiderly.SourceGenerators.Tests/Generators/*ManyToOne*.verified.txt
git commit -m "test(o2o): pin M2O codegen output to prove 1-1 carve-out is non-regressive

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.5: Dependent-side DTO flattening

**Files:**
- Modify: `Spiderly.SourceGenerators/Shared/SpiderlyClassFactory.cs` (M2O branch at :254 — `GetDTOColumns`)
- Modify: `Spiderly.SourceGenerators/Net/MapperGenerator.cs` (M2O branch at :220)
- Test: `Spiderly.SourceGenerators.Tests/Generators/OneToOneRelationshipTests.cs`

- [ ] **Step 1: Write the failing DTO test** — assert the dependent's DTO carries `OwningTaskItemId` + `OwningTaskItemDisplayName`, and the principal's DTO carries **no** Conversation column (D4):

```csharp
[Fact]
public Task DependentSide_FlattensToIdAndDisplayName_PrincipalSideOmitted()
{
    // reuse the Conversation/TaskItem source from Task 1.3 Step 6
    var driver = GeneratorTestHarness.Run<MapperGenerator>(/* Conversation/TaskItem source */);
    return Verify(driver);
}
```

- [ ] **Step 2: Run — verify FAIL** (dependent currently produces nothing because the carve-out removed it from the M2O branch)

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~DependentSide_Flattens"`
Expected: FAIL.

- [ ] **Step 3: Route the dependent (`IsOneToOneType()`) through the same code path as the M2O branch.** In `SpiderlyClassFactory.cs:254`, change the M2O condition to also accept O2O dependents so they emit `ManyToOneId` + `ManyToOneDisplayName` columns; mirror the same in `MapperGenerator.cs:220`. Keep the principal-side bare nav excluded (no change needed — it never matched M2O and stays out per D4).

```csharp
// SpiderlyClassFactory.cs (~254) — was: if (property.IsManyToOneType())
if (property.IsManyToOneType() || property.IsOneToOneType())
{
    // unchanged body: emit ManyToOneId + ManyToOneDisplayName columns
}
```

- [ ] **Step 4: Run — accept snapshot — verify PASS**

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~DependentSide_Flattens"`
Expected: accept snapshot → PASS. Confirm in `.received.txt`: `ConversationDTO` has `OwningTaskItemId` + `OwningTaskItemDisplayName`; `TaskItemDTO` has **no** Conversation field.

- [ ] **Step 5: Re-run the M2O non-regression test from Task 1.4 — must stay PASS**

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~ManyToOneNonRegression"`
Expected: PASS (unchanged).

- [ ] **Step 6: Commit**

```bash
cd spiderly
git add Spiderly.SourceGenerators/Shared/SpiderlyClassFactory.cs \
        Spiderly.SourceGenerators/Net/MapperGenerator.cs \
        Spiderly.SourceGenerators.Tests/Generators/*OneToOne*.verified.txt
git commit -m "feat(o2o): flatten dependent side to {Nav}Id + {Nav}DisplayName (principal opt-in)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.6: Cascade walker sees 1-1 edges

**Files:**
- Modify: `Spiderly.SourceGenerators/Shared/Helpers.cs` (`GetCascadeDeleteProperties` at :63 — include O2O cascade navs)
- Test: `Spiderly.SourceGenerators.Tests/Generators/OneToOneRelationshipTests.cs`

- [ ] **Step 1: Write the failing cascade test** — deleting `TaskItem` must delete its `Conversation` (FK on Conversation, `[CascadeDelete]` on the `[WithOne]` nav), child-first:

```csharp
[Fact]
public Task DeletingPrincipal_CascadesOneToOneDependent()
{
    // Conversation([WithOne]+[CascadeDelete] -> TaskItem) ; TaskItem principal
    var driver = GeneratorTestHarness.Run<ServicesGenerator>(/* Conversation/TaskItem + TaskItemService */);
    return Verify(driver);
}
```

- [ ] **Step 2: Run — verify FAIL** (walker doesn't yet collect the O2O edge after the carve-out)

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~DeletingPrincipal_Cascades"`
Expected: FAIL.

- [ ] **Step 3: Make `GetCascadeDeleteProperties` (Helpers.cs:63) include O2O dependents** whose `[WithOne]` nav carries `[CascadeDelete]`, pointing at the principal. The downstream `GetForeignKeyAccessExpression` already resolves the FK via `ResolveExplicitForeignKeyName` (works for shadow + explicit, D8), so only the *collection* predicate needs widening — wherever it currently filters `IsManyToOneType()`, also accept `IsOneToOneType()`.

- [ ] **Step 4: Run — accept snapshot — PASS**

Run: `dotnet test Spiderly.SourceGenerators.Tests --filter "FullyQualifiedName~DeletingPrincipal_Cascades"`
Expected: accept → PASS. Confirm `DeleteTaskItem` deletes `Conversation` rows (by FK) before the `TaskItem` row, in one transaction.

- [ ] **Step 5: Full test regression**

Run: `dotnet test Spiderly.SourceGenerators.Tests/Spiderly.SourceGenerators.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
cd spiderly
git add Spiderly.SourceGenerators/Shared/Helpers.cs \
        Spiderly.SourceGenerators.Tests/Generators/*OneToOne*.verified.txt
git commit -m "feat(o2o): cascade walker orders 1-1 dependent teardown child-first

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.7: Unique-violation → localized BusinessException (the one mandatory UI-side deliverable, D5)

**Files:**
- Locate: the seam where generated/base service `SaveChanges` runs inside `WithTransactionAsync` (Grep `WithTransactionAsync` and the base service in `Spiderly.Infrastructure` / `Spiderly.Security`). Find where `ConcurrencyException` is thrown — translate the unique violation at the same layer.
- Modify: that seam to catch the provider unique-violation and rethrow a localized `BusinessException`.
- Modify: `Spiderly.Shared/Contracts/ApiErrorCodes.cs` (+ TS mirrors per `spiderly/CLAUDE.md` "API error codes") if a new code is warranted.
- Test: `Spiderly.SourceGenerators.Tests` is the wrong project for runtime behavior — add to the backend test project that exercises services (Grep for an existing `*.Tests` project building real services; the `backend-testing` skill describes the InMemory pattern, but **unique constraints are NOT enforced by the EF InMemory provider** — use the SQLite in-memory provider or a real Postgres test container for this one).

- [ ] **Step 1: Identify the exception to catch.** Npgsql throws `PostgresException` with `SqlState == "23505"` on unique violation; SQL Server throws `SqlException` number 2601/2627. Decide whether to catch `DbUpdateException` and inspect `InnerException`, which is provider-portable.

- [ ] **Step 2: Write the failing test** — saving a second dependent that points at an already-linked principal throws a `BusinessException` (not a raw `DbUpdateException`/500), against SQLite-in-memory or a Postgres test container (NOT EF InMemory — it ignores unique indexes):

```csharp
[Fact]
public async Task SavingSecondDependentForSamePrincipal_ThrowsBusinessException()
{
    // arrange: principal + one dependent linked. act: save a second dependent with same FK.
    // assert: await Assert.ThrowsAsync<BusinessException>(() => service.SaveConversation(secondDto, ...));
}
```

- [ ] **Step 3: Run — verify FAIL** (currently surfaces a raw DbUpdateException)

Run: `dotnet test <backend-service-test-project> --filter "FullyQualifiedName~SavingSecondDependent"`
Expected: FAIL.

- [ ] **Step 4: Implement the translation** at the SaveChanges seam: catch the unique-violation, throw `new BusinessException(localizer["OneToOneAlreadyLinked", entityName])` (use the JSON-file localizer per the `backend-localization` skill; add the key to the culture JSON files). Keep it generic — any unique-index violation maps to a clean localized message; do not hard-code the FK name.

- [ ] **Step 5: Run — verify PASS**

Run: `dotnet test <backend-service-test-project> --filter "FullyQualifiedName~SavingSecondDependent"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
cd spiderly
git add -A
git commit -m "feat(o2o): translate unique-index violation to localized BusinessException

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

**>>> SLICE 1 REVIEW CHECKPOINT — full `dotnet test` + a manual `spiderly init`-style smoke (or the e2e fixture) before Slice 2. <<<**

- [ ] Run full generator + backend test suites: `dotnet test Spiderly.SourceGenerators.Tests/Spiderly.SourceGenerators.Tests.csproj`
- [ ] Add a `Conversation`/`TaskItem` 1-1 pair to the e2e fixture (`tests/e2e-fixtures/backend/entities/`) if a runtime migration + delete smoke is wanted (mirror `Project.cs`/`ProjectTask.cs`).

---

# SLICE 2 — Docs + skills

### Task 2.1: Update the entity-design skill

**Files:**
- Modify: `spiderly/claude-plugins/skills/entity-design/SKILL.md`

- [ ] **Step 1: Add a "One-to-One" section** after "Many-to-One". Cover: `[WithOne]` on the dependent; presence designates dependent; explicit-FK-recommended-for-code-managed; required = dependent-FK nullability only (principal guarantee impossible); optional allows many NULLs; unidirectional via no-arg; self-referential unsupported; the four diagnostics (SPIDERLY019–022); `[UIDoNotGenerate]` for code-managed; app-layer `[CascadeDelete]` works. Include the Helmio `Conversation`/`TaskItem` example verbatim from `WithOneAttribute.cs`.

- [ ] **Step 2: Update the skill description frontmatter** to mention 1-1 (it currently lists "M2O, M2M, ordered O2M").

- [ ] **Step 3: Commit**

```bash
cd spiderly
git add claude-plugins/skills/entity-design/SKILL.md
git commit -m "docs(entity-design): document [WithOne] one-to-one relationships

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 2.2: Update the ef-migrations skill + public website

**Files:**
- Modify: `spiderly/claude-plugins/skills/ef-migrations/SKILL.md`
- Modify: `spiderly-website/` (the build-diagnostics page → add SPIDERLY019–022; relationships/attributes docs → add `[WithOne]`)

- [ ] **Step 1: ef-migrations skill** — note that a 1-1 produces a unique index (multi-NULL-safe, provider conventions) declaratively, and that the migration just reflects the model (no manual `[Index(IsUnique=true)]`, no manual `OnModelCreating` — matching the "prefer declarative `modelBuilder` config" project preference).

- [ ] **Step 2: spiderly-website** — add SPIDERLY019–022 to the build-diagnostics reference and `[WithOne]` to the attributes/relationships docs (per `spiderly/CLAUDE.md` "Documentation updates").

- [ ] **Step 3: Commit each repo separately** (different default branches: `spiderly` → `develop`; `spiderly-website` → `develop`).

```bash
cd spiderly && git add claude-plugins/skills/ef-migrations/SKILL.md && \
  git commit -m "docs(ef-migrations): note 1-1 declarative unique index

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
# then, in the spiderly-website repo:
# git add ... && git commit -m "docs: document [WithOne] + SPIDERLY019-022"
```

---

## Self-review (completed against the resolved design)

- **Spec coverage:** D1→(rationale), D2→1.1/1.2, D3→Slice 0 + 1.6, D4→1.5, D5→1.4 (reuse) + 1.7 (mandatory guard), D6→1.3, D7→1.2 (SPIDERLY021), D8→resolver reuse throughout, D9→1.1 (no-arg) + 1.2 (SPIDERLY022), D10→slice ordering. Deferred items explicitly excluded. ✅
- **Type/name consistency:** `IsOneToOneType`, `HasWithOneAttribute`, `GetWithOneInverseName`, `ConfigureOneToOneRelationships`, `OneToOneValidator.ValidateEntity` used consistently across tasks. ✅
- **Known soft spots flagged inline (verify against live code, do not guess):** (1) exact shape of Slice 0's bug — confirm from the received snapshot in Task 0.1 before fixing; (2) exact EF Core 9 `HasForeignKey`/`HasIndex` overloads in Task 1.3; (3) whether the unique index is source-generated vs runtime-configured (changes the Task 1.3/1.6 test layer); (4) the SaveChanges exception-translation seam location in Task 1.7; (5) `GeneratorTestHarness.AssertDiagnostic` may need adding. These are real-codebase lookups, not design gaps.

---

## Implementation status (as-built — 2026-05-30)

**Slice 0 — abandoned.** Investigation (Task 0.1, commit `30b1e54`) proved the nav-based multi-level cascade ordering is *already correct*; the only real defect is **no-nav explicit-FK** edges being invisible to the walker — and native 1-1 always has a nav, so it never hits that bug. The no-nav-FK cascade fix is therefore an **independent, deferred bugfix** (needs its own attribute-surface design for declaring cascade on a bare scalar FK). The Task 0.1 characterization test was kept as a **pin** guarding nav-cascade ordering.

**Slice 1 — complete** (commits `76a4dde`→`942caea`; 374 generator tests + EF model test green; M2O proven byte-identical):
- `[WithOne]` attribute; `IsOneToOneType` / `HasWithOneAttribute` / `GetWithOneInverseName` predicates + carve-out.
- `OneToOneValidator` (SPIDERLY019–022); EF `ConfigureOneToOneRelationships` (`HasOne().WithOne().HasForeignKey()` + declarative `HasIndex().IsUnique()`, no DB cascade, no `HasFilter(null)`).
- **Principal-inverse handling (`2c2d574`)** — *not in the original plan.* A valid bidirectional 1-1's principal-side bare nav was classified as a M2O missing `[WithMany]` (SPIDERLY015) → build-broke. Added cross-entity `IsOneToOnePrincipalInverse` excluding it from validation/DTO/mapper/UI.
- Dependent DTO flattening (`d7585c9`); 1-1 cascade walker (`893b0e2`).
- **Task 1.7 (D5 unique-violation) needed NO code** — `SpiderlyExceptionHandler.TryMapDbConstraint` already maps PG 23505 / SQL Server 2627/2601 to a clean localized 409 generically.
- **Final-review fixes (`942caea`)** — the `|| IsOneToOneType()` reuse had been applied to DTO/mapper/cascade but **not** to the UI control-type, save-hydration, FluentValidation, or FK-validation sites; the dependent rendered a broken `UIControlType.None` autocomplete (slipped past all unit tests + the review's first pass). Propagated to `GetUIControlType`, `GetFormControlName`, `GetTableColFilterType`, `ShouldGenerateAutocompleteControllerMethod`, `GetManyToOneInstancesForSave`, `ValidationRuleBuilder`, `ForeignKeyValidator`.

**E2E validation — authored, CI-deferred** (commit `31558fb`): `ProjectCharter`↔`Project` optional 1-1 in the Playwright fixture (`tests/e2e-fixtures/`). Asserts on **real Postgres** (via `ci.yml`): dependent-FK round-trip, multi-NULL (NULLS DISTINCT), duplicate→4xx, cascade, and the autocomplete-renders regression. **Validation happens only on `push` to `develop`** (`ci.yml` runs it; `release.yml` is manual-only, so pushing can't publish). Not runnable locally.

**Slice 2 — docs — NOT STARTED.** Gated on the e2e going green (validate-before-document). Tasks 2.1/2.2 above stand.

**Deferred follow-ups (non-blocking):** no-nav-FK cascade fix (separate feature); shadow-FK 1-1 dependent (read/DTO/cascade/save now handled, but only explicit-FK fixtures exist — add a shadow-FK test); multi-level cascade chain test that includes a 1-1 hop; relocate `OneToOneRelationshipModelTests` out of `Spiderly.Shared.Tests` into a proper `Spiderly.Infrastructure.Tests` (the `Shared.Tests`→`Infrastructure` ref is a layering smell); positively confirm NULLS-DISTINCT on a real provider (now covered behaviorally by the e2e); tighten the `SpiderlyClassFactory.cs` shadow-FK comment.

**Branch state:** all commits on `develop`, unpushed. Pushing triggers CI (the e2e gate) but no release.
