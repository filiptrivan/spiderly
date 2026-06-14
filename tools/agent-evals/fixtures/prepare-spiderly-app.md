# Preparing the `spiderly-app` fixture (one-time, local)

The e2e overlay (`tests/e2e-fixtures/setup.sh`) populates an already-init'd app, so the
fixture must be scaffolded first, then kept local (gitignored).

1. From a scratch dir: `spiderly init` to scaffold `Backend/` + `Frontend/`.
2. Run `tests/e2e-fixtures/setup.sh <AppName> <appFolder>` to overlay entities (Product, etc.).
3. `npm install` in `Frontend/` and `dotnet restore` in `Backend/` so builds are warm.
4. Copy the result to `tools/agent-evals/fixtures/spiderly-app/`.

This fixture is large; keep it gitignored (`tools/agent-evals/fixtures/spiderly-app/`
is in .gitignore) and document the regen steps here rather than committing the whole app.

## Authoring the oracle patch (`tools/agent-evals/oracle/add-validator/`)

The oracle patch is NOT committed yet — it must be authored by hand once:
1. Copy the `spiderly-app` fixture to a scratch workspace.
2. Implement the Product.Name validation the Spiderly way (consult the `validation` /
   `attribute-reference` docs). Confirm `dotnet build` in `Backend/` succeeds.
3. Copy ONLY the changed files into `tools/agent-evals/oracle/add-validator/`, preserving
   their relative paths under the workspace root (e.g. `Backend/<App>.Business/...`).
Until this patch exists, running `--agents oracle` on `add-validator` reports an infra
error (pass:null) by design — that is the signal the oracle still needs authoring.
