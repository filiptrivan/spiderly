import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { readStoredJson, writeStoredJson } from '../../services/web-storage';

/** The table's inputs that say where its state persists. */
export interface TableStateStorageHost {
  readonly stateKey?: string;
  readonly additionalFilterIdLong: number;
  readonly stateStorage: 'session' | 'local';
}

/**
 * A piece of table state that each view keeps apart. `columns` holds the visibility overrides,
 * `layout` the wrap, order and widths, `filters` the applied filters, and `sort` the operator's
 * sort override.
 */
export type ViewStateSlot = 'columns' | 'layout' | 'filters' | 'sort';

/**
 * Where each piece of a `spiderly-data-table`'s state persists: its key and its storage. A table
 * with no state key persists nothing, so each read answers null and each write does nothing.
 *
 * A view-scoped key carries the ACTIVE VIEW, which is what makes a view more than a saved filter: a
 * picking view and a payments view want different COLUMNS, not just different rows. Global
 * layout would re-create the original complaint one level up — the operator still re-picking
 * columns every time the job changes. A view is a saved question, so each view keeps its own
 * filters too (decision 10).
 *
 * A table with no views has no segment, so the twenty-six tables that have not migrated keep
 * reading exactly the keys they already wrote.
 *
 * Construct it in the table's injection context, for the `Router` that the default key reads.
 */
export class TableStateStorage {
  private readonly router = inject(Router);

  /** The table's state key, or null before `resolveKey` runs and on a client-side table. */
  resolvedStateKey: string | null = null;

  constructor(private readonly host: TableStateStorageHost) {}

  /**
   * Derives the key from `stateKey`, else from the route, then `additionalFilterIdLong`. A lazy
   * table's `ngOnInit` calls it.
   */
  resolveKey(): void {
    const baseKey = this.host.stateKey ?? `spiderly-table:${this.router.url}`;
    this.resolvedStateKey =
      this.host.additionalFilterIdLong != null
        ? `${baseKey}:${this.host.additionalFilterIdLong}`
        : baseKey;
  }

  /** Whether the table has a state key, so that its state persists at all. */
  get persists(): boolean {
    return !!this.resolvedStateKey;
  }

  /** The slot's stored value in `viewId`'s view (null for a table without views), or null. */
  read(slot: ViewStateSlot, viewId: string | null): any {
    const key = this.keyOf(slot, viewId);

    return key ? readStoredJson(this.storageOf(slot), key) : null;
  }

  /** Stores `value` in the slot. Null removes the key: a slot at its default is stored as absence. */
  write(slot: ViewStateSlot, viewId: string | null, value: unknown): void {
    const key = this.keyOf(slot, viewId);
    if (!key) return;

    if (value == null) this.storageOf(slot).removeItem(key);
    else writeStoredJson(this.storageOf(slot), key, value);
  }

  /**
   * The active view's id. Deliberately NOT view-scoped — it is the input the view segment
   * derives from — and it follows `stateStorage` like the filters rather than the layout's
   * always-local rule, because the id and the filters it scopes are one question: a durable id
   * over session-scoped filters would restore a tab with nothing under it.
   */
  readActiveViewId(): unknown {
    return this.persists
      ? readStoredJson(this.lifetimeStorage, this.viewIdKey)
      : null;
  }

  writeActiveViewId(id: string): void {
    if (this.persists) writeStoredJson(this.lifetimeStorage, this.viewIdKey, id);
  }

  /**
   * PrimeNG's persisted table state (filters/sort/pagination), read directly from storage
   * because we need it before PrimeNG's restoreState() runs on the first [value] change.
   */
  readTableState(): any {
    return this.persists
      ? readStoredJson(this.lifetimeStorage, this.resolvedStateKey!)
      : null;
  }

  writeTableState(state: unknown): void {
    if (this.persists) {
      writeStoredJson(this.lifetimeStorage, this.resolvedStateKey!, state);
    }
  }

  private keyOf(slot: ViewStateSlot, viewId: string | null): string | null {
    if (!this.persists) return null;

    return `${this.resolvedStateKey}${viewId ? `:${viewId}` : ''}:${slot}`;
  }

  private get viewIdKey(): string {
    return `${this.resolvedStateKey}:view`;
  }

  /**
   * The column layout is ALWAYS localStorage, even when `stateStorage` is 'session': it is a
   * durable preference, while filters are a transient working set — different natural lifetimes.
   *
   * The filters follow `stateStorage` because that is where they lived before the bar took them
   * off the header: PrimeNG's stateful table persisted them for free, and nothing replaced it — a
   * refresh dropped every filter (Filip, on /tags). A filter is a question you are in the middle of
   * asking, not a durable preference like a column's width. The sort override is part of the same
   * question, so it follows `stateStorage` too.
   */
  private storageOf(slot: ViewStateSlot): Storage {
    return slot === 'columns' || slot === 'layout'
      ? localStorage
      : this.lifetimeStorage;
  }

  private get lifetimeStorage(): Storage {
    return this.host.stateStorage === 'local' ? localStorage : sessionStorage;
  }
}
