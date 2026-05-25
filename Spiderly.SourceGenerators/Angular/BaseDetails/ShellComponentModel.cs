namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Emission model for the <c>{Entity}BaseDetails</c> shell — the panel + Save + auth + route/load lifecycle that
    /// wraps the <c>{Entity}Fields</c> fragment. Keeps today's public selector/class name so consumers don't move.
    /// </summary>
    internal sealed class ShellComponentModel
    {
        public string EntityName { get; set; }
        public string Selector { get; set; }
        public string ComponentClassName { get; set; }
        public string FieldsComponentClassName { get; set; }
        public string FieldsSelector { get; set; }
        public string SaveBodyTypeName { get; set; }
        public string MainUIFormTypeName { get; set; }
        public string ConfigClassName { get; set; }

        /// <summary>Initial <c>isAuthorizedForSave</c> / additional-auth default — true only when the entity is <c>[DoNotAuthorize]</c>.</summary>
        public bool DefaultAuthorized { get; set; }
    }
}
