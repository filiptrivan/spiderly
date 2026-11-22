import { Column } from './data-table-column';

/**
 * Sizes the columns from their declared widths instead of from the rendered rows. The table binds
 * it as its `tableStyle`, and it is what makes every width below a share.
 *
 * The browser default is `table-layout: auto`, which computes every column from the content
 * of the rows currently on screen — so a column is one width on this page and another on the
 * next, and a filter (which narrows the variety of values in the column it filters) moves the
 * whole grid. PrimeNG sets no `table-layout` of its own, so without this the table inherits
 * that default. Bound to a field, never an object literal in the template: a literal is a new
 * reference on every change-detection pass.
 *
 * The table needs no `min-width`, and this style needs no computed floor. The browser already
 * widens a table past its container to the sum of its declared widths, so the container still
 * scrolls. The "too wide for its container scrolls" spec holds the measurement.
 */
export const FIXED_TABLE_LAYOUT = { 'table-layout': 'fixed' };

/** One rem in pixels. Both the share arithmetic and the px-to-share conversion need it. */
export function rootFontSizePx(): number {
  return parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
}

/**
 * Default column width per value type, in rem — generous on purpose (these numbers date from when
 * the header carried a filter input, and nobody has re-derived them from values yet), and
 * {@link Column.width} is the per-column override.
 *
 * A table, not a switch, so the mapping is exhaustive: a new `filterType` fails the build here
 * instead of silently inheriting the actions-column reservation below.
 */
const DEFAULT_COLUMN_WIDTH_REM: Record<
  NonNullable<Column['filterType']>,
  number
> = {
  text: 12,
  date: 10,
  multiselect: 12,
  boolean: 8,
  numeric: 12,
  // Fits the 45px thumbnail `#defaultCell` renders for a blob.
  blob: 5,
};

/** This column's share, override first, then its declared width, then the per-type default. */
export function columnShare(col: Column, override: number | undefined): number {
  if (override != null) return override;

  if (col.width != null) return shareOfDeclaredWidth(col);

  return defaultShare(col);
}

/**
 * The column's declared {@link Column.width}, or the default for its filter type.
 *
 * A width, not a minimum: under the fixed layout {@link FIXED_TABLE_LAYOUT} establishes, this is
 * what the column is sized from, and the table shares its surplus in PROPORTION to these numbers.
 * Columns declaring none would otherwise take an equal share, which throws away what the
 * per-type defaults say — a boolean holds "Da"/"Ne", a text column holds a name.
 *
 * Never `0rem`: auto layout read it as "shrink to content", but the fixed layout reads it as
 * zero and the column disappears. An actions column gets a real share too (`defaultShare()`).
 */
export function columnWidth(col: Column, override: number | undefined): string {
  if (override != null) return `${override}rem`;

  // A declared width passes through VERBATIM so a consumer keeps % and px; every other branch is
  // a share and renders in rem. Both halves resolve through `columnShare` otherwise, because
  // this chain and that one were written twice and had already drifted apart on the px case.
  if (col.width != null) return col.width;

  return `${defaultShare(col)}rem`;
}

/**
 * A declared width expressed in SHARE units. rem IS the share unit and px converts; any other
 * unit falls back to the per-type default rather than being read as a bare number.
 *
 * `parseFloat` alone shipped here first, so `width: '150px'` resolved to share 150 against
 * neighbours at 12 — one column eating the grid on the first drag or fit. Every declared width
 * in the consumer happens to be rem today, which is exactly why nothing would have caught it.
 */
function shareOfDeclaredWidth(col: Column): number {
  const declared = col.width!.trim();
  const value = parseFloat(declared);

  if (Number.isNaN(value)) return defaultShare(col);
  if (declared.endsWith('rem')) return value;
  if (declared.endsWith('px')) return value / rootFontSizePx();

  return defaultShare(col);
}

function defaultShare(col: Column): number {
  if (col.filterType) return DEFAULT_COLUMN_WIDTH_REM[col.filterType];

  // What is left declares no filterType: an actions column. It cannot shrink to fit any more,
  // so the icons need a reservation — each sits in a flex row whose gap is set beside them in
  // the template, inside the cell's own padding. 2.5, not 2.2, so the arithmetic stays exact in
  // binary and the string never reads `8.6000000001rem`.
  return 2 + (col.actions?.length ?? 0) * 2.5;
}
