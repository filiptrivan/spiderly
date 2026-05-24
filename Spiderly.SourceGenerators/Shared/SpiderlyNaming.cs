using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Single source of truth for the generated DTO name conventions. The suffix set lives in one
    /// place so that adding a new generated DTO variant is a one-line change here, and every
    /// consumer (e.g. entity resolution in <c>ExcelPropertiesGenerator</c>) picks it up automatically
    /// instead of carrying its own inline copy that can silently fall out of sync.
    /// <para>
    /// Forward-direction name building is still inlined as string interpolation across the
    /// generators (<c>$"{entity.Name}SaveBodyDTO"</c>); those don't fail silently, so migrating them
    /// here is opportunistic, not required.
    /// </para>
    /// </summary>
    public static class SpiderlyNaming
    {
        /// <summary>
        /// Suffixes appended to an entity name to form its generated read/write DTO names
        /// (<c>"Order"</c> → <c>"OrderDTO"</c>, <c>"OrderSaveBodyDTO"</c>, <c>"OrderMainUIFormDTO"</c>).
        /// </summary>
        public static readonly string[] DTOSuffixes =
        {
            "DTO",
            "SaveBodyDTO",
            "MainUIFormDTO",
        };

        /// <summary>
        /// True when <paramref name="DTOClassName"/> is one of the DTO names generated for
        /// <paramref name="entityName"/>. Matches the full generated name (rather than stripping a
        /// suffix off the DTO name) so an entity whose own name ends in "SaveBody"/"MainUIForm" can't
        /// be mis-resolved.
        /// </summary>
        public static bool IsGeneratedDTOName(string DTOClassName, string entityName)
            => DTOSuffixes.Any(suffix => DTOClassName == $"{entityName}{suffix}");
    }
}
