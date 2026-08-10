using Spiderly.Shared.Contracts;

namespace Spiderly.Shared.Exceptions
{
    /// <summary>
    /// Factories for the 400s the generated pagination pipeline (the emitted <c>Build</c> methods)
    /// throws on invalid client input. A static seam rather than inline emitted string literals so the
    /// wording and the <see cref="ApiErrorCodes"/> pairing live in ONE place instead of being duplicated
    /// into every entity's generated <c>Build</c> (hundreds of throw sites in a mid-size consumer), and
    /// so generated code — which has no DI — still produces machine-coded errors.
    /// </summary>
    public static class PaginationErrors
    {
        /// <summary>
        /// The pagination request filtered on <paramref name="field"/>, which has no generated filter
        /// case. <paramref name="filterableFields"/> is the pre-joined valid list, baked into the
        /// generated call site at generation time.
        /// </summary>
        public static BusinessException UnknownFilterField(string field, string filterableFields) =>
            new($"Unknown filter field '{field}'. Filterable fields: {filterableFields}.", ApiErrorCodes.UnknownFilterField);

        /// <summary>
        /// The pagination request sorted on <paramref name="field"/>, which has no generated sort case.
        /// <paramref name="sortableFields"/> is the pre-joined valid list, baked into the generated call
        /// site at generation time.
        /// </summary>
        public static BusinessException UnknownSortField(string field, string sortableFields) =>
            new($"Unknown sort field '{field}'. Sortable fields: {sortableFields}.", ApiErrorCodes.UnknownSortField);

        /// <summary>
        /// A filter rule on <paramref name="field"/> used <paramref name="matchMode"/>, which is not
        /// valid for the field's type.
        /// </summary>
        public static BusinessException InvalidMatchMode(string matchMode, string field) =>
            new($"Invalid match mode '{matchMode}' for filter field '{field}'.", ApiErrorCodes.InvalidMatchMode);
    }
}
