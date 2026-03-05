---
name: angular-customization
description: Customize Spiderly Angular admin panel — forms, data tables, service overrides, layout, validation, translations. Use when extending generated Angular components, overriding form save behavior, configuring data table columns, customizing layout or theme, adding custom validators, or working with translations.
---

# Angular Customization

## Generated File Structure

```
Frontend/src/app/business/
├── entities/entities.generated.ts         # TypeScript DTOs
├── services/api/api.service.generated.ts  # Typed API methods
├── components/base-details.generated.ts   # Entity form components
├── services/validators/validators.generated.ts
├── services/translates/translates.generated.ts
└── enums/enums.generated.ts
```

Never modify `.generated.ts` files — they regenerate on build.

## Form System

### Inheritance Chain

```
BaseFormComponent<TMainUIForm, TSaveBody>  (Spiderly library)
        ↓
{Entity}BaseDetailsComponent          (generated)
        ↓
{Entity}DetailsComponent              (your code)
```

### Save Flow (execution order)

```
1. onSave(rerouteToParentSlugAfterSave)
2.   → Build saveBody from form raw values
3.   → onBeforeSave(saveBody)        ← mutate saveBody here
4.   → baseFormService.isControlValid()
5.   → saveObservableMethod(saveBody)  (HTTP PUT)
6.   → onAfterSaveRequest()
7.   → Success toast + reroute
8.   → onAfterSave()
```

### Overridable Hooks

```typescript
export class ProductDetailsComponent
  extends BaseFormComponent<ProductMainUIForm, ProductSaveBody>
  implements OnInit
{
  override mainUIFormClass = ProductMainUIForm;
  override saveBodyClass = ProductSaveBody;

  override onBeforeSave = (saveBody?: ProductSaveBody) => {
    saveBody.productDTO.stock = saveBody.orderedProductVariantsSaveBodyDTO
      .reduce((sum, v) => sum + (v.productVariantDTO.stock ?? 0), 0);
  };

  override onAfterSave = () => {
    this.refreshRelatedData();
  };

  override rerouteToSavedObject = (rerouteId: number | string) => {
    this.router.navigateByUrl(`/custom-path/${rerouteId}`);
  };
}
```

### Key Form Classes

```typescript
SpiderlyFormControl<T> extends FormControl<T>
  label: string              // Untranslated name
  labelForDisplay: string    // Translated label
  required: boolean
  type: string               // 'number', 'Date', 'Namebook[]'
  validator: SpiderlyValidatorFn | null

SpiderlyFormGroup<T> extends FormGroup
  controls: SpiderlyControlsOfType<T>
  targetClass: SchemaAwareConstructor<T>
  getControl(formControlName): SpiderlyFormControl

SpiderlyFormArray<T> extends FormArray
  formGroupInitialValues: Partial<T>
  targetClass: SchemaAwareConstructor<T>
  getCrudMenuForOrderedData(): MenuItem[]  // Remove, AddAbove, AddBelow
  addNewFormGroup(index)
  getFormGroups(): SpiderlyFormGroup<T>[]
```

### DTO Mapping

- `MainUIFormDTO` — what the API returns (read)
- `SaveBodyDTO` — what you send to API (write)
- `baseFormService.mapMainUIFormToSaveBody()` handles conversion automatically
- Naming convention: `orderedItemsMainUIFormDTO` → `orderedItemsSaveBodyDTO`

## Data Table

### Lazy Load Mode (server-side pagination, default)

```html
<spiderly-data-table
  [cols]="cols"
  [getPaginatedListObservableMethod]="getPaginatedProductListMethod"
  [additionalFilterIdLong]="categoryId"
  [navigateOnRowClick]="true"
  [rowNavigationPath]="'/product-list'"
>
</spiderly-data-table>
```

### Client-Side Mode

```html
<spiderly-data-table
  [cols]="cols"
  [items]="localItems"
  [hasLazyLoad]="false"
>
</spiderly-data-table>
```

### Column Definition

