// What a consumer declares for one column of a `spiderly-data-table`, and the payloads that its
// click callbacks receive.
import { PrimengOption } from '../../entities/primeng-option';

export class Action {
  name?: string;
  field?: string;
  icon?: string;
  style?: string;
  styleClass?: string;
  /**
   * Fired when a custom action is clicked. Receives an {@link ActionClickEvent} with the
   * row id, the full row object, the clicked DOM element (use it to anchor an overlay/popover),
   * and the original `MouseEvent`.
   *
   * Only fires for custom actions — the built-in `field` values `'Details'` and `'Delete'`
   * are handled internally and never invoke `onClick`.
   */
  onClick?: (event: ActionClickEvent) => void;

  constructor({
    name,
    field,
    icon,
    style,
    styleClass,
    onClick,
  }: {
    name?: string;
    field?: string;
    icon?: string;
    style?: string;
    styleClass?: string;
    onClick?: (event: ActionClickEvent) => void;
  } = {}) {
    this.name = name;
    this.field = field;
    this.icon = icon;
    this.style = style;
    this.styleClass = styleClass;
    this.onClick = onClick;
  }
}

export class Column<T = any> {
  name?: string;
  field?: string & keyof T;
  /**
   * The column's VALUE SHAPE: it sizes the column (`getColWidth`), picks the cell rendering
   * (`getRowData`, the blob branch) and the numeric right-align. The name is a fossil of the
   * header-filter era — filtering lives in the store handed to `[filters]` — kept because
   * renaming it would be a 195-declaration migration for zero behavior.
   */
  filterType?: 'text' | 'date' | 'multiselect' | 'boolean' | 'numeric' | 'blob';
  /**
   * The id of the filter in the table's store that this column stands for, which is what the
   * header menu's `Filter…` opens. A LINK, not a declaration: the filter exists in the store
   * whether or not any column names it, and a column needs this only to offer the shortcut.
   *
   * Untyped against the store on purpose — a `Column<T>` knows its row type and nothing about
   * which store it will be rendered beside, so the compile-time key check lives where the store
   * is declared (`createFilterStore`), not here.
   */
  filterId?: string;
  /**
   * @deprecated Dead since the header-filter deletion: NOTHING reads it. It survives on the type
   * only so details-table cols emitted by an OLDER Spiderly.SourceGenerators (which wrote
   * `showMatchModes: true`) still compile against this library during a mixed-version window —
   * a consumer on published NuGet generators plus an npm-linked library build. Delete it once
   * every consumer regenerates with a generator from this release or later (spiderly#407).
   */
  showMatchModes?: boolean;
  /**
   * @deprecated The same version-skew bridge as {@link showMatchModes}, and nothing else: older
   * generators emit `filterField: 'xxxId'` for every dropdown-typed details-table column, so
   * deleting it breaks the mixed window the moment a consumer adds one. NOTHING reads it —
   * filter ids live in the store, linked from a column via {@link filterId}. Delete with
   * `showMatchModes` (spiderly#407).
   */
  filterField?: string & keyof T;
  dropdownOrMultiselectValues?: PrimengOption[];
  actions?: Action[];
  editable?: boolean;
  showTime?: boolean;
  decimalPlaces?: number;
  /**
   * For a boolean column: show null as an empty cell, not as "No". Declare it on a `bool?` whose
   * null means "not set" (inherit, unknown). Leave it off a checkbox: Spiderly reads a checkbox's
   * null as false, and an untouched checkbox saves null.
   */
  triState?: boolean;
  sortable?: boolean;
  /**
   * Whether the column is initially rendered. Defaults to `true`. Declare `visible: false` for
   * columns that should be available in the column chooser but hidden until the user reveals them.
   * A view overrides it for itself through {@link TableView.columns}.
   */
  visible?: boolean;
  /**
   * Pins the column: it always renders and shows in the chooser as checked and disabled.
   * Use for the row's identifying column (e.g. a product's title) so the table can never
   * lose its anchor.
   */
  lockVisible?: boolean;
  /**
   * CSS length fixing this column's width (e.g. `'8rem'`). Overrides the per-type default,
   * which is generous for a column of short values.
   *
   * Declare a width on the columns that must stay NARROW and leave the flexible one undeclared:
   * the table distributes its surplus in proportion to the declared widths, so a column carrying
   * the long values wants the larger number, not a cap.
   */
  width?: string;
  /**
   * Fired when this column's cell is clicked. Receives a {@link CellClickEvent} with the row id,
   * the column field, the full row, the raw and formatted cell value, the clicked `<td>` element
   * (use it to anchor an overlay/popover), and the original `MouseEvent`.
   *
   * Setting this makes the cell visibly clickable (cursor + hover) and the click no longer bubbles
   * up to row navigation. The mirror of {@link Action.onClick}, but for plain value cells.
   * Not applied to editable cells — those belong to their inline input.
   */
  onCellClick?: (event: CellClickEvent) => void;

  constructor({
    name,
    field,
    filterType,
    filterId,
    showMatchModes,
    filterField,
    dropdownOrMultiselectValues,
    actions,
    editable,
    showTime,
    decimalPlaces,
    triState,
    sortable,
    visible,
    lockVisible,
    width,
    onCellClick,
  }: {
    name?: string;
    field?: string & keyof T;
    filterType?: 'text' | 'date' | 'multiselect' | 'boolean' | 'numeric' | 'blob';
    filterId?: string;
    showMatchModes?: boolean;
    filterField?: string & keyof T;
    dropdownOrMultiselectValues?: PrimengOption[];
    actions?: Action[];
    editable?: boolean;
    showTime?: boolean;
    decimalPlaces?: number;
    triState?: boolean;
    sortable?: boolean;
    visible?: boolean;
    lockVisible?: boolean;
    width?: string;
    onCellClick?: (event: CellClickEvent) => void;
  } = {}) {
    this.name = name;
    this.field = field;
    this.filterType = filterType;
    this.filterId = filterId;
    this.showMatchModes = showMatchModes;
    this.filterField = filterField;
    this.dropdownOrMultiselectValues = dropdownOrMultiselectValues;
    this.actions = actions;
    this.editable = editable;
    this.showTime = showTime;
    this.decimalPlaces = decimalPlaces;
    this.triState = triState;
    this.sortable = sortable;
    this.visible = visible;
    this.lockVisible = lockVisible;
    this.width = width;
    this.onCellClick = onCellClick;
  }
}

/**
 * Payload passed to {@link Action.onClick} when a custom row action is clicked.
 * Every field is populated by the data table, so consumers can rely on them being present.
 */
export interface ActionClickEvent {
  /** The clicked row's id (`row[idField]`). */
  id: number;
  /** The full row object the action belongs to. */
  row: any;
  /**
   * The clicked action element — pass it as the anchor when opening an overlay/popover.
   * Captured at click time on purpose: `originalEvent.currentTarget` is reset to null once
   * dispatch ends, so it would already be null inside an async handler.
   */
  element: HTMLElement;
  /** The original DOM click event. */
  originalEvent: MouseEvent;
}

/**
 * Payload passed to {@link Column.onCellClick} when a cell is clicked. A superset of
 * {@link ActionClickEvent} (same `id` / `row` / `element` / `originalEvent`) plus the cell's
 * column and value. Every field is populated by the data table.
 */
export interface CellClickEvent extends ActionClickEvent {
  /** The clicked column's `field`. */
  field: string;
  /** The cell's raw value (`row[field]`). */
  value: any;
  /** The formatted value shown in the cell (what the table renders via `getRowData`). */
  displayValue: string;
}
