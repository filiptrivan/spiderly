using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Net;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Model-wide guard behind <c>SPIDERLY030</c>: every storage property's <b>effective</b> key
    /// prefix — the custom <c>KeyPrefix</c> where declared, the <c>{Entity}/{Property}</c>
    /// (editor images: <c>{Entity}/{Property}Image</c>) default otherwise — must be unique, no
    /// prefix may be a path-parent of another (both are listing scopes for cleanup/staging
    /// promotion — see <see cref="ServiceSaveGenerator.GetKeyPrefixExpression"/>, which must stay
    /// in step with the effective-prefix computation here), and custom prefixes must be key-safe
    /// (they land verbatim in public URLs).
    /// </summary>
    public static class BlobKeyPrefixValidator
    {
        /// <summary>
        /// Lowercase ASCII kebab-case segments separated by '/'. Anything else either
        /// percent-encodes in URLs (uppercase is legal but is banned for consistency with the
        /// slugified file segment) or collides with the reserved <c>_tmp</c> staging segment.
        /// </summary>
        private static readonly Regex KeySafePrefix = new("^[a-z0-9]+(-[a-z0-9]+)*(/[a-z0-9]+(-[a-z0-9]+)*)*$", RegexOptions.Compiled);

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

                    bool isEditorProperty = property.IsEditorControlType() || property.IsMarkdownControlType();

                    effectivePrefixes.Add((
                        customPrefix != null && !isEditorProperty ? customPrefix : $"{entity.Name}/{property.Name}",
                        entity, property));

                    if (isEditorProperty && property.HasS3PublicStorageAttribute())
                    {
                        effectivePrefixes.Add((
                            customPrefix ?? $"{entity.Name}/{property.Name}Image",
                            entity, property));
                    }
                }
            }

            for (int i = 0; i < effectivePrefixes.Count; i++)
            {
                for (int j = i + 1; j < effectivePrefixes.Count; j++)
                {
                    (string first, SpiderlyClass firstEntity, SpiderlyProperty firstProperty) = effectivePrefixes[i];
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
