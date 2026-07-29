using System.Reflection;
using Spiderly.Shared.Extensions;

// Deliberately declared in the GLOBAL namespace — Type.Namespace is null for such a type, which is the
// shape under test. Nothing else in the suite can provide one.
public class GlobalNamespaceReferenceType { }

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// <c>IsManyToOneType</c> runs over every property of an entity and read <c>Type.Namespace</c> directly
    /// to exclude BCL types. <c>Type.Namespace</c> is null for a type declared in the global namespace, so a
    /// single such property type NRE'd the whole classification.
    /// </summary>
    public class IsManyToOneTypeTests
    {
        [Fact]
        public void GlobalNamespaceReferenceProperty_IsTreatedAsAManyToOneType()
        {
            // A global-namespace class is not a System type, so it classifies exactly like any other.
            Assert.True(PropertyOf(nameof(Holder.GlobalNav)).IsManyToOneType());
        }

        [Fact]
        public void SystemTypesAndScalars_AreStillExcluded()
        {
            Assert.False(PropertyOf(nameof(Holder.Text)).IsManyToOneType());
            Assert.False(PropertyOf(nameof(Holder.Number)).IsManyToOneType());
            Assert.False(PropertyOf(nameof(Holder.Items)).IsManyToOneType());
        }

        [Fact]
        public void NamespacedReferenceProperty_IsStillAManyToOneType()
        {
            Assert.True(PropertyOf(nameof(Holder.Nav)).IsManyToOneType());
        }

        private static PropertyInfo PropertyOf(string name) => typeof(Holder).GetProperty(name)!;

        private sealed class Holder
        {
            public GlobalNamespaceReferenceType GlobalNav { get; set; } = null!;
            public Nested Nav { get; set; } = null!;
            public string Text { get; set; } = null!;
            public int Number { get; set; }
            public List<Nested> Items { get; set; } = null!;
        }

        public sealed class Nested { }
    }
}
