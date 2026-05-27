namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Default <see cref="IPrincipalRegistry"/> built from every <see cref="IPrincipalPermissionResolver"/>
    /// registered in the container. Registered as an empty-safe singleton: when no principal kinds are
    /// registered it simply resolves to nothing, so an application that never opts into the principal
    /// model still boots.
    /// </summary>
    public sealed class PrincipalRegistry : IPrincipalRegistry
    {
        private readonly Dictionary<string, IPrincipalPermissionResolver> _byKind =
            new(StringComparer.OrdinalIgnoreCase);

        public PrincipalRegistry(IEnumerable<IPrincipalPermissionResolver> resolvers)
        {
            foreach (IPrincipalPermissionResolver resolver in resolvers)
            {
                // Explicit check (rather than ToDictionary) so a double registration fails with an
                // actionable message instead of a cryptic "An item with the same key has already been added".
                // Kinds are matched case-insensitively, so "User" and "user" collide here too.
                if (_byKind.ContainsKey(resolver.Kind))
                    throw new InvalidOperationException(
                        $"Principal kind '{resolver.Kind}' is registered more than once. Each kind must be " +
                        $"registered exactly once via AddSpiderlyPrincipal<TPrincipal>(\"kind\") (kinds are case-insensitive).");

                _byKind[resolver.Kind] = resolver;
            }

            // A single registered kind is unambiguous, so requests need not carry the claim. With zero or
            // many kinds there is no safe default and the claim must identify the caller.
            DefaultKind = _byKind.Count == 1 ? _byKind.Keys.First() : null;
        }

        public bool IsEmpty => _byKind.Count == 0;

        public string DefaultKind { get; }

        public bool TryResolve(string kind, out IPrincipalPermissionResolver resolver)
        {
            string effectiveKind = string.IsNullOrEmpty(kind) ? DefaultKind : kind;

            if (effectiveKind != null && _byKind.TryGetValue(effectiveKind, out resolver))
                return true;

            resolver = null;
            return false;
        }
    }
}
