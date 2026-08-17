---
name: angular-customization
description: Build, compose, or style any Spiderly Angular admin-panel UI — pages, cards, panels, dashboards, buttons, tables, dialogs, empty/loading states. Use to reuse existing Spiderly/PrimeNG components instead of hand-writing Tailwind/HTML, and when extending generated components, overriding form save behavior, configuring data tables, customizing layout/theme, or adding validators. For translating UI strings (Transloco, assets/i18n), use the frontend-localization skill.
---

# Angular Customization

> **Scope:** Angular admin panel only — not storefront apps (Next.js + shadcn/Tailwind), where plain Tailwind is correct.

## Reuse components before hand-writing UI

Before building or restyling any admin UI, check for an existing component first. Prefer: **Spiderly (`spiderly-*`) → PrimeNG (`p-*`) → raw Tailwind/HTML.** Spiderly is built on PrimeNG, so the theme applies to both. See the catalogs below; unsure a wrapper exists? `grep -rE "selector:.*spiderly-" projects/spiderly/src/lib`.

**Look before you build — don't force-fit a component you'd have to hack.** Raw Tailwind/HTML is correct for pure layout/spacing, one-off widgets with no component analog, or when forcing a component means fighting it. When you hand-roll, state in one line why no component fit.

**Same rule for utility functions.** Before writing a formatting / date / file / dropdown-option helper, check the shared catalog — see [references/helper-functions.generated.md](references/helper-functions.generated.md) (from `helper-functions.ts`). Re-implementing `exportListToExcel`, `getPrimengDropdownNamebookOptions`, `kebabToTitleCase`, and friends is a common, avoidable duplication.

Keep admin UI **responsive and mobile-first**, matching the layout/grid conventions of surrounding pages rather than inventing a new one.

## Global loading & error handling — don't hand-roll either

Two cross-cutting behaviors ship with every Spiderly admin and cover **every** generated-client call automatically. Re-implementing them per call is the most common needless boilerplate in admin code:

- **Loading:** an HTTP interceptor shows the global full-screen blocking spinner for every request and hides it when the request settles. Read-shaped responses (autocomplete, dropdowns, table pagination, bare-scalar GETs) are exempted server-side via `[SkipSpinner]` inference — see the `custom-endpoints` skill to tune that per endpoint. Don't add per-component spinners around API calls; for layout placeholders use `card-skeleton` (catalog below).
- **Errors:** an HTTP interceptor toasts every failed request — server-unreachable warning, the server's `BusinessException` message on 400, login/permission/not-found messages on 401/403/404, a generic error toast for the rest — then **rethrows**, so callers only ever run their success path. Uncaught non-HTTP runtime errors get a generic toast from the global `ErrorHandler`. Both surfaces `console.error` every error, production included — the toast is deliberately vague, so the console is where a developer reads what actually broke. That toast tells the user the team was notified, which is only true if you make it so: **wire an error tracker** by providing an `ErrorHandler` that reports and then delegates to `SpiderlyErrorHandler` (inject it) for the toast. Report non-HTTP errors only — the interceptor already owns HTTP-error UX, and a server 5xx is in your backend's tracker under the same `traceId` the toast shows the user. So: **subscribe to the success path only** — no per-call `catchError` toasts, no `try/catch` around generated client calls. Add `catchError` only when you need to *react* to a specific failure (e.g. roll back optimistic UI state); the user-facing message has already been shown by the time your handler runs.

## Generated File Structure

```
Frontend/src/app/business/
├── entities/entities.generated.ts         # TypeScript DTOs
├── services/api/api.service.generated.ts  # Typed API methods
├── components/base-details.generated.ts   # Entity form components
├── services/validators/validators.generated.ts
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
    saveBody.productDTO.stock =
      saveBody.orderedProductVariantsSaveBodyDTO.reduce(
        (sum, v) => sum + (v.productVariantDTO.stock ?? 0),
        0,
      );
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

### Conditional visibility (`show*` inputs)

Every property block the generator emits into `{Entity}BaseDetailsComponent` is wrapped in `*ngIf="show{PropertyName}For{EntityName}"`, and each is exposed as an `@Input()` defaulting to `true`. Bind it from your `{Entity}DetailsComponent` template to show/hide a field — conditionally or statically — without editing generated code:

```html
<user-base-details
  [parentFormGroup]="parentFormGroup"
  [showIsDisabledForUser]="isAdmin"          <!-- show a field only when a condition holds -->
  [showEmailForUser]="false"                 <!-- hide a field outright -->
  [showTimeOnBirthDateForUser]="true"        <!-- calendar control: also render the time picker -->
  (onSave)="onSave()"
