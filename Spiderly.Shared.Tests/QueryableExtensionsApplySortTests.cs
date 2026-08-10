using Spiderly.Shared.Extensions;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins <c>ApplySort</c>'s documented fallback: called with <c>isFirst: false</c> on an UNORDERED
    /// query it must apply <c>OrderBy</c>, never <c>ThenBy</c>. The subtlety is WHERE "ordered" can be
    /// decided: <c>Queryable.ThenBy*</c> validates the EXPRESSION TREE's static type, while the runtime
    /// object lies — EF's <c>EntityQueryable</c> and LINQ-to-Objects' <c>EnumerableQuery</c> implement
    /// <c>IOrderedQueryable&lt;T&gt;</c> whether ordered or not. A runtime <c>is</c> check therefore
    /// waves an unordered query into <c>ThenBy*</c>, which throws
    /// <c>ArgumentException: Expression of type 'IQueryable`1' cannot be used for parameter of type
    /// 'IOrderedQueryable`1'</c>. Prod case: Sentry BACKEND-RS-1F (2026-08-10) — generated Build's Id
    /// tie-breaker ran with <c>isFirst: false</c> after a client sort field matched no case.
    /// </summary>
    public class QueryableExtensionsApplySortTests
    {
        private sealed class Row
        {
            public int Id { get; set; }
            public int Rank { get; set; }
        }

        /// <summary>
        /// <c>.Where</c> is load-bearing: it gives the expression tree the static type
        /// <c>IQueryable&lt;Row&gt;</c> (the generated Build always applies its predicate first), while
        /// the runtime object still claims <c>IOrderedQueryable&lt;Row&gt;</c> — the exact prod shape.
        /// </summary>
        private static IQueryable<Row> UnorderedQuery() => new[]
        {
            new Row { Id = 1, Rank = 2 },
            new Row { Id = 2, Rank = 1 },
            new Row { Id = 3, Rank = 3 },
        }.AsQueryable().Where(x => true);

        [Fact]
        public void UnorderedQuery_WithIsFirstFalse_FallsBackToOrderByDescending()
        {
            IQueryable<Row> sorted = UnorderedQuery().ApplySort(x => x.Id, ascending: false, isFirst: false);

            Assert.Equal(new[] { 3, 2, 1 }, sorted.Select(x => x.Id).ToArray());
        }

        [Fact]
        public void UnorderedQuery_WithIsFirstFalse_FallsBackToOrderByAscending()
        {
            IQueryable<Row> sorted = UnorderedQuery().ApplySort(x => x.Rank, ascending: true, isFirst: false);

            Assert.Equal(new[] { 2, 1, 3 }, sorted.Select(x => x.Id).ToArray());
        }

        [Fact]
        public void OrderedQuery_WithIsFirstFalse_ComposesThenBy()
        {
            IQueryable<Row> query = new[]
            {
                new Row { Id = 1, Rank = 2 },
                new Row { Id = 2, Rank = 1 },
                new Row { Id = 3, Rank = 2 },
            }.AsQueryable().Where(x => true).OrderBy(x => x.Rank);

            IQueryable<Row> sorted = query.ApplySort(x => x.Id, ascending: false, isFirst: false);

            // Rank ascending first (2 alone on rank 1), then Id descending inside rank 2 (3 before 1) —
            // proves the ThenBy path composed instead of the fallback re-ordering the whole sequence.
            Assert.Equal(new[] { 2, 3, 1 }, sorted.Select(x => x.Id).ToArray());
        }

        [Fact]
        public void IsFirstTrue_Orders()
        {
            IQueryable<Row> sorted = UnorderedQuery().ApplySort(x => x.Rank, ascending: true, isFirst: true);

            Assert.Equal(new[] { 1, 2, 3 }, sorted.Select(x => x.Rank).ToArray());
        }
    }
}
