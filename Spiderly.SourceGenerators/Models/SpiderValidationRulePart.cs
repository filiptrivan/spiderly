namespace Spiderly.SourceGenerators.Models
{
    public abstract class SpiderValidationRulePart { }

    /// <summary>Parameterless validation rule parts.</summary>
    public sealed class NotEmptyRulePart : SpiderValidationRulePart { }
    public sealed class EmailAddressRulePart : SpiderValidationRulePart { }
    public sealed class NotHaveWhiteSpaceRulePart : SpiderValidationRulePart { }

    /// <summary>String length validation rule part.</summary>
    public sealed class MaximumLengthRulePart(int maxLength) : SpiderValidationRulePart
    {
        public int MaxLength { get; } = maxLength;
    }

    public sealed class LengthRangePart(int min, int max) : SpiderValidationRulePart
    {
        public int Min { get; } = min;
        public int Max { get; } = max;
    }

    public sealed class ExactLengthRulePart(int length) : SpiderValidationRulePart
    {
        public int Length { get; } = length;
    }

    /// <summary>Numeric validation rule part.</summary>
    public sealed class GreaterThanOrEqualToRulePart(string value) : SpiderValidationRulePart
    {
        public string Value { get; } = value;
    }

    public sealed class LessThanOrEqualToRulePart(string value) : SpiderValidationRulePart
    {
        public string Value { get; } = value;
    }

    /// <summary>Precision/scale validation rule part.</summary>
    public sealed class PrecisionScaleRulePart(int precision, int scale) : SpiderValidationRulePart
    {
        public int Precision { get; } = precision;
        public int Scale { get; } = scale;
    }

    /// <summary>Conditional validation rule part.</summary>
    public sealed class UnlessRulePart(string condition) : SpiderValidationRulePart
    {
        public string Condition { get; } = condition;
    }
}
