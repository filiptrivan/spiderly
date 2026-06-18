using Spiderly.CLI.Commands;

namespace Spiderly.CLI.Tests;

/// <summary>
/// Unit tests for the pure decision logic behind `spiderly agent-sync`'s workspace target
/// (extracted into internal helpers so they're testable without the filesystem).
/// </summary>
public class AgentRootLogicTests
{
    #region ExtractAgentRoot

    [Fact]
    public void ExtractAgentRoot_ReadsAgentSyncRoot()
        => Assert.Equal("..", AgentSyncCommand.ExtractAgentRoot("""{"agentSync":{"root":".."}}"""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("{}")]
    [InlineData("""{"agentSync":{}}""")]
    [InlineData("""{"agentSync":{"root":""}}""")]
    [InlineData("""{"agentSync":{"root":123}}""")]
    public void ExtractAgentRoot_ReturnsNull_WhenAbsentOrInvalid(string? json)
        => Assert.Null(AgentSyncCommand.ExtractAgentRoot(json!));

    [Fact]
    public void ExtractAgentRoot_ToleratesCommentsAndTrailingCommas()
        => Assert.Equal("../ws", AgentSyncCommand.ExtractAgentRoot(
            """
            {
              // machine-local
              "agentSync": { "root": "../ws", },
            }
            """));

    #endregion

    #region ResolveAgentRootPath (precedence + relative resolution)

    [Fact]
    public void Resolve_ExplicitFlag_WinsOverConfig()
    {
        string src = Root();
        Assert.Equal(
            Path.GetFullPath(Path.Combine(src, "flagdir")),
            AgentSyncCommand.ResolveAgentRootPath(src, explicitAgentRoot: "flagdir", fromConfig: "configdir"));
    }

    [Fact]
    public void Resolve_Config_UsedWhenNoFlag()
    {
        string src = Root();
        Assert.Equal(
            Path.GetFullPath(Path.Combine(src, "configdir")),
            AgentSyncCommand.ResolveAgentRootPath(src, explicitAgentRoot: null, fromConfig: "configdir"));
    }

    [Fact]
    public void Resolve_DefaultsToSource_WhenNeitherSet()
    {
        string src = Root();
        Assert.Equal(Path.GetFullPath(src), AgentSyncCommand.ResolveAgentRootPath(src, null, null));
    }

    [Fact]
    public void Resolve_RelativeDotDot_TargetsParentWorkspace()
    {
        string parent = Root();
        string src = Path.Combine(parent, "app");
        Assert.Equal(Path.GetFullPath(parent), AgentSyncCommand.ResolveAgentRootPath(src, "..", null));
    }

    [Fact]
    public void Resolve_AbsoluteValue_PassesThrough()
    {
        string elsewhere = Path.Combine(Root(), "elsewhere");
        Assert.Equal(Path.GetFullPath(elsewhere), AgentSyncCommand.ResolveAgentRootPath(Root(), elsewhere, null));
    }

    #endregion

    #region MergeAgentRoot (preserve other keys)

    [Fact]
    public void Merge_PreservesOtherKeys()
    {
        string merged = AgentSyncCommand.MergeAgentRoot("""{"generators":{"X":false},"agentSync":{"root":"old"}}""", "..");
        Assert.Equal("..", AgentSyncCommand.ExtractAgentRoot(merged));
        Assert.Contains("generators", merged);
        Assert.Contains("\"X\"", merged);
    }

    [Fact]
    public void Merge_CreatesStructure_FromNullOrEmpty()
    {
        Assert.Equal("..", AgentSyncCommand.ExtractAgentRoot(AgentSyncCommand.MergeAgentRoot(null, "..")));
        Assert.Equal("..", AgentSyncCommand.ExtractAgentRoot(AgentSyncCommand.MergeAgentRoot("", "..")));
    }

    [Fact]
    public void Merge_OverwritesExistingRoot()
        => Assert.Equal("new", AgentSyncCommand.ExtractAgentRoot(
            AgentSyncCommand.MergeAgentRoot("""{"agentSync":{"root":"old"}}""", "new")));

    #endregion

    #region IsPatternIgnored

    [Theory]
    [InlineData(".spiderly/*.local.json")]
    [InlineData("**/.spiderly/*.local.json")]
    [InlineData("node_modules/\n.spiderly/*.local.json\n")]
    [InlineData("  .spiderly/*.local.json  ")]
    [InlineData("a\r\n.spiderly/*.local.json\r\nb")]
    public void IsPatternIgnored_True_WhenListed(string content)
        => Assert.True(AgentSyncCommand.IsPatternIgnored(content, ".spiderly/*.local.json"));

    [Theory]
    [InlineData("")]
    [InlineData("node_modules/\n")]
    [InlineData(".spiderly/config.json")]
    public void IsPatternIgnored_False_WhenAbsent(string content)
        => Assert.False(AgentSyncCommand.IsPatternIgnored(content, ".spiderly/*.local.json"));

    #endregion

    // A normalized absolute path that is never created on disk — these tests do no I/O.
    private static string Root() => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "spiderly-cli-unit-root"));
}
