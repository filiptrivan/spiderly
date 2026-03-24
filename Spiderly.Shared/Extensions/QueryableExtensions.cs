using System.Linq;
using System.Linq.Expressions;

namespace Spiderly.Shared.Extensions
{
    public static class QueryableExtensions
    {
        /// <summary>
        /// Applies OrderBy/ThenBy (ascending or descending) to a query based on whether this is the first sort expression.
        /// Used by the generated <c>PaginatedResultGenerator</c> to apply <c>MultiSortMeta</c> from the frontend.
        /// <example>
        /// <code>
        /// for (int i = 0; i &lt; filterDTO.MultiSortMeta.Count; i++)
        /// {
        ///     bool ascending = filterDTO.MultiSortMeta[i].Order == 1;
        ///     query = query.ApplySort(x => x.Name, ascending, i == 0);
        /// }
        /// </code>
        /// </example>
        /// </summary>
        public static IQueryable<T> ApplySort<T, TKey>(
            this IQueryable<T> query,
            Expression<Func<T, TKey>> keySelector,
            bool ascending,
            bool isFirst)
        {
            if (isFirst)
                return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);

            if (query is IOrderedQueryable<T> orderedQuery)
                return ascending ? orderedQuery.ThenBy(keySelector) : orderedQuery.ThenByDescending(keySelector);

            // Defensive fallback: if called with isFirst=false on an unordered query, treat as first sort
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }
    }
}
