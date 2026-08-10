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
            // "Already ordered" is decided on the EXPRESSION TREE's static type — the layer Queryable.ThenBy*
            // validates. The runtime object cannot answer it: EF's EntityQueryable and LINQ's EnumerableQuery
            // implement IOrderedQueryable<T> whether ordered or not (Sentry BACKEND-RS-1F). The cast is safe
            // by construction — an ordered expression type only arises from an OrderBy/ThenBy call, whose own
            // return cast already proved the runtime type.
            if (!isFirst && typeof(IOrderedQueryable<T>).IsAssignableFrom(query.Expression.Type))
            {
                IOrderedQueryable<T> ordered = (IOrderedQueryable<T>)query;
                return ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
            }

            // First sort — or isFirst=false on a not-yet-ordered query (e.g. the generated Id tie-breaker
            // when the client sent no applicable sort), which starts the ordering instead of composing.
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }
    }
}
