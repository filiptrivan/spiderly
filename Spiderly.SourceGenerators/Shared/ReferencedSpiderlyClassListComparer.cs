using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;

namespace Spiderly.SourceGenerators.Shared
{
    internal sealed class ReferencedSpiderlyClassListComparer : IEqualityComparer<List<SpiderlyClass>>
    {
        public static readonly ReferencedSpiderlyClassListComparer Instance = new();

        private ReferencedSpiderlyClassListComparer()
        {
        }

        public bool Equals(List<SpiderlyClass> x, List<SpiderlyClass> y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null || x.Count != y.Count)
                return false;

            for (int i = 0; i < x.Count; i++)
            {
                if (!ClassEquals(x[i], y[i]))
                    return false;
            }

            return true;
        }

        public int GetHashCode(List<SpiderlyClass> obj)
        {
            if (obj is null)
                return 0;

            unchecked
            {
                int hash = 17;
                foreach (SpiderlyClass item in obj)
                    hash = hash * 31 + GetClassHashCode(item);

                return hash;
            }
        }

        private static bool ClassEquals(SpiderlyClass x, SpiderlyClass y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return x.Name == y.Name &&
                x.Namespace == y.Namespace &&
                x.BaseType == y.BaseType &&
                x.IsAbstract == y.IsAbstract &&
                x.ControllerName == y.ControllerName &&
                x.IsGenerated == y.IsGenerated &&
                x.Description == y.Description &&
                ListEquals(x.Properties, y.Properties, PropertyEquals) &&
                ListEquals(x.Attributes, y.Attributes, AttributeEquals) &&
                ListEquals(x.Methods, y.Methods, MethodEquals);
        }

        private static int GetClassHashCode(SpiderlyClass item)
        {
            if (item is null)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = Add(hash, item.Name);
                hash = Add(hash, item.Namespace);
                hash = Add(hash, item.BaseType);
                hash = Add(hash, item.IsAbstract);
                hash = Add(hash, item.ControllerName);
                hash = Add(hash, item.IsGenerated);
                hash = Add(hash, item.Description);
                hash = AddList(hash, item.Properties, GetPropertyHashCode);
                hash = AddList(hash, item.Attributes, GetAttributeHashCode);
                hash = AddList(hash, item.Methods, GetMethodHashCode);
                return hash;
            }
        }

        private static bool PropertyEquals(SpiderlyProperty x, SpiderlyProperty y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return EqualityComparer<SpiderlyTypeRef>.Default.Equals(x.Type, y.Type) &&
                x.Name == y.Name &&
                x.StringValue == y.StringValue &&
                x.EntityName == y.EntityName &&
                x.IsSaveBodyMainDTO == y.IsSaveBodyMainDTO &&
                x.Description == y.Description &&
                x.IsEnum == y.IsEnum &&
                x.IsOneToOnePrincipalInverseNav == y.IsOneToOnePrincipalInverseNav &&
                ListEquals(x.Attributes, y.Attributes, AttributeEquals);
        }

        private static int GetPropertyHashCode(SpiderlyProperty item)
        {
            if (item is null)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = Add(hash, item.Type);
                hash = Add(hash, item.Name);
                hash = Add(hash, item.StringValue);
                hash = Add(hash, item.EntityName);
                hash = Add(hash, item.IsSaveBodyMainDTO);
                hash = Add(hash, item.Description);
                hash = Add(hash, item.IsEnum);
                hash = Add(hash, item.IsOneToOnePrincipalInverseNav);
                hash = AddList(hash, item.Attributes, GetAttributeHashCode);
                return hash;
            }
        }

        private static bool MethodEquals(SpiderlyMethod x, SpiderlyMethod y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return x.Name == y.Name &&
                x.ReturnType == y.ReturnType &&
                x.Body == y.Body &&
                ListEquals(x.Parameters, y.Parameters, ParameterEquals) &&
                ListEquals(x.Attributes, y.Attributes, AttributeEquals);
        }

        private static int GetMethodHashCode(SpiderlyMethod item)
        {
            if (item is null)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = Add(hash, item.Name);
                hash = Add(hash, item.ReturnType);
                hash = Add(hash, item.Body);
                hash = AddList(hash, item.Parameters, GetParameterHashCode);
                hash = AddList(hash, item.Attributes, GetAttributeHashCode);
                return hash;
            }
        }

        private static bool ParameterEquals(SpiderParameter x, SpiderParameter y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return x.Name == y.Name &&
                EqualityComparer<SpiderlyTypeRef>.Default.Equals(x.Type, y.Type) &&
                ListEquals(x.Attributes, y.Attributes, AttributeEquals);
        }

        private static int GetParameterHashCode(SpiderParameter item)
        {
            if (item is null)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = Add(hash, item.Name);
                hash = Add(hash, item.Type);
                hash = AddList(hash, item.Attributes, GetAttributeHashCode);
                return hash;
            }
        }

        private static bool AttributeEquals(SpiderlyAttribute x, SpiderlyAttribute y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return x.Name == y.Name && x.Value == y.Value;
        }

        private static int GetAttributeHashCode(SpiderlyAttribute item)
        {
            if (item is null)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = Add(hash, item.Name);
                hash = Add(hash, item.Value);
                return hash;
            }
        }

        private static bool ListEquals<T>(IReadOnlyList<T>? x, IReadOnlyList<T>? y, System.Func<T, T, bool> equals)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null || x.Count != y.Count)
                return false;

            for (int i = 0; i < x.Count; i++)
            {
                if (!equals(x[i], y[i]))
                    return false;
            }

            return true;
        }

        private static int AddList<T>(int hash, IReadOnlyList<T>? items, System.Func<T, int> getHashCode)
        {
            unchecked
            {
                hash = Add(hash, items?.Count ?? 0);

                if (items is null)
                    return hash;

                foreach (T item in items)
                    hash = hash * 31 + getHashCode(item);

                return hash;
            }
        }

        private static int Add<T>(int hash, T value)
        {
            unchecked
            {
                return hash * 31 + EqualityComparer<T>.Default.GetHashCode(value);
            }
        }
    }
}