```typescript
cols: Column<ProductDTO>[] = [
  new Column({ field: 'title', name: 'Title', filterType: 'text' }),
  new Column({ field: 'price', name: 'Price', filterType: 'numeric', showMatchModes: true, decimalPlaces: 2 }),
  new Column({ field: 'createdAt', name: 'Created', filterType: 'date', showTime: true }),
  new Column({ field: 'isActive', name: 'Active', filterType: 'boolean' }),
  new Column({ field: 'categoryDisplayName', name: 'Category', filterType: 'multiselect',
    dropdownOrMultiselectValues: this.categoryOptions }),
  new Column({
    actions: [
      new Action({ field: 'Details', icon: 'pi pi-pencil', onClick: (id) => this.editProduct(id) }),
      new Action({ field: 'Delete' }),
      new Action({ field: 'custom', name: 'Clone', icon: 'pi pi-copy', onClick: (id) => this.clone(id) }),
    ]
  }),
];
```

### Key Inputs

| Input | Type | Default | Purpose |
|---|---|---|---|
| `cols` | `Column[]` | — | Column definitions |
| `getPaginatedListObservableMethod` | `(filter) => Observable` | — | Server-side data source |
| `additionalFilterIdLong` | `number` | — | Parent entity filter |
| `hasLazyLoad` | `boolean` | `true` | Server vs client mode |
| `items` | `any[]` | — | Client-side data |
| `selectionMode` | `'single' \| 'multiple'` | — | Selection mode |
| `navigateOnRowClick` | `boolean` | `false` | Click row → details |
| `rowNavigationPath` | `string` | — | Base path for row click |
| `showAddButton` | `boolean` | `true` | Show "New" button |
| `showExportToExcelButton` | `boolean` | `true` | Show Excel export |
| `readonly` | `boolean` | `false` | Disable mutations |

### Key Outputs

| Output | Payload | Purpose |
|---|---|---|
| `onRowSelect` | `RowClickEvent` | Row selected |
| `onRowUnselect` | `RowClickEvent` | Row deselected |
| `onIsAllSelectedChange` | `AllClickEvent` | Select-all toggled |
| `onTotalRecordsChange` | `number` | Total records updated |

## Service Overrides

### ConfigServiceBase

```typescript
@Injectable({ providedIn: 'root' })
export class ConfigService extends ConfigServiceBase {
  override logoPath = 'assets/images/my-logo.png';
  override companyName = 'My Company';
  override primaryColor = '#3B82F6';
  override defaultPageSize = 25;
  override loginSlug = 'sign-in';
  override showGoogleAuth = true;
}
```

Key properties: `apiUrl`, `frontendUrl`, `GoogleClientId`, `companyName`, `primaryColor`, `logoPath`, `defaultPageSize`, `loginSlug`, `showGoogleAuth`.

### AuthServiceBase

Override hooks for custom post-auth behavior:

```typescript
export class AuthService extends AuthServiceBase {
  override onAfterLoginExternal = () => {
    this.analyticsService.trackLogin('google');
  };

  override onAfterLogout = () => {
    this.cacheService.clear();
  };

  override onAfterRefreshToken = () => {
    this.syncPermissions();
  };
}
```

Key observables: `user$` (current user), `currentUserPermissionCodes$` (permission codes).

### LayoutServiceBase

```typescript
export class LayoutService extends LayoutServiceBase {
  override initTopBarData(): Observable<InitTopBarData> {
    return this.apiService.getTopBarData().pipe(
      map(data => new InitTopBarData({ ... }))
    );
  }
}
```

#### Theme Configuration (AppConfig)

```typescript
layoutConfig: AppConfig = {
  inputStyle: 'outlined',     // 'outlined' | 'filled'
  colorScheme: 'light',       // 'light' | 'dark'
  menuMode: 'static',         // 'static' | 'overlay'
  scale: 14,                  // Font scale
  ripple: false,
  theme: 'lara-light-indigo',
  color: 'var(--p-primary-color)',
};
```

