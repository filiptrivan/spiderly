using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Net;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Model-wide guard over blob key prefixes. Returns diagnostics rather than throwing, because
    /// unlike the per-entity validators it emits two different KINDS of finding and must be able
    /// to report several: <c>SPIDERLY030</c> (Error — a prefix that loses files) and
    /// <c>SPIDERLY031</c> (Warning — a prefix that merely departs from house style). See
    /// <see cref="SpiderlyDiagnostics.InvalidBlobKeyPrefix"/> for why those are separate ids.
    /// <para>
    /// The which-path-does-a-custom-prefix-bind-to rule is NOT re-derived here: it is
    /// <see cref="Extensions.GetEffectiveKeyPrefix"/>, shared with
    /// <see cref="ServiceSaveGenerator.GetBlobKeyPrefixExpression"/>. A second spelling of it made
    /// the validator pass while the generator emitted a different prefix — a guard that stops
    /// guarding without failing, which is worse than no guard.
    /// </para>
    /// </summary>
    public static class BlobKeyPrefixValidator
    {
        /// <summary>
        /// Slash-separated segments of unreserved ASCII. This is the MECHANISM bar, not the style
        /// bar: it permits uppercase, underscores and dots (all legal keys everywhere Spiderly
        /// stores blobs) and rejects only what genuinely breaks — whitespace and non-ASCII would
        /// percent-encode in the public URL, and an empty segment means a leading, trailing or
        /// doubled slash, which changes the key depth that cleanup lists by.
        /// <para>
        /// Not <c>RegexOptions.Compiled</c>: measured, compiling costs ~10,9 ms of Reflection.Emit
        /// at first use against ~0,3 ms interpreted, and saves ~3,6 µs per validation run —
        /// break-even is thousands of generation passes in one Roslyn session, so every build
        /// would pay the emit and never recover it.
        /// </para>
        /// </summary>
        private static readonly Regex UsableAsKeyPrefix = new(@"^[A-Za-z0-9._-]+(/[A-Za-z0-9._-]+)*$");

        public static List<Diagnostic> Validate(List<SpiderlyClass> entities)
        {
            List<Diagnostic> diagnostics = new();
            List<(string Prefix, SpiderlyClass Entity, SpiderlyProperty Property)> effectivePrefixes = new();

            foreach (SpiderlyClass entity in entities)
            {
                foreach (SpiderlyProperty property in Helpers.GetBlobProperties(entity.Properties))
                {
                    string? customPrefix = property.GetBlobKeyPrefix();

                    if (customPrefix != null)
                        ValidateCustomPrefix(customPrefix, entity, property, diagnostics);

                    effectivePrefixes.Add((property.GetEffectiveKeyPrefix(entity.Name), entity, property));

                    if (property.IsEditorImageProperty())
                    {
                        effectivePrefixes.Add((
                            property.GetEffectiveKeyPrefix(entity.Name, isEditorImagePath: true),
                            entity, property));
                    }
                }
            }

            ReportCollisions(effectivePrefixes, diagnostics);

            return diagnostics;
        }

        private static void ValidateCustomPrefix(
            string prefix, SpiderlyClass entity, SpiderlyProperty property, List<Diagnostic> diagnostics)
        {
            Location? location = property.Location ?? entity.Location;

            if (!UsableAsKeyPrefix.IsMatch(prefix))
            {
                diagnostics.Add(Diagnostic.Create(
                    SpiderlyDiagnostics.InvalidBlobKeyPrefix, location,
                    prefix, entity.Name, property.Name,
                    "unusable as a storage key. Use slash-separated segments of ASCII letters, digits, '.', '_' or '-' — no whitespace, no non-ASCII (it would percent-encode in the public URL), and no leading, trailing or doubled slash."));
                return;
            }

            if (prefix.Split('/').Any(segment => segment == BlobKeyConventionsMirror.StagingSegment))
            {
                diagnostics.Add(Diagnostic.Create(
                    SpiderlyDiagnostics.InvalidBlobKeyPrefix, location,
                    prefix, entity.Name, property.Name,
                    $"uses the reserved '{BlobKeyConventionsMirror.StagingSegment}' segment, which marks not-yet-promoted uploads. A prefix containing it makes permanent blobs read as staged."));
                return;
            }

            List<string> departures = new();

            if (prefix.Any(char.IsUpper))
                departures.Add("not lowercase");

            if (prefix.Contains('_'))
                departures.Add("using underscores rather than dashes");

            if (departures.Count > 0)
            {
                diagnostics.Add(Diagnostic.Create(
                    SpiderlyDiagnostics.UnconventionalBlobKeyPrefix, location,
                    prefix, entity.Name, property.Name, string.Join(" and ", departures)));
            }
        }

        private static void ReportCollisions(
            List<(string Prefix, SpiderlyClass Entity, SpiderlyProperty Property)> effectivePrefixes,
            List<Diagnostic> diagnostics)
        {
            for (int i = 0; i < effectivePrefixes.Count; i++)
            {
                (string first, SpiderlyClass firstEntity, SpiderlyProperty firstProperty) = effectivePrefixes[i];

                for (int j = i + 1; j < effectivePrefixes.Count; j++)
                {
                    (string second, SpiderlyClass secondEntity, SpiderlyProperty secondProperty) = effectivePrefixes[j];

                    if (first == second || first.StartsWith($"{second}/") || second.StartsWith($"{first}/"))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            SpiderlyDiagnostics.InvalidBlobKeyPrefix,
                            secondProperty.Location ?? secondEntity.Location,
                            second, secondEntity.Name, secondProperty.Name,
                            $"collides with prefix '{first}' of '{firstEntity.Name}.{firstProperty.Name}'. Prefixes are the cleanup/staging listing scope, so each must be unique and none may be a path-parent of another."));

                        return; // one collision per build is enough to act on; more would be noise
                    }
                }
            }
        }

        /// <summary>
        /// The source generator is a dependency-free netstandard2.0 analyzer and cannot reference
        /// <c>Spiderly.Shared</c>, so the one constant it needs from
        /// <c>BlobKeyConventions</c> is mirrored here. Keep the two in step.
        /// </summary>
        private static class BlobKeyConventionsMirror
        {
            public const string StagingSegment = "_tmp";
        }
    }
}
