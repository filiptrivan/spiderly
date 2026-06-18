using Spiderly.CLI.Commands;

namespace Spiderly.CLI.Tests;

/// <summary>
/// End-to-end check of the source/target split + persistence against a real temp filesystem:
/// a nested "app" (bundle source) inside an umbrella "workspace" (write target).
/// </summary>
public class AgentSyncIntegrationTests : IDisposable
{
    private readonly string _ws;   // umbrella / write target
    private readonly string _app;  // nested consumer project / bundle source

    public AgentSyncIntegrationTests()
    {
        _ws = Path.Combine(Path.GetTempPath(), "spiderly-agentsync-" + Guid.NewGuid().ToString("N"));
        _app = Path.Combine(_ws, "app");
        string bundle = Path.Combine(_app, "node_modules", "spiderly", "agent");
        Directory.CreateDirectory(Path.Combine(bundle, "skills", "add-entity"));
        Directory.CreateDirectory(Path.Combine(bundle, "skills", "ef-migrations"));
        Directory.CreateDirectory(Path.Combine(bundle, "docs"));
        File.WriteAllText(Path.Combine(_app, "node_modules", "spiderly", "package.json"),
            """{"name":"spiderly","version":"0.0.0-test"}""");
        File.WriteAllText(Path.Combine(bundle, "manifest.json"),
            """{"skills":[{"name":"add-entity","description":"a"},{"name":"ef-migrations","description":"b"}]}""");
        File.WriteAllText(Path.Combine(bundle, "skills", "add-entity", "SKILL.md"), "# a");
        File.WriteAllText(Path.Combine(bundle, "skills", "ef-migrations", "SKILL.md"), "# b");
        File.WriteAllText(Path.Combine(bundle, "docs", "entity-design.md"), "# d");
        File.WriteAllText(Path.Combine(_app, ".gitignore"), "node_modules/\n");
    }

    [Fact]
    public void SaveThenBareRun_ProjectsIntoWorkspaceRoot_AndPersists()
    {
        // RUN 1: from the nested app, project into the umbrella (..) and persist the choice.
        Assert.Equal(0, AgentSyncCommand.Execute(projectRoot: _app, agentRoot: "..", saveAgentRoot: true));

        // Guidance landed at the TARGET (umbrella), not the nested app.
        Assert.True(File.Exists(Path.Combine(_ws, "AGENTS.md")));
        Assert.False(File.Exists(Path.Combine(_app, "AGENTS.md")));
        Assert.Contains("@AGENTS.md", File.ReadAllText(Path.Combine(_ws, "CLAUDE.md")));

        // Docs pointer is relative to the target and correctly crosses into the nested app.
        Assert.Contains("app/node_modules/spiderly/agent/docs", File.ReadAllText(Path.Combine(_ws, "AGENTS.md")));

        // One junction per skill-surface entry, at the target.
        Assert.True(Directory.Exists(Path.Combine(_ws, ".claude", "skills", "spiderly-add-entity")));
        Assert.True(Directory.Exists(Path.Combine(_ws, ".claude", "skills", "spiderly-ef-migrations")));

        // Persisted to machine-local config in the source app, and gitignored.
        string localCfg = Path.Combine(_app, ".spiderly", "config.local.json");
        Assert.Equal("..", AgentSyncCommand.ExtractAgentRoot(File.ReadAllText(localCfg)));
        Assert.True(AgentSyncCommand.IsPatternIgnored(File.ReadAllText(Path.Combine(_app, ".gitignore")), ".spiderly/*.local.json"));

        // RUN 2: BARE (no flags) — must reuse the persisted root and re-project to the umbrella.
        File.Delete(Path.Combine(_ws, "AGENTS.md"));
        Assert.Equal(0, AgentSyncCommand.Execute(projectRoot: _app));
        Assert.True(File.Exists(Path.Combine(_ws, "AGENTS.md")));    // back at the umbrella
        Assert.False(File.Exists(Path.Combine(_app, "AGENTS.md")));  // no leak into the app
    }

    public void Dispose()
    {
        try { Directory.Delete(_ws, recursive: true); } catch { /* best-effort temp cleanup */ }
    }
}
