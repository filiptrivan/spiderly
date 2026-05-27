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

        /// <summary>
        /// ComplexManyToManyList default-seed forkJoin params for the route-load, e.g.
        /// <c>"defaultProductVariantWarehousesForProductVariant: this.apiService.getDefaultProductVariantWarehousesForProductVariant()"</c>
        /// (no indent, no trailing comma — the emitter lays them out). Covers the entity's own top-level
        /// [ComplexManyToManyList] props plus the [ComplexManyToManyList] props of its DIRECT [UIOrderedOneToMany]
        /// children. Empty when none — the route-load then stays byte-identical to the no-seed shell.
        /// </summary>
        public List<string> SeedForkJoinParams { get; set; } = new();

        /// <summary>
        /// New-entity initial values for the entity's OWN top-level [ComplexManyToManyList] props, e.g.
        /// <c>"productVariantWarehouses: data.defaultProductVariantWarehousesForProductVariant"</c>. Applied as the
        /// initFormGroup seed object on the create (modelId &lt;= 0) path so a new entity pre-populates its fixed junction rows.
        /// </summary>
        public List<string> NewEntitySeedInits { get; set; } = new();

        /// <summary>
        /// formGroupInitialValues assignment statements that seed default junction rows for an ordered-O2M child's
        /// [ComplexManyToManyList] props (so a newly-added child row pre-populates them), e.g.
        /// <c>"this.parentFormGroup.controls.orderedProductVariantsSaveBodyDTO.formGroupInitialValues = { ...this.parentFormGroup.controls.orderedProductVariantsSaveBodyDTO.formGroupInitialValues, productVariantWarehouses: data.defaultProductVariantWarehousesForProductVariant };"</c>.
        /// Applied (indented by the emitter) in both the existing and new route-load paths. Empty when none.
        /// </summary>
        public List<string> OrderedChildSeedAssignments { get; set; } = new();

        /// <summary>
        /// Ordered-O2M File-upload output names the shell re-declares and forwards from the embedded fragment, e.g.
        /// <c>"onUrlForProductMediaUploaded"</c>. One per File control on a DIRECT [UIOrderedOneToMany] child — matching
        /// the {Entity}Fields fragment's re-exposed on{Prop}For{ChildEntity}Uploaded outputs. Each is declared as
        /// <c>@Output() {name} = new EventEmitter&lt;{ event: SpiderlyFileSelectEvent; formGroup: SpiderlyFormGroup }&gt;()</c>
        /// and wired on the embedded fragment as <c>({name})="{name}.emit($event)"</c>. Empty when none — the shell
        /// then stays byte-identical to the no-forward shell.
        /// </summary>
        public List<string> ForwardedFileOutputs { get; set; } = new();
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
