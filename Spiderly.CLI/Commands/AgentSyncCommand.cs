using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spiderly.CLI.Services;

namespace Spiderly.CLI.Commands
{
    /// <summary>
    /// Projects the version-pinned agent guidance shipped inside the installed <c>spiderly</c> npm
    /// package (<c>node_modules/spiderly/agent</c>) into the consumer project, so AI coding agents
    /// get accurate, version-matched Spiderly docs without a floating GitHub plugin install.
    ///
    /// Reconcile (not append): every run rewrites the marker-delimited Spiderly block in
    /// <c>AGENTS.md</c> as a static pointer to the bundle's <c>docs/</c> directory, ensures
    /// <c>CLAUDE.md</c> imports it, and makes <c>.claude/skills/spiderly-*</c> match the
    /// manifest's skill entries (creating directory links and pruning stale ones — so
    /// rename/delete self-heal).
    /// See <c>docs/agent-guidance-distribution.md</c>.
    /// </summary>
    internal static class AgentSyncCommand
    {
        private const string BeginMarker = "<!-- BEGIN:spiderly -->";
        private const string EndMarker = "<!-- END:spiderly -->";
        private const string ClaudeImport = "@AGENTS.md";
        // Namespaces our junctions so reconcile prunes only Spiderly's links, never the user's own skills.
        private const string JunctionPrefix = "spiderly-";

        private sealed class Manifest
        {
            public List<SkillEntry> Skills { get; set; } = new();
        }

        private sealed class SkillEntry
        {
            public string Name { get; set; }
            public string Description { get; set; }
        }

