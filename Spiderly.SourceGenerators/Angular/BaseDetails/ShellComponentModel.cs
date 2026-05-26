using System.Collections.Generic;

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

        /// <summary>
        /// Extra save-authorization permission codes from [UIAdditionalPermissionCodeForInsert/Update]. Each grants
        /// save when the current user holds the code (scoped to insert vs update), in addition to the default
        /// Insert{Entity}/Update{Entity} checks. Empty when the entity declares none.
        /// </summary>
        public List<AdditionalSavePermissionCode> AdditionalSavePermissionCodes { get; set; } = new();
    }

    /// <summary>One [UIAdditionalPermissionCodeFor*] save-authorization grant.</summary>
    internal sealed class AdditionalSavePermissionCode
    {
        /// <summary>The permission code the user must hold.</summary>
        public string PermissionCode { get; set; }
        /// <summary>True = for insert (modelId &lt;= 0); false = for update (modelId &gt; 0).</summary>
        public bool ForInsert { get; set; }
    }
}
