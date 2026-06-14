# Preparing the `spiderly-app` fixture

The eval fixture is a real Spiderly app in its **pre-task state**: scaffolded by `spiderly init`,
then overlaid with the per-task starting entities. It is large and gitignored
(`tools/agent-evals/fixtures/spiderly-app/`), so it is built fresh rather than committed.

## Preferred: build it in CI

`.github/workflows/agent-evals.yml` (manual `workflow_dispatch`) builds the fixture on an Ubuntu
runner — `spiderly init` → apply the eval overlay → `dotnet build` → stage `Backend/` into
`fixtures/spiderly-app/` — then runs the agents. This is the normal path: a fixture built from the
current commit evaluates the current framework + agent bundle, with a pinned toolchain, and removes
all local-setup friction.

## Eval fixtures must start in the PRE-task state

Do **NOT** reuse `tests/e2e-fixtures/setup.sh`. That overlay represents *finished, enriched*
entities — its `Product.Name` already carries `[Required]` + `[MaxLength(100)]`, which would make
the `add-validator` task pre-satisfied (a do-nothing agent would pass). Each task instead owns a
minimal overlay under `tasks/<tier>/<id>/fixture/` that defines only what the task needs, without
the solution. For `add-validator` that is a `Product` with a plain `Name`
(`tasks/atomic/add-validator/fixture/Product.cs`).

> Walking-skeleton note: with a single real task, the overlay is applied by the workflow while
> building the fixture. When a second task needs a different pre-state, lift this into the harness
> (apply `tasks/<tier>/<id>/fixture/**` during provisioning) instead of baking it into the shared
> fixture.

## Building it locally (only needed to author an oracle)

1. From a scratch dir: `spiderly init --name TestApp --db postgresql`.
2. Copy the task overlay onto the app, replacing the `__APP_NAME__` placeholder:
   - `cp tasks/atomic/add-validator/fixture/Product.cs <app>/Backend/TestApp.Business/Entities/Product.cs`
   - `sed -i 's/__APP_NAME__/TestApp/g' <app>/Backend/TestApp.Business/Entities/Product.cs`
3. `dotnet build` in `Backend/` to confirm the pre-task state compiles and to warm caches.
4. `rsync -a --exclude bin --exclude obj <app>/Backend tools/agent-evals/fixtures/spiderly-app/`.

## Authoring the oracle patch (`tools/agent-evals/oracle/add-validator/`)

The oracle patch is NOT committed yet — it must be authored by hand once, against a built fixture:

1. Copy the `spiderly-app` fixture to a scratch workspace.
2. Add the `Product.Name` validation the Spiderly way — the idiomatic form is DataAnnotations on the
   entity (`[Required]` + `[MaxLength(100)]`); a hand-written `RuleFor(x => x.Name).NotEmpty()
   .MaximumLength(100)` in a `ProductDTOValidationRules` partial is also accepted by the verifier.
   Confirm `dotnet build` succeeds and `verify.mjs` reports `name-required` + `name-max-100`.
3. Copy ONLY the changed files into `tools/agent-evals/oracle/add-validator/`, preserving their
   relative paths under the workspace root (e.g. `Backend/TestApp.Business/Entities/Product.cs`).

Until this patch exists, running `--agents oracle` on `add-validator` reports an infra error
(pass:null) by design — that is the signal the oracle still needs authoring.
