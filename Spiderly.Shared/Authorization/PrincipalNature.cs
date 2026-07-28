namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Whether a principal kind represents a person or a machine. Declared per kind at registration
    /// (<c>AddSpiderlyPrincipal&lt;TPrincipal&gt;(kind, nature)</c>) because only the application knows: a
    /// custom <c>Employee</c> kind is human, a custom <c>ServiceAccount</c> is not, and the framework cannot
    /// guess from the name.
    /// </summary>
    /// <remarks>
    /// The distinction exists because "who is calling" and "whose data is this" are different questions that a
    /// single principal id cannot answer. Authorization, auditing and rate limiting want the caller whatever it
    /// is; an identity-scoped read ("my orders") wants a person, and must refuse rather than resolve a machine's
    /// id against the user table.
    /// </remarks>
    public enum PrincipalNature
    {
        /// <summary>A person, authenticated as themselves. Their id is a real row in the application's user table.</summary>
        Human = 0,

        /// <summary>
        /// A non-person caller — an API key, a service account. It carries its own permissions and its own id,
        /// which belongs to <b>its own</b> table and is meaningless as a user id.
        /// </summary>
        Machine = 1,
    }
}
