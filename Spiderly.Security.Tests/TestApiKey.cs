using System.ComponentModel.DataAnnotations.Schema;
using Spiderly.Security.Interfaces;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Minimal <see cref="IApiKey"/> entity for the API-key tests: it satisfies the generic
    /// <c>AddSpiderlyApiKeyAuthentication&lt;TApiKey&gt;</c> constraint and maps to a table where a test stores keys.
    /// </summary>
    internal sealed class TestApiKey : IApiKey
    {
        public long Id { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string KeyHash { get; set; } = null!;
        public bool? IsRevoked { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool? IsDisabled { get; set; }

        [NotMapped]
        public IReadOnlyCollection<IRole> Roles => Array.Empty<IRole>();
    }
}
