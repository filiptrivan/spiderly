---
name: ai-agentic-design
description: Design principles for building AI-agent-friendly Spiderly features. Use when working on Spiderly internals — CLI commands, source generators, framework code — or reviewing contributions for AI compatibility.
---

# AI-Agentic Design Principles

Spiderly is an AI-agentic framework. Every feature must be drivable by an AI agent (e.g., Claude Code) without human intervention. These principles guide all CLI, generator, and framework development.

## Principles

### 1. Non-Interactive by Default

Every CLI command must work fully via flags. Interactive prompts are convenience sugar on top.

| Do                                                                                           | Don't                                                          |
| -------------------------------------------------------------------------------------------- | -------------------------------------------------------------- |
| Add `--flag` first, then the interactive fallback                                            | Default to interactive prompt, add `--flag` as an afterthought |
| Guard with `ConsoleHelper.IsInteractive()` before any `AnsiConsole.Prompt()`                 | Call `AnsiConsole.Prompt()` unconditionally                    |
| Emit `MarkupLineERROR` with usage hint when required flag is missing in non-interactive mode | Silently hang waiting for input                                |

**Pattern:**

```csharp
if (string.IsNullOrWhiteSpace(flagValue))
{
    if (!ConsoleHelper.IsInteractive())
    {
        ConsoleHelper.MarkupLineERROR("Flag --name is required in non-interactive mode. Use: spiderly command --name Value");
        return 1;
    }

    flagValue = AnsiConsole.Prompt(new TextPrompt<string>("Enter value:"));
}
```

**Rule:** If you add a new parameter to any command, add the corresponding `--flag` first, then the interactive fallback. Never the other way around.

### 2. Fail Loudly

Non-zero exit codes on any failure. Never emit WARNING and return exit code 0 in non-interactive mode.

| Mode            | On partial failure                                     |
| --------------- | ------------------------------------------------------ |
| Interactive     | `MarkupLineWARNING` + continue (user can fix manually) |
| Non-interactive | `MarkupLineERROR` + set `hasErrors = true` + return 1  |

**Anti-pattern** (from the old `AddNewEntityCommand`):

```csharp
// BAD: Returns void, always exits 0. AI agent thinks everything succeeded.
ConsoleHelper.MarkupLineWARNING("Could not find location to insert routes.");
```

**Correct pattern:**

```csharp
if (!ConsoleHelper.IsInteractive())
{
    ConsoleHelper.MarkupLineERROR("Could not find location to insert routes. Route injection failed.");
    hasErrors = true;
}
else
{
    ConsoleHelper.MarkupLineWARNING("Could not find location to insert routes. Please add them manually.");
}
```

**Rule:** Every command that can fail must return `Task<int>`. Program.cs must propagate the exit code.

### 3. Prerequisite Validation Upfront

Check all requirements before starting work. Never discover a missing tool mid-way through a multi-step command.

**Pattern:**

```csharp
// At the TOP of Execute(), before any file generation or network calls:
if (!await PrerequisiteChecker.ValidatePrerequisites())
{
    return 1;
}
```

**Rule:** If a command needs .NET, Node.js, Docker, or any external tool — validate it before generating files, running migrations, or installing packages. Wasting 2-3 minutes of file generation only to fail on `npm install` is unacceptable.

### 4. Docker-First for Infrastructure

In non-interactive mode, auto-provision dependencies via Docker without prompting.

| Mode            | Behavior when dependency is missing           |
| --------------- | --------------------------------------------- |
| Interactive     | Ask permission: "Install via Docker?"         |
| Non-interactive | Auto-start Docker container, retry connection |

**Rule:** Interactive mode asks permission; non-interactive mode acts. If Docker is unavailable, fail with a clear message suggesting `--db-connection-string` or `--db skip`.

### 5. Documentation = AI Instructions

Getting-started docs and CLI reference must be precise enough for an AI agent to follow verbatim.

**Checklist for every CLI change:**

