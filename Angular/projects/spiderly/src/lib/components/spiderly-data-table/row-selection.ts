import { firstValueFrom } from 'rxjs';
import { Filter } from '../../entities/filter';
import { LazyLoadSelectedIdsResult } from '../../entities/lazy-load-selected-ids-result';
import type { SpiderlyDataTableComponent } from './spiderly-data-table.component';

/** The table's inputs and outputs that the row selection reads and emits through. */
export type RowSelectionHost = Pick<
  SpiderlyDataTableComponent,
  | 'table'
  | 'items'
  | 'totalRecords'
  | 'hasLazyLoad'
  | 'idField'
  | 'selectionMode'
  | 'additionalIndexes'
  | 'selectedLazyLoadObservableMethod'
  | 'deleteListFromTableObservableMethod'
  | 'getAlreadySelectedItemIds'
  | 'getAlreadySelectedItems'
  | 'onRowSelect'
  | 'onRowUnselect'
  | 'onIsAllSelectedChange'
>;

/**
 * The checkbox column of a `spiderly-data-table`: which rows are selected, the delta a lazy table
 * sends to its backend, the shift+click range, and the per-row events. The table's
 * `newlySelectedItems` and `unselectedItems` inputs are these fields.
 */
export class RowSelection {
  newlySelectedItems: number[] = [];
  fakeSelectedItems: number[] = []; // Only for showing checkboxes, we will not send this to the backend
  currentPageSelectedItemsFromDb: number[] = []; // Made so we can add only newly selected items to the newlySelectedItems
  unselectedItems: number[] = [];
  rowsSelectedNumber: number = 0;
  isAllSelected: boolean = null;
  fakeIsAllSelected: boolean = false; // Only for showing checkboxes, we will not send this to the backend
  isFirstTimeLazyLoad: boolean = true;
  /**
   * The row a shift+click range runs from: the row the last click toggled. Only a wholesale row
   * replacement resets it; `renderedRows()` says why no other path needs to.
   */
  private rangeAnchorId: number | null = null;
  /**
   * The shift state of a press on a row's checkbox, paired with that row's id. It lives on the
   * mousedown because `p-checkbox` emits the DOM `change` event as `onChange.originalEvent`, and
   * `change` carries no `shiftKey`. `onSelectionCellMouseDown` captures it, and the same click's
   * `selectRow` consumes and clears it.
   */
  private pendingShift: { shiftKey: boolean; id: number } | null = null;
  selectedItemIds: number[] = []; // Pass only when hasLazyLoad === false, it's enough if the M2M association hasn't additional fields
  selectedItems: any[] = []; // Pass only when hasLazyLoad === false

  constructor(private readonly host: RowSelectionHost) {}

  get showSelectAllCheckbox(): boolean {
    return (
      this.host.selectionMode === 'multiple' &&
      this.host.selectedLazyLoadObservableMethod != null
    );
  }

  /** A wholesale row replacement calls this; `renderedRows()` says why no other path needs to. */
  forgetAnchor(): void {
    this.rangeAnchorId = null;
  }

  /**
   * Starts the page's selected-ids request beside the list request, or answers null for a table
   * with no `selectedLazyLoadObservableMethod`.
   *
   * Issued NOW rather than after the list lands: it needs only `tableFilter`, so serialising
   * it behind the list response cost a second round trip on every page of every
   * selection-enabled table — and the overlay is held up for both. The `.catch` at creation
   * is mandatory, not defensive: when the list request errors, `next` never runs, and a
   * rejected promise nobody awaits is exactly the unowned rejection this handler exists to
   * prevent. The rejection is re-thrown out of `reconcileLoadedPage` instead, where it has one.
   */
  requestSelectedIds(
    tableFilter: Filter,
  ): Promise<LazyLoadSelectedIdsResult | Error> | null {
    return this.host.selectedLazyLoadObservableMethod != null
      ? firstValueFrom(
          this.host.selectedLazyLoadObservableMethod(tableFilter),
        ).catch((error) => error as Error)
      : null;
  }

  /**
   * Repaints the checkbox column for the page that just landed. It does not await: the page
   * loader's subscriber awaits the selected ids and passes them here. That keeps the selection
   * algebra out of the subscriber's try/finally, which guarantees the pending flag.
   */
  reconcileLoadedPage(selectedRows: LazyLoadSelectedIdsResult | Error | null): void {
    if (selectedRows == null) {
      if (this.host.deleteListFromTableObservableMethod) {
        this.fakeSelectedItems = this.host.items
          .map((x) => x[this.host.idField])
          .filter((id) => this.newlySelectedItems.includes(id));
      }
      return;
    }

    if (selectedRows instanceof Error) throw selectedRows;

    this.currentPageSelectedItemsFromDb = [
      ...selectedRows.selectedIds,
    ];

    if (this.isFirstTimeLazyLoad == true) {
      this.rowsSelectedNumber = selectedRows.totalRecordsSelected;
      this.setFakeIsAllSelected();
      this.isFirstTimeLazyLoad = false;
    }

    if (this.isAllSelected == true) {
      let idsToInsert = [...this.host.items.map((x) => x[this.host.idField])];
      idsToInsert = idsToInsert.filter(
        (x) => this.unselectedItems.includes(x) == false,
      );
      this.fakeSelectedItems = [...idsToInsert]; // Only for showing checkboxes, we will not send this to the backend
    } else if (this.isAllSelected == false) {
      this.fakeSelectedItems = [...this.newlySelectedItems]; // Only for showing checkboxes, we will not send this to the backend
    } else if (this.isAllSelected == null) {
      let idsToInsert = [
        ...selectedRows.selectedIds,
        ...this.newlySelectedItems,
      ];
      idsToInsert = idsToInsert.filter(
        (x) => this.unselectedItems.includes(x) == false,
      );
      this.fakeSelectedItems = [...idsToInsert];
    }
  }

