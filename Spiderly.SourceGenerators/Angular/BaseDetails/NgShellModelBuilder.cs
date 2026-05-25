using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>Builds a <see cref="ShellComponentModel"/> from a <see cref="SpiderlyClass"/>.</summary>
    internal static class NgShellModelBuilder
    {
        internal static ShellComponentModel Build(SpiderlyClass entity)
        {
            string kebab = entity.Name.FromPascalToKebabCase();

            return new ShellComponentModel
            {
                EntityName = entity.Name,
                Selector = $"{kebab}-base-details",
                ComponentClassName = $"{entity.Name}BaseDetailsComponent",
                FieldsComponentClassName = $"{entity.Name}FieldsComponent",
                FieldsSelector = $"{kebab}-fields",
                SaveBodyTypeName = $"{entity.Name}SaveBody",
                MainUIFormTypeName = $"{entity.Name}MainUIForm",
                ConfigClassName = $"{entity.Name}FieldsConfig",
                DefaultAuthorized = !Helpers.ShouldAuthorizeEntity(entity),
            };
        }
    }
}
