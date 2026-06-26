<!-- GENERATED FROM framework-metadata.json — DO NOT EDIT.
     Regenerate: `dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json && node tools/extract-ts-metadata.mjs && node tools/gen-skill-docs.mjs` -->

# Shared helper functions

Reusable helpers exported from `helper-functions.ts`. Import the one you need instead of re-implementing it.

| Signature | Description |
| --- | --- |
| `ReflectProp(target: any, propertyKey: string)` |  |
| `adjustColor(color: string, percent: number): string` |  |
| `capitalizeFirstChar(str: string): string` |  |
| `deleteAction(cols: Column[], actionField: string): void` |  |
| `exportListToExcel(exportListToExcelObservableMethod: (filter: Filter) => Observable<any>, filter: Filter)` |  |
| `firstCharToUpper(input: string): string` |  |
| `getFileNameFromContentDisposition(resp: HttpResponse<Blob>, defaultName: string): string` |  |
| `getHtmlImgDisplayString64(base64String: string)` |  |
| `getImageDimensions(file: File): Promise<{ width: number; height: number }>` |  |
| `getMimeTypeForFileName(fileName: string): string` |  |
| `getMonth(numberOfTheMonth: number): string` |  |
| `getParentUrl(currentUrl: string)` |  |
| `getPrimengAutocompleteCodebookOptions(getAutocompleteListObservable: ( limit: number, query: string, ) => Observable<Codebook[]>, limit: number, query: string): Observable<PrimengOption[]>` |  |
| `getPrimengAutocompleteNamebookOptions(getAutocompleteListObservable: ( limit: number, query: string, parentEntityId?: number, ) => Observable<Namebook[]>, limit: number, query: string, parentEntityId?: number): Observable<PrimengOption[]>` |  |
| `getPrimengDropdownCodebookOptions(getDropdownListObservable: () => Observable<Codebook[]>): Observable<PrimengOption[]>` |  |
| `getPrimengDropdownNamebookOptions(getDropdownListObservable: ( parentEntityId?: number, ) => Observable<Namebook[]>, parentEntityId?: number): Observable<PrimengOption[]>` |  |
| `getPrimengNamebookOptions(namebookList: Namebook[]): PrimengOption[]` |  |
| `isExcelFileType(mimeType: string): boolean` |  |
| `isFileImageType(mimeType: string): boolean` |  |
| `isNullOrEmpty(input: string)` |  |
| `kebabToTitleCase(input: string): string` |  |
| `nameOf<TObject extends { name: S }, S extends string>(funcOrClass: TObject): S` |  |
| `nameof(key1: any, key2?: any): any` |  |
| `parseDateOnlyLocal(s: string): Date \| null` |  |
| `pushAction(cols: Column[], action: Action)` |  |
| `saveResponseAsFile(res: HttpResponse<Blob>, fallbackName: string): void` |  |
| `selectedTab(tabs: SpiderlyTab[]): number` |  |
| `singleOrDefault<T>(array: T[], predicate: (item: T) => boolean): T \| undefined` |  |
| `splitPascalCase(input: string)` |  |
| `toCommaSeparatedString<T>(input: T[]): string` |  |
| `validatePrecisionScale(value: any, precision: number, scale: number, ignoreTrailingZeros: boolean): boolean` |  |