></user-base-details>
```

Names are PascalCase: `show{PropertyName}For{EntityName}` (plus `showTimeOn{PropertyName}For{EntityName}` for calendar controls).

Library components/controls also expose their own `show*` `@Input()`s (e.g. `showLabel` on every control, `showAddButton` on the data table, `showPanelHeader` on the panel) — each a plain boolean, bound the same way: `[showAddButton]="canCreate"`. See the *UI Controls Reference* and *Presentational & Layout Components* tables below for the available inputs.

### Replacing a field with your own control (`below*` slots)

Each property block also emits an `<ng-content select="[below{PropertyName}For{EntityName}]">` right where the field sits. Pair it with the `show*` input to swap a generated control for your own without editing generated code: hide the generated one, project yours into its slot, and bind yours to the same form control so the save body picks the value up unchanged.

```html
<user-base-details [parentFormGroup]="parentFormGroup" [showEmailForUser]="false" (onSave)="onSave()">
  <div belowEmailForUser>
    @if (parentFormGroup.controls.userDTO; as userDTO) {
      <spiderly-textbox [control]="userDTO.getControl('email')"></spiderly-textbox>
    }
  </div>
</user-base-details>
```

**The `@if` is required, not defensive.** Projected content belongs to *your* view, and Angular creates it with your view — the `@defer` the generated form sits behind does not defer it. So your control renders while `parentFormGroup` is still the empty group `BaseFormComponent` starts with, before `get{Entity}MainUIFormDTO` answers, and `controls.{entity}DTO` is undefined for those first change-detection passes. Unguarded, `controls.{entity}DTO.getControl(...)` throws on each one, and the global `ErrorHandler` turns every throw into the generic error toast. It stops once the data lands, so the symptom is a burst of error toasts on entering the page and then a form that works — easy to misread as a server problem.

Two details that decide whether the guard actually works:

- **Keep the slot attribute on a real element and put the `@if` inside it.** An `@if` anchor node carries no attribute selector, so wrapping the `<div belowEmailForUser>` in an `@if` drops the content into the default slot instead. (Same rule as conditional `[buttons]`, which needs `<ng-container ngProjectAs="[buttons]">`.)
- **`?.` alone is not a substitute.** It hands the control `undefined`, which every control's *template* renders fine, but `spiderly-autocomplete`, `spiderly-colorpicker` and `spiderly-checkbox` (with `initializeToFalse`) dereference the control in their own `ngOnInit` and still throw. A component of your own has to be equally tolerant if you rely on `?.` instead of the guard.

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

Lazy tables are always deterministically ordered: when no sort is active the backend falls back to `Id` DESC (newest first). Declare `defaultSortField` (+ `defaultSortOrder`) when a page wants a different, user-visible default — it renders as a normal header sort arrow, persisted user sort wins over it, and un-sorting (tri-state header click, Clear filters) returns to it instead of "unsorted".

### Client-Side Mode

```html
<spiderly-data-table [cols]="cols" [items]="localItems" [hasLazyLoad]="false">
</spiderly-data-table>
```

### Column Definition

```typescript
cols: Column<ProductDTO>[] = [
  new Column({ field: 'title', name: 'Title', filterType: 'text', lockVisible: true }),
  new Column({ field: 'price', name: 'Price', filterType: 'numeric', showMatchModes: true, decimalPlaces: 2 }),
  new Column({ field: 'createdAt', name: 'Created', filterType: 'date', showTime: true, visible: false }),
  new Column({ field: 'isActive', name: 'Active', filterType: 'boolean' }),
  new Column({ field: 'categoryDisplayName', name: 'Category', filterType: 'multiselect',
    dropdownOrMultiselectValues: this.categoryOptions }),
  new Column({
    actions: [
      new Action({ field: 'Details', icon: 'pi pi-pencil' }),
      new Action({ field: 'Delete' }),
      new Action({ field: 'custom', name: 'Clone', icon: 'pi pi-copy', onClick: (e) => this.clone(e.id) }),
    ]
  }),
];
```

Headers are click-to-sort by default; disable with `sortable: false`. Columns whose `field` ends in `CommaSeparated` are auto-disabled — the backend generates no sort case for collection columns and rejects unknown sort fields with a 400, so the table never offers the click.

Column widths default per `filterType` and are sized for the **header** — the filter input plus its match-mode dropdown — so a column of short values (an amount, a status code) reserves more than it uses. `minWidth: '8rem'` overrides that floor for one column; it stays a minimum, so the table still distributes the leftover space.

### Column chooser (show/hide columns)

Lazy tables render a **Columns** toolbar button opening a checkbox list of all data columns. Because a column's header is the table's only filter surface, showing a column is what makes it filterable — declare rarely-needed but filter-worthy columns with `visible: false` (available in the chooser, hidden by default) and pin the row's identifying column with `lockVisible: true`. Rules the table enforces: hiding a column clears its active filter + sort (one reload; plain hides don't hit the server), choices persist per table in `localStorage` under `` `${stateKey}:columns` `` (durable even with `stateStorage: 'session'`; only explicit toggles are stored, so later declared-default changes flow through), the last visible data column can't be hidden, and *Reset to default* restores the declared configuration.

The `onClick` callback receives an `ActionClickEvent` — `{ id, row, element, originalEvent }`. Use `element` (or `originalEvent`) to anchor an overlay/popover to the clicked action; `row` gives you the full row object. It fires only for custom `field` values (never `'Details'` / `'Delete'`).

For a click on the cell value itself (not an action icon), set a column's `onCellClick` callback — the mirror of `Action.onClick`, but for plain value cells:

```typescript
new Column({ field: 'total', name: 'Total', filterType: 'numeric',
  onCellClick: (e) => this.showItems(e) }),
