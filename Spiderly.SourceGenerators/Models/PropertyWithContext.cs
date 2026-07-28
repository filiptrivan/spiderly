namespace Spiderly.SourceGenerators.Models
{
    public class PropertyWithContext
    {
        /// <summary>
        /// The Angular form-control name, or <c>null</c> for entries that carry none — ordered-one-to-many
        /// and complex-many-to-many-list panels, and table-typed controls. Consumers filter on
        /// <c>FormControlName != null</c> to skip them.
        /// </summary>
        public string? FormControlName { get; set; }
        public SpiderlyClass Entity { get; set; } = null!;
        public SpiderlyProperty Property { get; set; } = null!;
    }
}
