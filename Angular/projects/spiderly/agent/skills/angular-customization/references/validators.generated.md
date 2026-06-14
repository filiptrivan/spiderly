<!-- GENERATED FROM framework-metadata.json — DO NOT EDIT.
     Regenerate: `dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json && node tools/extract-ts-metadata.mjs && node tools/gen-skill-docs.mjs` -->

# Built-in validators

Built-in validators on `ValidatorAbstractService` (call from your `setValidator` / `setFormArrayValidator` override).

| Validator | Description |
| --- | --- |
| `isArrayEmpty(control: SpiderlyFormControl): SpiderlyValidatorFn` | Validates that a SpiderlyFormControl holding an array value (e.g., multi-select dropdown) is not empty. |
| `isFormArrayEmpty(control: SpiderlyFormArray): void` | Validates that a SpiderlyFormArray (collection of form controls/groups) is not empty. |
| `notEmpty(control: SpiderlyFormControl): void` |  |
| `validateImageDimensions(file: File, imageWidth: number, imageHeight: number): Promise<ImageDimensionsValidationResult>` |  |
