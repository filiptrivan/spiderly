using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Net;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Model-wide guard behind <c>SPIDERLY030</c>: every storage property's <b>effective</b> key
    /// prefix (see <see cref="Extensions.GetEffectiveKeyPrefix"/>) must be unique, no prefix may
    /// be a path-parent of another (both are listing scopes for cleanup/staging promotion), and
    /// custom prefixes must be key-safe (they land verbatim in public URLs).
    /// <para>
    /// The which-path-does-a-custom-prefix-bind-to rule is NOT re-derived here: it is
    /// <see cref="Extensions.IsEditorImageProperty"/>, shared with
    /// <see cref="ServiceSaveGenerator.GetBlobKeyPrefixExpression"/>. A second spelling of it made
    /// the validator pass while the generator emitted a different prefix — a guard that stops
    /// guarding without failing, which is worse than no guard.
    /// </para>
    /// </summary>
    public static class BlobKeyPrefixValidator
    {
        /// <summary>
        /// Lowercase ASCII kebab-case segments separated by '/'. Anything else either
        /// percent-encodes in URLs (uppercase is legal but is banned for consistency with the
        /// slugified file segment) or collides with the reserved <c>_tmp</c> staging segment.
        /// <para>
        /// Deliberately NOT <c>RegexOptions.Compiled</c>: measured, compiling costs ~10,9 ms of
        /// Reflection.Emit at first use against ~0,3 ms interpreted, and saves ~3,6 µs per
        /// validation run — break-even is thousands of generation passes in one Roslyn session,
        /// so every build would pay the emit and never recover it.
        /// </para>
        /// </summary>
        private static readonly Regex KeySafePrefix = new("^[a-z0-9]+(-[a-z0-9]+)*(/[a-z0-9]+(-[a-z0-9]+)*)*$");

        public static void Validate(List<SpiderlyClass> entities)
        {
            List<(string Prefix, SpiderlyClass Entity, SpiderlyProperty Property)> effectivePrefixes = new();

            foreach (SpiderlyClass entity in entities)
            {
                foreach (SpiderlyProperty property in Helpers.GetBlobProperties(entity.Properties))
                {
                    string? customPrefix = property.GetBlobKeyPrefix();

                    if (customPrefix != null && !KeySafePrefix.IsMatch(customPrefix))
                    {
                        throw SpiderlyDiagnostics.Create(
                            SpiderlyDiagnostics.InvalidBlobKeyPrefix,
                            property.Location ?? entity.Location,
                            customPrefix, entity.Name, property.Name,
                            "not key-safe. Use lowercase ASCII kebab-case segments separated by '/', e.g. \"products\" or \"products/thumbs\".");
                    }

                    effectivePrefixes.Add((property.GetEffectiveKeyPrefix(entity.Name), entity, property));

                    if (property.IsEditorImageProperty())
                    {
                        effectivePrefixes.Add((
                            property.GetEffectiveKeyPrefix(entity.Name, isEditorImagePath: true),
                            entity, property));
                    }
                }
            }

            for (int i = 0; i < effectivePrefixes.Count; i++)
            {
                (string first, SpiderlyClass firstEntity, SpiderlyProperty firstProperty) = effectivePrefixes[i];

                for (int j = i + 1; j < effectivePrefixes.Count; j++)
                {
                    (string second, SpiderlyClass secondEntity, SpiderlyProperty secondProperty) = effectivePrefixes[j];

                    if (first == second || first.StartsWith($"{second}/") || second.StartsWith($"{first}/"))
                    {
                        throw SpiderlyDiagnostics.Create(
                            SpiderlyDiagnostics.InvalidBlobKeyPrefix,
                            secondProperty.Location ?? secondEntity.Location,
                            second, secondEntity.Name, secondProperty.Name,
                            $"collides with prefix '{first}' of '{firstEntity.Name}.{firstProperty.Name}'. Prefixes are the cleanup/staging listing scope, so each must be unique and none may be a path-parent of another.");
                    }
                }
            }
        }
    }
}