        /// <param name="projectRoot">Consumer project root (where the npm bundle is resolved); defaults to the current directory.</param>
        /// <param name="agentRoot">
        /// Where the agent guidance is *written* (AGENTS.md, the CLAUDE.md import, and
        /// .claude/skills/spiderly-*). Defaults to <paramref name="projectRoot"/>; set it (via
        /// <c>--agent-root</c>) to an outer workspace/umbrella root that nests the consumer project.
        /// Relative values resolve against <paramref name="projectRoot"/>. When not passed, falls back
        /// to <c>agentSync.root</c> in <c>.spiderly/config.local.json</c> then <c>.spiderly/config.json</c>.
        /// </param>
        /// <param name="saveAgentRoot">
        /// When true and <paramref name="agentRoot"/> is set, persists it to the machine-local
        /// <c>.spiderly/config.local.json</c> (gitignored) so later bare runs reuse it.
        /// </param>
        /// <param name="failIfMissing">
        /// When true (standalone <c>spiderly agent-sync</c>), a missing bundle is an error (exit 1).
        /// When false (called from <c>init</c>), it's a soft skip — the package may be older or, in
        /// <c>--dev</c> mode, referenced from local source rather than node_modules.
        /// </param>
        public static int Execute(string projectRoot = null, string agentRoot = null, bool saveAgentRoot = false, bool failIfMissing = true)
        {
            string source = projectRoot ?? Environment.CurrentDirectory;

            string manifestPath = ResolveManifestPath(source);
            if (manifestPath == null)
            {
                const string where = "the Spiderly agent bundle (node_modules/spiderly/agent/manifest.json under the current directory or 'Frontend/')";
                if (failIfMissing)
                {
                    ConsoleHelper.MarkupLineERROR(
                        $"Could not find {where}. Install the npm package first (run your package manager " +
                        "install in the Angular project), then re-run 'spiderly agent-sync'.");
                    return 1;
                }
                ConsoleHelper.MarkupLineWARNING(
                    $"Could not find {where} — skipping agent guidance sync. Run 'spiderly agent-sync' once the package is installed.");
                return 0;
            }

            Manifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<Manifest>(
                    File.ReadAllText(manifestPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                ConsoleHelper.MarkupLineERROR($"Failed to read the agent bundle manifest at '{manifestPath}': {ex.Message}");
                return 1;
            }

            if (manifest is null || manifest.Skills.Count == 0)
            {
                ConsoleHelper.MarkupLineERROR($"The agent bundle manifest at '{manifestPath}' contains no skills.");
                return 1;
            }

            // The bundle is resolved from `source` (the consumer project that has node_modules), but
            // agent guidance is *projected* into `target`. These differ when the AI agent runs from an
            // outer workspace/umbrella root that nests the consumer project. Resolution: --agent-root >
            // .spiderly/config.local.json (agentSync.root, machine-local) > .spiderly/config.json >
            // `source` itself (the original, same-directory behavior).
            string target = ResolveAgentRoot(source, agentRoot);

            if (saveAgentRoot && !string.IsNullOrWhiteSpace(agentRoot))
                SaveAgentRoot(source, agentRoot);

            string agentDir = Path.GetDirectoryName(manifestPath);
            string docsDir = Path.Combine(agentDir, "docs");
            string skillsDir = Path.Combine(agentDir, "skills");
            string relDocs = Path.GetRelativePath(target, docsDir).Replace('\\', '/');
            string version = ReadPackageVersion(agentDir);

            // manifest.json now lists skill-surface entries only — every entry is junctioned.
            List<SkillEntry> skillLinks = manifest.Skills;

            int created, pruned;
            try
            {
                WriteAgentsBlock(target, BuildBlock(relDocs));
                EnsureClaudeImport(target);
                (created, pruned) = ReconcileSkillJunctions(target, skillsDir, skillLinks);
            }
            catch (Exception ex)
            {
                ConsoleHelper.MarkupLineERROR($"Failed to write agent guidance: {ex.Message}");
                return 1;
            }

            ConsoleHelper.MarkupLineOK(
                $"Synced AGENTS.md docs pointer + {skillLinks.Count} skill junction(s)" +
                (pruned > 0 ? $" ({pruned} stale pruned)" : "") +
                $", and ensured CLAUDE.md imports it" + (version != null ? $" (v{version})." : "."));
            if (!PathsEqual(source, target))
                ConsoleHelper.MarkupLineOK($"Projected into workspace root: {Path.GetFullPath(target)}");
            return 0;
        }

        /// <summary>Finds <c>node_modules/spiderly/agent/manifest.json</c> from the consumer root or its Frontend/ project.</summary>
        private static string ResolveManifestPath(string cwd)
        {
            string[] candidates =
            {
                Path.Combine(cwd, "Frontend", "node_modules", "spiderly", "agent", "manifest.json"),
                Path.Combine(cwd, "node_modules", "spiderly", "agent", "manifest.json"),
            };
            return Array.Find(candidates, File.Exists);
        }

        /// <summary>
        /// Resolves where agent guidance is written: explicit <paramref name="explicitAgentRoot"/> >
        /// <c>.spiderly/config.local.json</c> (agentSync.root) > <c>.spiderly/config.json</c> >
        /// <paramref name="source"/> itself. Relative values resolve against the source project, so
        /// <c>".."</c> targets the parent workspace. Returns an absolute path.
        /// </summary>
        private static string ResolveAgentRoot(string source, string explicitAgentRoot)
            => ResolveAgentRootPath(source, explicitAgentRoot, ReadAgentRootFromConfig(source));

        /// <summary>Pure precedence + path resolution (no I/O): explicit > fromConfig > source itself.</summary>
        internal static string ResolveAgentRootPath(string source, string explicitAgentRoot, string fromConfig)
        {
            string value = !string.IsNullOrWhiteSpace(explicitAgentRoot) ? explicitAgentRoot
                : !string.IsNullOrWhiteSpace(fromConfig) ? fromConfig
                : null;

            if (string.IsNullOrWhiteSpace(value))
                return Path.GetFullPath(source);

            return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(source, value));
        }

