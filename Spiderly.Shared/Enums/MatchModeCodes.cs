namespace Spiderly.Shared.Enums
{
    /// <summary>
    /// String constants for the comparison operators a filter rule can use
    /// (carried on <c>FilterRuleDTO.MatchMode</c>). The values mirror PrimeNG's
    /// table filter match modes, so the same code drives both the Angular UI and
    /// the server-side query translation in the generated paginated-list logic.
    /// </summary>
    public static class MatchModeCodes
    {
        /// <summary>String prefix match, case-insensitive (<c>value.StartsWith(...)</c>).</summary>
        public const string StartsWith = "startsWith";

        /// <summary>String substring match, case-insensitive (<c>value.Contains(...)</c>).</summary>
        public const string Contains = "contains";

        /// <summary>
        /// Equality match. For strings it is case-insensitive; for bool, number, and
        /// date/time properties it is an exact <c>==</c> comparison.
        /// </summary>
        // 'new' suppresses CS0108: this constant intentionally shadows the inherited
        // object.Equals name. Harmless here since MatchModeCodes is a static class
        // (no instance, never called as a method) — the member is only read as a
        // compile-time string via MatchModeCodes.Equals.
        public new const string Equals = "equals";

        /// <summary>Less-than comparison (<c>&lt;</c>), for number and date/time properties.</summary>
        public const string LessThan = "lessThan";

        /// <summary>Greater-than comparison (<c>&gt;</c>), for number and date/time properties.</summary>
        public const string GreaterThan = "greaterThan";

        /// <summary>
        /// Membership match against a JSON array of values (<c>value IN [...]</c>),
        /// for number and id properties.
        /// </summary>
        public const string In = "in";
    }
}
