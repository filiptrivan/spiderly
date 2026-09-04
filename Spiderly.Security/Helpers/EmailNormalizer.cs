namespace Spiderly.Security.Helpers
{
    /// <summary>
    /// The canonical form of an email address used as an ACCOUNT KEY.
    /// </summary>
    /// <remarks>
    /// Address identity is case-insensitive as people use it: nobody who typed
    /// <c>Kupac@Example.com</c> at signup believes they own something different from
    /// <c>kupac@example.com</c>. Databases disagree — <c>==</c> on a Postgres <c>text</c> column, or
    /// on SQL Server under a case-sensitive collation, is ordinal — so every lookup and every write
    /// on the login path folds through here. Without it a second casing does not fail to match, it
    /// silently CREATES a second account, splitting one person's data across two rows.
    /// <para>
    /// Applied at the auth boundary (<see cref="Services.SecurityServiceBase{TUser, TUserExternalLogin}"/>'s
    /// public entry points) rather than inside the token layer, so the database lookup and the
    /// verification-code store are handed the same string by construction. Normalizing only one of
    /// them is worse than normalizing neither: the code is stored under one address and validated
    /// against the other, both compared ordinally, and the login stops completing at all.
    /// </para>
    /// <para>
    /// <b>The framework canonicalizes only what flows through <c>SecurityServiceBase</c>.</b> A
    /// consumer that inserts a <c>TUser</c> row by any other path — a guest checkout that provisions
    /// an account, an admin CRUD save — owns that write and must fold through here too, or it
    /// reintroduces exactly the split this prevents.
    /// </para>
    /// <para>
    /// Normalization stops new splits; it cannot make them unrepresentable. That needs a
    /// case-insensitive unique key on the consumer's own user table (a functional
    /// <c>lower(Email)</c> index, or <c>citext</c>), which is provider-specific SQL a portable EF
    /// <c>HasIndex</c> cannot express — so it stays the consumer's to add, after folding whatever
    /// rows predate this.
    /// </para>
    /// <para>
    /// <see cref="string.ToLowerInvariant"/>, never <c>ToLower()</c> — under a Turkish culture the
    /// latter maps <c>I</c> to <c>ı</c>, so the same address would canonicalize differently
    /// depending on the server's locale.
    /// </para>
    /// <para>
    /// Only the case is folded. The local part of an address is case-SENSITIVE per RFC 5321 and
    /// providers merely choose not to exercise that, so this is a deliberate policy choice — the
    /// same one every mainstream identity system makes — not a reading of the spec. Provider-specific
    /// rules (Gmail's dots, plus-addressing) are NOT applied: <c>a.b@gmail.com</c> and
    /// <c>ab@gmail.com</c> stay distinct accounts, because folding them would be wrong for every
    /// domain that treats them as distinct mailboxes.
    /// </para>
    /// </remarks>
    public static class EmailNormalizer
    {
        /// <summary>Trims surrounding whitespace and lower-cases invariantly.</summary>
        public static string Normalize(string email) => email.Trim().ToLowerInvariant();
    }
}
