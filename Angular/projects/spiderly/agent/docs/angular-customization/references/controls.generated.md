<!-- GENERATED FROM framework-metadata.json — DO NOT EDIT.
     Regenerate: `dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json && node tools/extract-ts-metadata.mjs && node tools/gen-skill-docs.mjs` -->

# Form control components

Every control also accepts the shared `BaseControl` inputs: `control`, `controlValid`, `disabled`, `label`, `placeholder`, `showLabel`, `showRequired`, `showTooltip`, `tooltipIcon`, `tooltipText`.

| Selector | Component | Control-specific inputs |
| --- | --- | --- |
| `spiderly-autocomplete` | `SpiderlyAutocompleteComponent` | `appendTo`, `showClear`, `emptyMessage`, `displayName` |
| `spiderly-calendar` | `SpiderlyCalendarComponent` | `showTime`, `dateOnly`, `timeOnly` |
| `spiderly-checkbox` | `SpiderlyCheckboxComponent` | `fakeLabel`, `initializeToFalse`, `inlineLabel` |
| `spiderly-colorpicker` | `SpiderlyColorPickerComponent` | `showInputTextField` |
| `spiderly-dropdown` | `SpiderlyDropdownComponent` | `isBooleanPicker` |
| `spiderly-editor` | `SpiderlyEditorComponent` | `uploadImageMethod`, `objectId`, `acceptedFileTypes` |
| `spiderly-file` | `SpiderlyFileComponent` | `objectId`, `fileData`, `acceptedFileTypes`, `required`, `multiple`, `isUrlFileData`, `imageWidth`, `imageHeight`, `maxFileSize`, `files` |
| `spiderly-markdown` | `SpiderlyMarkdownComponent` | `uploadImageMethod`, `objectId` |
| `spiderly-multiautocomplete` | `SpiderlyMultiAutocompleteComponent` | — |
| `spiderly-multiselect` | `SpiderlyMultiSelectComponent` | — |
| `spiderly-number` | `SpiderlyNumberComponent` | `prefix`, `showButtons`, `decimal`, `maxFractionDigits` |
| `spiderly-password` | `SpiderlyPasswordComponent` | `showPasswordStrength` |
| `spiderly-textarea` | `SpiderlyTextareaComponent` | — |
| `spiderly-textbox` | `SpiderlyTextboxComponent` | `showButton`, `buttonIcon` |