```

It receives a `CellClickEvent` — `{ id, field, row, value, displayValue, element, originalEvent }`, where `value` is the raw cell value and `displayValue` is the formatted text shown. Only columns that set it become clickable (they get a `cursor`/hover affordance), and the click stops propagation so it does **not** also trigger `navigateOnRowClick`. Anchor overlays with `element` — it's the clicked `<td>`, captured synchronously, so it stays valid inside an async handler (`originalEvent.currentTarget` is null once dispatch ends). Not applied to editable cells.

**Reusing ONE popover across cells: close it and reopen it, never re-anchor in place.** Two PrimeNG 19.1.3 bugs make the in-place move (`show()` then `align()`) unfixable from outside the library: `align()` only ever *adds* its flip class and arrow offset and never clears them, so moving from a cell the panel opened above to one it opens below leaves the arrow pointing the wrong way; and PrimeNG emits `(onHide)` from inside its own animation callback, one step *before* it clears `isOverlayAnimationInProgress`, so a `show()` fired from there hits the library's own guard and silently does nothing. Stash the new anchor, `hide()`, and reopen from `(onHide)` deferred by one microtask — every open then runs `align()` once on a fresh container:

```typescript
onCellClick: (e) => {
  if (this.popover.overlayVisible) {
    this.reopenAnchor = e.element;   // re-anchor: close first, reopen from (onHide)
    this.popover.hide();
  } else {
    this.popover.show(e.originalEvent, e.element);
  }
},
```

```typescript
onPopoverHide(): void {
  const el = this.reopenAnchor;
  if (el == null) return;            // ordinary dismiss (outside click / Esc)
  this.reopenAnchor = null;
  queueMicrotask(() => this.popover.show(null, el));
}
```

The cost is a fade-out/fade-in instead of a slide. Re-check on each PrimeNG bump and delete the dance once both bugs are fixed upstream.

Style the popover's content at your component's SCSS **top level**, never nested under `:host` — PrimeNG appends the open popover to `document.body`, where `:host`-scoped rules (compiled to `[_nghost] … [_ngcontent]` descendant selectors) silently stop matching; top-level rules keep the `[_ngcontent]` scoping that travels with the moved nodes.

### Custom Cell Content (per-column templates)

Project an `<ng-template spiderlyCellTemplate="field">` to render one column's cells yourself instead of the plain formatted value. Import `SpiderlyCellTemplateDirective` in the consuming component. Columns with no matching template keep the built-in rendering, so a table opts in one column at a time.

```html
<spiderly-data-table [cols]="cols" [getPaginatedListObservableMethod]="getListMethod">
  <ng-template spiderlyCellTemplate="orderNumber" let-row let-displayValue="displayValue">
    <a [routerLink]="['/orders', row.id]">{{ displayValue }}</a>
    <div class="secondary">{{ row.itemCount }} items</div>
  </ng-template>
