using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// Makes an action's non-nullable <c>string</c> parameter required. Under <c>[ApiController]</c>, a
    /// missing or empty value then answers 400 before the action runs. Without it, the model state is
    /// invalid and the action runs. A <c>string?</c> parameter stays optional. So does a parameter with a
    /// default value, or one in a nullable-oblivious file.
    /// <para>
    /// <c>SpiderlyAddControllers</c> turns MVC's implicit <c>[Required]</c> off. Then a null body property
    /// reaches the generated FluentValidation rules and their 422. But the switch also covers action
    /// parameters, so a <c>string q</c> arrived as null for <c>?q=</c>. This provider puts the rule back
    /// for parameters only. Body properties are not parameters, so it leaves them alone. The case it
    /// stops: <c>NonNullableStringParameterRequiredTests</c>.
    /// </para>
    /// </summary>
    public sealed class RequiredStringParameterMetadataProvider : IValidationMetadataProvider
    {
        /// <summary>
        /// Adds <see cref="RequiredAttribute"/> to the metadata of each non-nullable <c>string</c>
        /// action parameter without a default value.
        /// </summary>
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            if (context.Key.MetadataKind != ModelMetadataKind.Parameter)
                return;

            ParameterInfo? parameter = context.Key.ParameterInfo;
            if (parameter == null || parameter.ParameterType != typeof(string) || parameter.HasDefaultValue)
                return;

            // NullabilityInfoContext caches per instance and is not thread-safe, so each call makes one.
            if (new NullabilityInfoContext().Create(parameter).WriteState != NullabilityState.NotNull)
                return;

            if (context.ValidationMetadata.ValidatorMetadata.OfType<RequiredAttribute>().Any())
                return;

            context.ValidationMetadata.IsRequired = true;
            context.ValidationMetadata.ValidatorMetadata.Add(new RequiredAttribute());
        }
    }
}