- [ ] Every flag documented in CLI reference with description and example
- [ ] Non-interactive usage shown alongside interactive usage
- [ ] Prerequisite verification commands listed with expected output
- [ ] Error scenarios documented with recovery steps

### 6. Fail at Build Time, Not at Runtime

When a source generator detects user code that would produce invalid output (broken TypeScript references, missing imports, unresolvable types, impossible mappings), emit a compiler diagnostic via `context.ReportDiagnostic`. Don't silently emit broken code and let the failure surface at `ng build`, typecheck, or runtime — by then the connection to the root cause is lost.

**Pattern:**

```csharp
context.ReportDiagnostic(Diagnostic.Create(
    SpiderlyDiagnostics.UnresolvableControllerType,
    method.Location ?? Location.None,
    kind, cSharpType, controllerName, methodName));
```

**Rules:**

- All diagnostic descriptors live in `Spiderly.SourceGenerators/Shared/SpiderlyDiagnostics.cs`.
- IDs are `SPIDERLYNNN` — sequential, stable, never reused (even if a rule is removed).
- Default severity is `Error` unless the generated output is merely suboptimal (then `Warning`).
- The message must name the offending symbol and suggest the concrete fix.
- Preserve syntax `Location` through the pipeline so the diagnostic squiggles on the right line — if a model object loses location info, add a `Location` property.

**Rule:** A generator that produces invalid output without a diagnostic is a bug, not a feature. If you find one, add the diagnostic in the same change.

### 6a. Classification is by attribute

Source generators classify classes by marker attribute. Namespace conventions (`*.Entities`, `*.DTO`, `*.Services`, `*.DataMappers`) are organizational preference and drive where generated code lands, but they do **not** enroll classes in generation.

| Class kind | Marker attribute |
|---|---|
| Entity | `[SpiderlyEntity]` |
| Hand-written DTO | `[SpiderlyDTO]` |
| Custom controller | `[SpiderlyController]` |
| Entity service (extends `{Entity}ServiceGenerated`) | `[SpiderlyService]` |
| Hand-written partial mapper | `[SpiderlyDataMapper]` |
| C# enum / class-based enum exposed to Angular | `[SpiderlyEnum]` |

**Rules:**

- Any class the generator must process needs the matching attribute. Forgetting it means no generated output for that class; the consumer's own C# code will fail to compile and surface the omission.
- Static classes, constants containers, enums, interfaces, and helpers are inert under classification — place them anywhere, including under `*.DTO` or `*.Entities` namespaces if organizationally convenient.
- When adding a new generator, filter via the dedicated `Has*Attribute()` extensions in `Spiderly.SourceGenerators.Shared.Extensions`.

### 7. The AI Agent Test

Before merging any CLI or framework feature, answer these questions:

| Question                                               | Required answer        |
| ------------------------------------------------------ | ---------------------- |
| Can an AI agent drive this without human intervention? | Yes                    |
| Does it have flags for all inputs?                     | Yes                    |
| Does it return proper exit codes on failure?           | Yes                    |
| Does it validate prerequisites before doing work?      | Yes                    |
| Is the behavior documented in CLI reference?           | Yes                    |
| Does non-interactive mode auto-provision dependencies? | Yes (where applicable) |

## Quick Reference: CLI Command Checklist

When adding or modifying a CLI command:

1. Define all parameters as `--flag` options in `Program.cs` (`GetArgValue`)
2. Add `ConsoleHelper.IsInteractive()` guard before every `AnsiConsole.Prompt()`
3. Return `Task<int>` (not `Task`)
4. Propagate exit code in `Program.cs`
5. Track failures with `bool hasErrors = false`
6. Use `MarkupLineERROR` (not WARNING) in non-interactive mode for any failure
7. Add flags, descriptions, and examples to `HelpCommand.cs`
8. Document in `cli-reference.mdx`
9. Call `PrerequisiteChecker.ValidatePrerequisites()` if the command needs external tools