</spiderly-data-table>
```

- Context: `let-row` (`$implicit`, the full row), `let-value="value"` (raw), `let-displayValue="displayValue"` (**what the table would have rendered** — formatted for the column's `filterType` and the app's `LOCALE_ID`, so a template that only decorates the value never re-implements that), `let-col="col"`. `value` / `displayValue` mean the same here as on `CellClickEvent`, so a column carrying both a click handler and a template reads one vocabulary.
- **Cells only.** The header, its filter and its sorting are untouched, and the filter still works on the underlying `field` whatever the template draws.
- The template binds to the **consuming component**, so its handlers and pipes are yours.
- A real `<a [routerLink]>` here beats `onCellClick` for navigation — middle-click and Ctrl+click open a new tab, which a JavaScript `navigate()` does not.
- This is also the way out of the enum-cell limit: a `multiselect` column renders the raw stored value in its cell (the table does not map it through `dropdownOrMultiselectValues`), so a template that draws the label from the row's id gets the checkbox filter *and* readable cells.

### Custom Toolbar Actions

Project an `<ng-template spiderlyDataTableActions>` to add your own buttons (or any markup) to the table's toolbar, alongside the built-in Clear Filters / Export to Excel / Reload / Delete Selected buttons. Import `SpiderlyDataTableActionsDirective` in the consuming component.

```html
<spiderly-data-table [cols]="cols" [getPaginatedListObservableMethod]="getListMethod">
  <ng-template spiderlyDataTableActions>
    <button pButton class="p-button-outlined" style="flex: none"
            icon="pi pi-check" [label]="t('ApproveAll')" (click)="approveAll()"></button>
  </ng-template>