  /** The client-side table's selection, read from the consumer's form array. */
  loadClientSelection(): void {
    if (this.host.getAlreadySelectedItemIds) {
      this.selectedItemIds = this.host.getAlreadySelectedItemIds(
        this.host.additionalIndexes,
      );
    }
    if (this.host.getAlreadySelectedItems) {
      this.selectedItems = this.host.getAlreadySelectedItems(this.host.additionalIndexes);
    }
    this.rowsSelectedNumber = this.selectedItemIds.length;
    this.setFakeIsAllSelected();
  }

  reset() {
    this.newlySelectedItems.length = 0;
    this.unselectedItems.length = 0;
    this.fakeSelectedItems = [];
    this.rowsSelectedNumber = 0;
    this.isAllSelected = null;
    this.fakeIsAllSelected = false;
  }

  setFakeIsAllSelected() {
    if (this.rowsSelectedNumber == this.host.totalRecords)
      this.fakeIsAllSelected = true;
    else this.fakeIsAllSelected = false;
  }

  selectAll(checked: boolean) {
    this.unselectedItems.length = 0;
    this.newlySelectedItems.length = 0;

    if (checked == true) {
      this.isAllSelected = true;
      this.fakeIsAllSelected = true;
      this.host.onIsAllSelectedChange.next(
        new AllClickEvent({
          checked: true,
          additionalIndexes: this.host.additionalIndexes,
        }),
      );
      this.rowsSelectedNumber = this.host.totalRecords;
      this.fakeSelectedItems = [...this.host.items.map((x) => x[this.host.idField])];
      this.selectedItemIds = [...this.host.items.map((x) => x[this.host.idField])];
    } else {
      this.isAllSelected = false;
      this.fakeIsAllSelected = false;
      this.host.onIsAllSelectedChange.next(
        new AllClickEvent({
          checked: false,
          additionalIndexes: this.host.additionalIndexes,
        }),
      );
      this.rowsSelectedNumber = 0;
      this.fakeSelectedItems = [];
      this.selectedItemIds = [];
    }
  }

  /**
   * Captures the shift state for the checkbox change this press produces — id-paired so an
   * aborted press can never leak into a later toggle. Also stops shift+click from extending the
   * browser text selection, which starts at mousedown.
   *
   * Do not clear the flag on the cell's `click`. The checkbox's `change` fires after the click
   * dispatch completes, so a click-time clear runs first and kills every real range: the range
   * suite fails. The accepted gap: press a checkbox, drag off, release outside, then toggle that
   * row by keyboard, and it ranges. A document-level `mouseup` has the same order problem.
   */
  onSelectionCellMouseDown(event: MouseEvent, id: number) {
    // Arm only for a press on the checkbox itself: a press on the surrounding cell produces no
    // `change`, so arming there would strand the flag until some later toggle consumed it. The
    // text-selection preventDefault still covers the whole cell.
    if ((event.target as HTMLElement).closest('p-checkbox')) {
      this.pendingShift = { shiftKey: event.shiftKey, id };
    }
    if (event.shiftKey) event.preventDefault();
  }

  /**
   * The rows the user can currently see, in display order — PrimeNG's own answer, so it cannot
   * drift from what the table paints (it applies `filteredValue` and the paginator slice, and
   * caps lazy pages at `rows` even when the server overshoots). Resolving a range over raw
   * `items` would sweep filtered-out or off-page rows sitting between two visually adjacent
   * ones. An anchor outside this window misses and the shift-click degrades to a plain toggle.
   *
   * Never resolve a range over `rowData.index`: only `loadFormArrayItems` sets it. Because the
   * anchor is checked here, at use, a page flip and a client-side sort or filter need no reset of
   * their own.
   */
  private renderedRows(): any[] {
    return this.host.table?.dataToRender(null) ?? this.host.items;
  }