## Layout & Menu

### SpiderlyMenuItem

```typescript
interface SpiderlyMenuItem extends PrimeNG.MenuItem {
  hasPermission?: (permissionCodes: string[]) => boolean;
  showPartnerDialog?: boolean;
}
```

### Menu Setup

```typescript
// In layout.component.ts:
menu: SpiderlyMenuItem[] = [
  {
    label: this.translocoService.translate('Dashboard'),
    icon: 'pi pi-fw pi-home',
    routerLink: ['/dashboard'],
  },
  {
    label: this.translocoService.translate('Products'),
    icon: 'pi pi-fw pi-box',
    items: [
      { label: 'All Products', routerLink: ['/product-list'] },
      { label: 'Categories', routerLink: ['/category-list'] },
    ],
  },
];
```

### Layout Template

```html
<!-- Side menu (default) -->
<spiderly-layout [menu]="menu" [isSideMenuLayout]="true">
  <router-outlet></router-outlet>
</spiderly-layout>

<!-- Top menu -->
<spiderly-layout [menu]="menu" [isSideMenuLayout]="false">
  <router-outlet></router-outlet>
</spiderly-layout>
```

## Validation

### ValidatorAbstractService

Subclass to add custom validators per entity/field:

```typescript
@Injectable({ providedIn: 'root' })
export class MyValidatorService extends ValidatorAbstractService {
  setValidator(control: SpiderlyFormControl, className: string): SpiderlyValidatorFn {
    if (className === 'Product' && control.label === 'sku') {
      const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
        const value = control.value as string;
        if (value && !value.match(/^[A-Z0-9]{6,12}$/))
          return { _: this.translocoService.translate('InvalidSKU') };
        return null;
      };
      control.validator = validator;
    }
    return control.validator;
  }

  setFormArrayValidator(formArray: SpiderlyFormArray, className: string): void {
    if (className === 'OrderItems') {
      this.isFormArrayEmpty(formArray);
    }
  }
}
```

### Built-in Validators

| Method | Purpose |
|---|---|
| `notEmpty(control)` | Required field (sets `control.required = true`) |
| `isFormArrayEmpty(formArray)` | Array must have items |
| `isArrayEmpty(control)` | Multi-select must have selections |
| `validateImageDimensions(file, width, height)` | Async image dimension check (client-side) |

## Translations

### Adding Custom Keys

1. Add to `Frontend/src/assets/i18n/en.json`:
   ```json
   { "MyCustomKey": "Custom text" }
   ```

2. Use in TypeScript: `this.translocoService.translate('MyCustomKey')`

3. Use in template: `{{ t('MyCustomKey') }}` (inside `*transloco="let t"`)

### Label Auto-Translation

`BaseFormService.getTranslatedLabel()` converts camelCase property names:
- `productId` → looks up `Product` key
- `displayName` → empty string (stripped)
- `name` → looks up `Name` key

## UI Controls Reference

| Component | Selector | Key Inputs |
|---|---|---|
| TextBox | `spiderly-textbox` | `control` |
| Number | `spiderly-number` | `control` |
| TextArea | `spiderly-textarea` | `control` |
| CheckBox | `spiderly-checkbox` | `control` |
| Calendar | `spiderly-calendar` | `control`, `showTime` |
| Dropdown | `spiderly-dropdown` | `control`, `options: Namebook[]` |
| MultiSelect | `spiderly-multiselect` | `control`, `options` |
| Autocomplete | `spiderly-autocomplete` | `control`, `onTextInput` |
| MultiAutocomplete | `spiderly-multiautocomplete` | `control`, `onTextInput` |
| Editor | `spiderly-editor` | `control` |
| File | `spiderly-file` | `control` |
| ColorPicker | `spiderly-colorpicker` | `control` |
| Password | `spiderly-password` | `control` |

All controls share base inputs: `label`, `disabled`, `showLabel`, `showRequired`, `placeholder`, `showTooltip`, `tooltipText`.