</spiderly-data-table>
```

- The projected content renders **ahead of** the built-in buttons, so its position stays stable even as the conditional Delete Selected button appears/disappears.
- The template binds to the **consuming component** (`(click)` calls your method), and root-level buttons inherit the toolbar's spacing + responsive stacking.
- No table state is passed as context — read it from the table's outputs (`onLazyLoad` for the current `Filter`, `onTotalRecordsChange`, `onRowSelect`) when an action needs it.

### Row selection (checkbox column + shift-click ranges)

`selectionMode: 'multiple'` renders a checkbox column (auto-enabled when `deleteListFromTableObservableMethod` is set). Clicking a checkbox never triggers `navigateOnRowClick` — the selection cell stops the click.

**Shift+click selects a range**: click one checkbox, hold Shift, click another — every row between receives the *clicked checkbox's new state* (shift-checking selects the range, shift-unchecking clears it). The anchor is the last clicked checkbox and resets whenever the rendered rows change (page, sort, filter) — ranges never span pages. Rows already in the target state are skipped; each actual change emits `onRowSelect`/`onRowUnselect` exactly as a single click would, so delta-based consumers (`newlySelectedItems`/`unselectedItems`) need no changes.

### Key Inputs

| Input                              | Type                     | Default | Purpose                 |
| ---------------------------------- | ------------------------ | ------- | ----------------------- |
| `cols`                             | `Column[]`               | —       | Column definitions      |
| `getPaginatedListObservableMethod` | `(filter) => Observable` | —       | Server-side data source |
| `additionalFilterIdLong`           | `number`                 | —       | Parent entity filter    |
| `hasLazyLoad`                      | `boolean`                | `true`  | Server vs client mode   |
| `items`                            | `any[]`                  | —       | Client-side data        |
| `selectionMode`                    | `'single' \| 'multiple'` | —       | Selection mode          |
| `navigateOnRowClick`               | `boolean`                | `false` | Click row → details     |
| `rowNavigationPath`                | `string`                 | —       | Base path for row click |
| `defaultSortField`                 | `string`                 | —       | Sort applied while the user has none |
| `defaultSortOrder`                 | `1 \| -1`                | `1`     | Direction for `defaultSortField` |
| `showAddButton`                    | `boolean`                | `true`  | Show "New" button       |
| `showExportToExcelButton`          | `boolean`                | `true`  | Show Excel export       |
| `readonly`                         | `boolean`                | `false` | Disable mutations       |

### Key Outputs

| Output                  | Payload         | Purpose               |
| ----------------------- | --------------- | --------------------- |
| `onRowSelect`           | `RowClickEvent` | Row selected          |
| `onRowUnselect`         | `RowClickEvent` | Row deselected        |
| `onIsAllSelectedChange` | `AllClickEvent` | Select-all toggled    |
| `onTotalRecordsChange`  | `number`        | Total records updated |

## Service Overrides

### ConfigServiceBase

```typescript
@Injectable({ providedIn: "root" })
export class ConfigService extends ConfigServiceBase {
  override logoPath = "assets/images/my-logo.png";
  override companyName = "My Company";
  override primaryColor = "#3B82F6";
  override defaultPageSize = 25;
  override loginSlug = "sign-in";
  override showGoogleAuth = true;
}
```

Key properties: `apiUrl`, `frontendUrl`, `GoogleClientId`, `companyName`, `primaryColor`, `logoPath`, `defaultPageSize`, `loginSlug`, `showGoogleAuth`.

### AuthServiceBase

Override hooks for custom post-auth behavior:

```typescript
export class AuthService extends AuthServiceBase {
  override onAfterLoginExternal = () => {
    this.analyticsService.trackLogin("google");
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
  inputStyle: "outlined", // 'outlined' | 'filled'
  colorScheme: "light", // 'light' | 'dark'
  menuMode: "static", // 'static' | 'overlay'
  scale: 14, // Font scale
  ripple: false,
  theme: "lara-light-indigo",
  color: "var(--p-primary-color)",
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

The side menu's desktop collapse state persists automatically (localStorage key `spiderly-layout:menu-desktop-inactive`), so the sidebar stays collapsed/expanded across reloads.

## Validation

### ValidatorAbstractService

Subclass to add custom validators per entity/field:

```typescript
@Injectable({ providedIn: "root" })
export class MyValidatorService extends ValidatorAbstractService {
  setValidator(
    control: SpiderlyFormControl,
    className: string,
  ): SpiderlyValidatorFn {
    if (className === "Product" && control.label === "sku") {
      const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
        const value = control.value as string;
        if (value && !value.match(/^[A-Z0-9]{6,12}$/))
          return { _: this.translocoService.translate("InvalidSKU") };
        return null;
      };
      control.validator = validator;
    }
    return control.validator;
  }

  setFormArrayValidator(formArray: SpiderlyFormArray, className: string): void {
    if (className === "OrderItems") {
      this.isFormArrayEmpty(formArray);
    }
  }
}
```

### Built-in Validators

The built-in validators on `ValidatorAbstractService` (with signatures) are generated from the class: see [references/validators.generated.md](references/validators.generated.md).

## Translations

UI string translation — Transloco setup, `assets/i18n/{lang}.json` files, `translocoService.translate` / the `*transloco` template directive, and form label auto-translation (`getTranslatedLabel`) — is covered by the dedicated **frontend-localization** skill. For server-side (.NET) strings, see **backend-localization**.

## UI Controls Reference

Two generated references cover the controls:

- **Control type codes** — what you pass to `[UIControlType(nameof(UIControlTypeCodes.X))]`, and the property type each is auto-selected for: [references/ui-control-types.generated.md](references/ui-control-types.generated.md).
- **Control components** — each `spiderly-*` selector, its component class, control-specific `@Input()`s, and the shared `BaseControl` inputs: [references/controls.generated.md](references/controls.generated.md).

## Presentational & Layout Components

Reach for these before hand-rolling cards, panels, buttons, lists, or empty/loading states.

| Component           | Selector                                     | Purpose                                                                 | Key inputs                                                                                                                    |
| ------------------- | -------------------------------------------- | ----------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| Card                | `spiderly-card`                              | Titled content container with icon                                      | `title`, `icon`                                                                                                               |
| Panel               | `spiderly-panel`                             | Collapsible section, supports multi-panel grouping + CRUD menu          | `toggleable`, `collapsed`, `crudMenu`, `showRemoveIcon`, `isFirstMultiplePanel`/`isMiddleMultiplePanel`/`isLastMultiplePanel` |
| Panel parts         | `panel-header`, `panel-body`, `panel-footer` | Compose a panel's regions                                               | (slotted content)                                                                                                             |
| Info card           | `info-card`                                  | Inline informational/callout box                                        | `header`, `icon`, `showSmallIcon`, `textColor`                                                                                |
| Index card          | `index-card`                                 | Ordered list item with index + CRUD menu                                | `index`, `header`, `description`, `crudMenu`, `showRemoveIcon`, `last`                                                        |
| Card skeleton       | `card-skeleton`                              | **Loading placeholder** — use instead of a hand-rolled spinner/skeleton | `height`                                                                                                                      |
| Button              | `spiderly-button`                            | Themed button                                                           | `type` (`button`/`submit`/`reset`)                                                                                            |
| Split button        | `spiderly-split-button`                      | Button with dropdown menu                                               | `dropdownItems`                                                                                                               |
| Return button       | `return-button`                              | Back-navigation button                                                  | `navigateUrl`                                                                                                                 |
| Data view           | `spiderly-data-view`                         | Card/grid list with filters + pagination (vs. table)                    | `items`, `rows`, `filters`, `getPaginatedListObservableMethod`, `showCardWrapper`                                             |
| Delete confirmation | `spiderly-delete-confirmation`               | Standard delete-confirm dialog                                          | —                                                                                                                             |
| Not found           | `not-found`                                  | **Empty / 404 state** — use instead of a hand-rolled empty state        | —                                                                                                                             |

For data lists, use `spiderly-data-table` (tabular — see Data Table section) or `spiderly-data-view` (card/grid) — don't build either from scratch.