  selectRow(id: number, index: number) {
    const shiftRange =
      this.pendingShift?.id === id && this.pendingShift.shiftKey;
    this.pendingShift = null;

    // `null` is the no-anchor sentinel, so it must never resolve to a row: a client-side table
    // can hold unsaved rows whose idField is null, and findIndex would happily match one.
    if (shiftRange && this.rangeAnchorId != null && id != null) {
      const rendered = this.renderedRows();
      const indexOf = (rowId: number) =>
        rendered.findIndex((x) => x[this.host.idField] === rowId);
      const anchorIndex = indexOf(this.rangeAnchorId);
      const clickedIndex = indexOf(id);

      if (anchorIndex !== -1 && clickedIndex !== -1) {
        // The clicked checkbox's NEW state, applied to the whole range. Our model hasn't been
        // updated for this click yet, so the new state is the negation of what we hold.
        this.applyRange(
          rendered,
          anchorIndex,
          clickedIndex,
          !this.isRowSelected(id),
        );
        this.rangeAnchorId = id;
        return;
      }
    }

    this.toggleRow(id, index);
    this.rangeAnchorId = id;
  }

  /**
   * Single-row toggle — the plain-click path. Each per-row change the table makes, a click or a
   * range, goes through it, so the lazy delta model (`newlySelectedItems`, `unselectedItems`) and
   * the per-row events stay identical to a manual click. A caller must skip a row already in the
   * target state, as `applyRange` does, or the delta arrays get duplicate ids.
   */
  private toggleRow(id: number, index: number) {
    if (this.isRowSelected(id)) {
      this.rowUnselect(id);
      this.host.onRowUnselect.next(
        new RowClickEvent({
          index: index,
          id: id,
          additionalIndexes: this.host.additionalIndexes,
        }),
      );
    } else {
      this.rowSelect(id);
      this.host.onRowSelect.next(
        new RowClickEvent({
          index: index,
          id: id,
          additionalIndexes: this.host.additionalIndexes,
        }),
      );
    }
  }

  /**
   * Applies `select` to every row between the two positions in `rows` (inclusive), skipping rows
   * already in the target state; each change goes through `toggleRow()` (its comment says why).
   * The event payload keeps `row.index` for parity with what the single-click template binding
   * passes.
   */
  private applyRange(
    rows: any[],
    fromIndex: number,
    toIndex: number,
    select: boolean,
  ) {
    const start = Math.min(fromIndex, toIndex);
    const end = Math.max(fromIndex, toIndex);

    for (let i = start; i <= end; i++) {
      const rowId = rows[i][this.host.idField];
      if (this.isRowSelected(rowId) !== select) {
        this.toggleRow(rowId, rows[i].index);
      }
    }
  }

  isRowSelected(id: number) {
    if (this.host.hasLazyLoad) {
      return this.fakeSelectedItems.find((x) => x === id) != undefined;
    } else {
      return this.selectedItemIds.find((x) => x === id) != undefined;
    }
  }

  rowSelect(id: number) {
    if (
      this.isAllSelected == false ||
      this.currentPageSelectedItemsFromDb.includes(id) == false
    ) {
      this.newlySelectedItems.push(id);
    }

    if (this.host.hasLazyLoad) {
      this.fakeSelectedItems.push(id);
    } else {
      this.selectedItemIds.push(id);
    }

    this.rowsSelectedNumber++;

    const index = this.unselectedItems.indexOf(id);
    if (index !== -1) {
      this.unselectedItems.splice(index, 1); // Splice is mutating the array
    }

    this.setFakeIsAllSelected();
  }

  rowUnselect(id: number) {
    if (
      this.isAllSelected == true ||
      this.currentPageSelectedItemsFromDb.includes(id) == true
    ) {
      this.unselectedItems.push(id);
    }

    this.rowsSelectedNumber--;

    const index = this.newlySelectedItems.indexOf(id);
    const fakeIndex = this.fakeSelectedItems.indexOf(id);
    const nonLazyLoadIndex = this.selectedItemIds.indexOf(id);

    if (index !== -1) {
      this.newlySelectedItems.splice(index, 1); // Splice is mutating the array
    }
    if (fakeIndex !== -1) {
      this.fakeSelectedItems.splice(fakeIndex, 1); // Splice is mutating the array
    }
    if (nonLazyLoadIndex !== -1) {
      this.selectedItemIds.splice(nonLazyLoadIndex, 1); // Splice is mutating the array
    }

    this.setFakeIsAllSelected();
  }
}

export class RowClickEvent {
  index?: number;
  id?: number;
  additionalIndexes?: any;

  constructor({
    index,
    id,
    additionalIndexes,
  }: {
    index?: number;
    id?: number;
    additionalIndexes?: any;
  } = {}) {
    this.index = index;
    this.id = id;
    this.additionalIndexes = additionalIndexes;
  }
}

export class AllClickEvent {
  checked?: boolean;
  additionalIndexes?: any;

  constructor({
    checked,
    additionalIndexes,
  }: {
    checked?: boolean;
    additionalIndexes?: any;
  } = {}) {
    this.checked = checked;
    this.additionalIndexes = additionalIndexes;
  }
}
