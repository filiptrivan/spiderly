using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Spiderly.CLI.Services;

namespace Spiderly.CLI.Commands
{
    /// <summary>
    /// Projects the version-pinned agent guidance shipped inside the installed <c>spiderly</c> npm
    /// package (<c>node_modules/spiderly/agent</c>) into the consumer project, so AI coding agents
    /// get accurate, version-matched Spiderly docs without a floating GitHub plugin install.
    ///
    /// Reconcile (not append): every run rewrites the marker-delimited Spiderly block in
    /// <c>AGENTS.md</c> from the bundle's <c>doc</c>-surface entries, ensures <c>CLAUDE.md</c>
    /// imports it, and makes <c>.claude/skills/spiderly-*</c> match the <c>skill</c>-surface
    /// entries (creating directory links and pruning stale ones — so rename/delete self-heal).
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
            public string Surface { get; set; }
            public string Description { get; set; }
        }

        /// <param name="projectRoot">Consumer project root; defaults to the current directory.</param>
        /// <param name="failIfMissing">
        /// When true (standalone <c>spiderly agent-sync</c>), a missing bundle is an error (exit 1).
        /// When false (called from <c>init</c>), it's a soft skip — the package may be older or, in
        /// <c>--dev</c> mode, referenced from local source rather than node_modules.
        /// </param>
        public static int Execute(string projectRoot = null, bool failIfMissing = true)
        {
            string cwd = projectRoot ?? Environment.CurrentDirectory;

            string manifestPath = ResolveManifestPath(cwd);
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

            string agentDir = Path.GetDirectoryName(manifestPath);
            string skillsDir = Path.Combine(agentDir, "skills");
            string relSkills = Path.GetRelativePath(cwd, skillsDir).Replace('\\', '/');
            string version = ReadPackageVersion(agentDir);

            List<SkillEntry> docs = Surface(manifest, "doc");
            List<SkillEntry> skillLinks = Surface(manifest, "skill");

            int created, pruned;
            try
            {
                WriteAgentsBlock(cwd, BuildBlock(docs, relSkills, version));
                EnsureClaudeImport(cwd);
                (created, pruned) = ReconcileSkillJunctions(cwd, skillsDir, skillLinks);
            }
            catch (Exception ex)
            {
                ConsoleHelper.MarkupLineERROR($"Failed to write agent guidance: {ex.Message}");
                return 1;
            }

            ConsoleHelper.MarkupLineOK(
                $"Synced {docs.Count} doc(s) into AGENTS.md, {skillLinks.Count} skill junction(s)" +
                (pruned > 0 ? $" ({pruned} stale pruned)" : "") +
                $", and ensured CLAUDE.md imports it" + (version != null ? $" (v{version})." : "."));
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

        private static string BuildBlock(List<SkillEntry> docs, string relSkills, string version)
        {
            var sb = new StringBuilder();
            sb.Append(BeginMarker).Append('\n');
            sb.Append("# Spiderly — read the docs before coding").Append(version != null ? $" (v{version})" : "").Append('\n');
            sb.Append('\n');
            sb.Append("Your training data is stale. Before any Spiderly work, read the matching reference at\n");
            sb.Append($"`{relSkills}/<name>/SKILL.md` (version-matched to the installed package):\n");
            sb.Append('\n');
            foreach (SkillEntry d in docs)
                sb.Append($"- **{d.Name}** — {d.Description}\n");
            sb.Append(EndMarker);
            return sb.ToString();
        }

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

        private static List<SkillEntry> Surface(Manifest manifest, string surface) =>
            manifest.Skills
                .Where(s => string.Equals(s.Surface, surface, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Name, StringComparer.Ordinal)
                .ToList();

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
