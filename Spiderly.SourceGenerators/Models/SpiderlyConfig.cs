using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyConfig : IEquatable<SpiderlyConfig>
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public Dictionary<string, bool> Generators { get; set; } = new Dictionary<string, bool>();
        public SpiderlyApiConfig Api { get; set; } = new SpiderlyApiConfig();

        public bool IsGeneratorEnabled(string generatorName)
        {
            if (Generators.TryGetValue(generatorName, out bool enabled))
                return enabled;

            return true;
        }

        public static SpiderlyConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new SpiderlyConfig();

            try
            {
                SpiderlyConfig? config = JsonSerializer.Deserialize<SpiderlyConfig>(json, _jsonOptions);
                return config ?? new SpiderlyConfig();
            }
            catch
            {
                return new SpiderlyConfig();
            }
        }

        public bool Equals(SpiderlyConfig? other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (!Api.Equals(other.Api))
                return false;

            if (Generators.Count != other.Generators.Count)
                return false;

            foreach (KeyValuePair<string, bool> kvp in Generators)
            {
                if (!other.Generators.TryGetValue(kvp.Key, out bool otherValue) || kvp.Value != otherValue)
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as SpiderlyConfig);

        public override int GetHashCode()
        {
            int hash = Api.GetHashCode();

            foreach (KeyValuePair<string, bool> kvp in Generators.OrderBy(x => x.Key))
            {
                hash = hash * 31 + kvp.Key.GetHashCode();
                hash = hash * 31 + kvp.Value.GetHashCode();
            }

            return hash;
        }
    }

    public class SpiderlyApiConfig : IEquatable<SpiderlyApiConfig>
    {
        public string RoutePrefix { get; set; } = "/api";

        public bool Equals(SpiderlyApiConfig? other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return RoutePrefix == other.RoutePrefix;
        }

        public override bool Equals(object? obj) => Equals(obj as SpiderlyApiConfig);

        public override int GetHashCode() => RoutePrefix?.GetHashCode() ?? 0;
    }
}