        /// <summary>
        /// Reads <c>agentSync.root</c> from <c>.spiderly/config.local.json</c> (machine-local) then
        /// <c>.spiderly/config.json</c> (committed) under <paramref name="source"/>; local overrides
        /// committed. Returns null when neither sets it.
        /// </summary>
        private static string ReadAgentRootFromConfig(string source)
        {
            foreach (string fileName in new[] { "config.local.json", "config.json" })
            {
                string path = Path.Combine(source, ".spiderly", fileName);
                if (!File.Exists(path))
                    continue;
                string content;
                try { content = File.ReadAllText(path); }
                catch { continue; }
                string value = ExtractAgentRoot(content);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }

        /// <summary>Pure: extracts <c>agentSync.root</c> from a config JSON string; null if absent/malformed.</summary>
        internal static string ExtractAgentRoot(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(
                    json,
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.TryGetProperty("agentSync", out JsonElement agentSync) &&
                    agentSync.TryGetProperty("root", out JsonElement root) &&
                    root.ValueKind == JsonValueKind.String)
                {
                    string value = root.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            catch
            {
                // Malformed config — treat as "not set".
            }
            return null;
        }

        /// <summary>
        /// Persists <c>agentSync.root</c> into <c>.spiderly/config.local.json</c> (machine-local) under
        /// <paramref name="source"/>, merging with any existing content, and ensures it is gitignored.
        /// </summary>
        private static void SaveAgentRoot(string source, string agentRoot)
        {
            string dir = Path.Combine(source, ".spiderly");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "config.local.json");

            string existing = File.Exists(path) ? File.ReadAllText(path) : null;
            File.WriteAllText(path, MergeAgentRoot(existing, agentRoot) + "\n", new UTF8Encoding(false));

            EnsureLocalConfigIgnored(source);
        }

        /// <summary>Pure: sets <c>agentSync.root</c> in the given config JSON, preserving other keys; returns new JSON.</summary>
        internal static string MergeAgentRoot(string existingJson, string agentRoot)
        {
            JsonObject root;
            try
            {
                root = string.IsNullOrWhiteSpace(existingJson)
                    ? new JsonObject()
                    : JsonNode.Parse(
                        existingJson,
                        documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }

            JsonObject agentSync = root["agentSync"] as JsonObject ?? new JsonObject();
            agentSync["root"] = agentRoot;
            root["agentSync"] = agentSync;

            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>Ensures <paramref name="source"/>'s .gitignore excludes machine-local <c>.spiderly/*.local.json</c>.</summary>
        private static void EnsureLocalConfigIgnored(string source)
        {
            const string pattern = ".spiderly/*.local.json";
            string path = Path.Combine(source, ".gitignore");
            string existing = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";

            if (IsPatternIgnored(existing, pattern))
                return;

            string sep = existing.Length == 0 || existing.EndsWith("\n") ? "" : "\n";
            File.AppendAllText(path, $"{sep}\n# Spiderly machine-local config (agent-sync workspace target, etc.)\n{pattern}\n");
        }

        /// <summary>Pure: true if a gitignore body already lists <paramref name="pattern"/> (with or without a leading **/).</summary>
        internal static bool IsPatternIgnored(string gitignoreContent, string pattern)
        {
            if (string.IsNullOrEmpty(gitignoreContent))
                return false;
            return gitignoreContent.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.Trim())
                .Any(t => t == pattern || t == "**/" + pattern);
        }

        private static string ReadPackageVersion(string agentDir)
        {
            try
            {
                string packageJson = Path.Combine(Path.GetDirectoryName(agentDir), "package.json");
                if (!File.Exists(packageJson)) return null;
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(packageJson));
                return doc.RootElement.TryGetProperty("version", out JsonElement v) ? v.GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        private static string BuildBlock(string relDocs) =>
            $$"""
            {{BeginMarker}}
            # Spiderly

            Your training data for Spiderly is stale. Before writing any Spiderly code, browse
            `{{relDocs}}/` and read the `SKILL.md` for the topic you're working on — these docs are
            version-matched to the installed Spiderly package.
            {{EndMarker}}
            """;

        /// <summary>Rewrites the marker-delimited Spiderly block in AGENTS.md, preserving any content outside it.</summary>
        private static void WriteAgentsBlock(string cwd, string block)
        {
            string path = Path.Combine(cwd, "AGENTS.md");

            if (!File.Exists(path))
            {
                WriteAllTextLf(path, block + "\n");
                return;
            }

            string existing = File.ReadAllText(path).Replace("\r\n", "\n");
            int begin = existing.IndexOf(BeginMarker, StringComparison.Ordinal);
            int end = existing.IndexOf(EndMarker, StringComparison.Ordinal);

            string updated;
            if (begin != -1 && end != -1 && end > begin)
            {
                int endAfter = end + EndMarker.Length;
                updated = existing.Substring(0, begin) + block + existing.Substring(endAfter);
            }
            else
            {
                string sep = existing.Length == 0 || existing.EndsWith("\n") ? "" : "\n";
                updated = existing + sep + "\n" + block + "\n";
            }

            WriteAllTextLf(path, updated);
        }

        /// <summary>Ensures CLAUDE.md imports AGENTS.md via the '@AGENTS.md' directive.</summary>
        private static void EnsureClaudeImport(string cwd)
        {
            string path = Path.Combine(cwd, "CLAUDE.md");

            if (!File.Exists(path))
            {
                WriteAllTextLf(path, ClaudeImport + "\n");
                return;
            }

            string existing = File.ReadAllText(path).Replace("\r\n", "\n");
            if (existing.Contains(ClaudeImport, StringComparison.Ordinal))
                return;

            string sep = existing.Length == 0 || existing.StartsWith("\n") ? "" : "\n";
            WriteAllTextLf(path, ClaudeImport + "\n" + sep + existing);
        }

        private static void WriteAllTextLf(string path, string content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        /// <summary>
        /// Makes .claude/skills/spiderly-* match the manifest's skill-surface entries: creates/refreshes
        /// a directory link per skill and prunes any spiderly-* link no longer in the manifest. The
        /// 'spiderly-' prefix scopes pruning to our own links. Returns (created, pruned).
        /// </summary>
        private static (int created, int pruned) ReconcileSkillJunctions(string cwd, string skillsDir, List<SkillEntry> skills)
        {
            string claudeSkillsDir = Path.Combine(cwd, ".claude", "skills");
            Directory.CreateDirectory(claudeSkillsDir);

            Dictionary<string, string> desired = skills.ToDictionary(
                s => JunctionPrefix + s.Name,
                s => Path.GetFullPath(Path.Combine(skillsDir, s.Name)),
                StringComparer.Ordinal);

            int created = 0, pruned = 0;

            // Prune stale spiderly-* links no longer desired (handles rename/delete).
            foreach (string dir in Directory.GetDirectories(claudeSkillsDir))
            {
                string name = Path.GetFileName(dir);
                if (!name.StartsWith(JunctionPrefix, StringComparison.Ordinal)) continue;
                if (desired.ContainsKey(name)) continue;
                if (IsLink(dir)) { Directory.Delete(dir); pruned++; } // never clobber a real dir the user made
            }

            // Create / refresh desired links.
            foreach (KeyValuePair<string, string> entry in desired)
            {
                string link = Path.Combine(claudeSkillsDir, entry.Key);
                string target = entry.Value;

                if (Directory.Exists(link) || File.Exists(link))
                {
                    if (!IsLink(link)) continue; // a real dir/file with our name — don't clobber
                    if (PathsEqual(TryResolveLinkTarget(link), target)) continue; // already correct — idempotent
                    Directory.Delete(link);
                }

                CreateDirectoryLink(link, target);
                created++;
            }

            return (created, pruned);
        }

        private static bool IsLink(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return false; }
        }

        private static string TryResolveLinkTarget(string path)
        {
            try { return Directory.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName; }
            catch { return null; }
        }

        private static bool PathsEqual(string a, string b)
        {
            if (a == null || b == null) return false;
            static string Norm(string p) => Path.GetFullPath(p).TrimEnd('\\', '/');
            StringComparison cmp = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(Norm(a), Norm(b), cmp);
        }

        private static void CreateDirectoryLink(string link, string target)
        {
            // Windows: directory junction (mklink /J) — works without admin/Developer Mode, unlike a
            // symlink. Other platforms: a normal directory symlink.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                RunQuiet("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"");
            else
                Directory.CreateSymbolicLink(link, target);
        }

        private static void RunQuiet(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process p = Process.Start(psi);
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new Exception($"'{fileName} {arguments}' failed (exit {p.ExitCode}): {err.Trim()}");
        }
    }
}
