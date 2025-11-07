namespace Spiderly.SourceGenerators.Models
{
    public class AngularFormBlock
    {
        public string FormControlName { get; set; }
        public SpiderlyClass Entity { get; set; }
        public SpiderlyProperty Property { get; set; }
    }
}
