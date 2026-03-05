using System.Linq.Expressions;

namespace Spiderly.Shared.DTO
{
    public class FilterDTO<TEntity> : FilterDTO
    {
        public FilterDTO<TEntity> AddFilter<TProperty>(
            Expression<Func<TEntity, TProperty>> property,
            TProperty value,
            string matchMode)
        {
            string propertyName = GetPropertyName(property);

            if (!Filters.ContainsKey(propertyName))
                Filters[propertyName] = new List<FilterRuleDTO>();

            Filters[propertyName].Add(new FilterRuleDTO
            {
                Value = value,
                MatchMode = matchMode
            });

            return this;
        }

        public FilterDTO<TEntity> AddSort<TProperty>(
            Expression<Func<TEntity, TProperty>> property,
            int order = 1)
        {
            string propertyName = GetPropertyName(property);

            MultiSortMeta.Add(new FilterSortMetaDTO
            {
                Field = propertyName,
                Order = order
            });

            return this;
        }

        public FilterDTO<TEntity> SetPagination(int first, int rows)
        {
            First = first;
            Rows = rows;
            return this;
        }

        private static string GetPropertyName<TProperty>(Expression<Func<TEntity, TProperty>> property)
        {
            string name = GetMemberName(property.Body);
            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        private static string GetMemberName(Expression expression)
        {
            return expression switch
            {
                MemberExpression member => member.Member.Name,
                UnaryExpression unary => GetMemberName(unary.Operand),
                _ => throw new ArgumentException("Invalid property expression")
            };
        }
    }
}
