import { DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Observable } from 'rxjs';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import {
  DeleteConfirmationData,
  SpiderlyDeleteConfirmationComponent,
} from '../spiderly-delete-dialog/spiderly-delete-confirmation.component';
import { CellDisplay } from './cell-display';
import { Action, ActionClickEvent, Column } from './data-table-column';
import { RowSelection } from './row-selection';

/** The table's inputs that say where a row leads and how a row is deleted, and its reload. */
export interface RowActionsHost {
  readonly idField: string;
  readonly navigateOnRowClick: boolean;
  readonly rowNavigationPath: string;
  readonly deleteItemFromTableObservableMethod: (rowId: number) => Observable<any>;
  readonly deleteListFromTableObservableMethod: (ids: number[]) => Observable<any>;
  reload(): void;
}

/**
 * What a click in a `spiderly-data-table`'s body does: open a row's details, run a cell's or an
 * action's callback, or delete rows after the operator confirms. Construct it in the table's
 * injection context, for the router, the dialog and the messages.
 */
export class RowActions {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialogService = inject(DialogService);
  private readonly messageService = inject(SpiderlyMessageService);
  private readonly translocoService = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  deleteRef: DynamicDialogRef;

  constructor(
    private readonly host: RowActionsHost,
    private readonly selection: RowSelection,
    private readonly cells: CellDisplay,
  ) {}

  /*
   * Navigate to details page based on rowId and rowNavigationPath.
   * If rowNavigationPath is provided, it will navigate to that path with the rowId.
   * If not, it will navigate to the current route with the rowId.
   */
  navigateToDetails(rowId: number): void {
    if (rowId == null) return;

    if (this.host.rowNavigationPath) {
      const cleanPath = this.host.rowNavigationPath.replace(/^\/|\/$/g, ''); // Remove leading and trailing slashes

      this.router.navigateByUrl(`/${cleanPath}/${rowId}`);
    } else {
      this.router.navigate([rowId], { relativeTo: this.route });
    }
  }

  /**
   * Handle row click event. Arbitration between "this click navigates the row" and "this click
   * belongs to a control inside the row" happens HERE, once, rather than in each interactive
   * surface: anything marked `.row-interactive` (the selection cell, the actions column, editable
   * cells, opted-in `onCellClick` cells) owns its own click and never navigates. Per-surface
   * `stopPropagation` used to be the mechanism and was structurally lossy — the actions column
   * never had one, so a Delete click opened the dialog AND navigated away. Mark new interactive
   * cells with the class; don't add another stop.
   */
  onRowClick(row: any, event: MouseEvent): void {
    if (!this.host.navigateOnRowClick) return;
    if ((event.target as HTMLElement)?.closest?.('.row-interactive')) return;

    this.navigateToDetails(row?.[this.host.idField]);
  }

  /*
   * Handle a cell click. Only columns that opt in (have an `onCellClick` callback) react; for them
   * we stop propagation, then invoke the consumer callback with a fully-populated CellClickEvent.
   * Inert cells fall through to onRowClick unchanged.
   * The `stopPropagation` is no longer what stops row navigation: `.row-interactive` does that, in
   * `onRowClick`. It stays because it is a documented consumer contract, and it shields a
   * consumer's own outer click handlers.
   */
  onCellClick(col: Column, rowData: any, event: MouseEvent): void {
    if (!col.onCellClick) return;
    event.stopPropagation();
    col.onCellClick({
      ...this.buildClickEvent(rowData, event),
      field: col.field,
      value: rowData[col.field],
      displayValue: this.cells.getRowData(rowData, col),
    });
  }

  /*
   * Build the base click payload shared by custom action clicks and cell clicks. The clicked element
   * is captured here, not read later from `originalEvent.currentTarget` (which nulls after dispatch).
   */
  private buildClickEvent(rowData: any, event: MouseEvent): ActionClickEvent {
    return {
      id: rowData[this.host.idField],
      row: rowData,
      element: event.currentTarget as HTMLElement,
      originalEvent: event,
    };
  }

  getMethodForAction(action: Action, rowData: any, event: MouseEvent) {
    switch (action.field) {
      case 'Details':
        return this.navigateToDetails(rowData[this.host.idField]);
      case 'Delete':
        return this.deleteObject(rowData[this.host.idField]);
      default:
        return action.onClick(this.buildClickEvent(rowData, event));
    }
  }

  deleteObject(rowId: number) {
    this.openDeleteConfirmation(
      {
        deleteItemFromTableObservableMethod:
          this.host.deleteItemFromTableObservableMethod,
        id: rowId,
        message: this.translocoService.translate('spiderly.PleaseConfirmToProceed'),
      },
      'spiderly.SuccessfullyDeletedMessage',
    );
  }

  deleteSelectedObjects() {
    const selectedIds = [...this.selection.newlySelectedItems];

    if (selectedIds.length === 0) return;

    this.openDeleteConfirmation(
      {
        deleteListFromTableObservableMethod:
          this.host.deleteListFromTableObservableMethod,
        ids: selectedIds,
        message: this.translocoService.translate('spiderly.DeleteBulkConfirmation', {
          count: selectedIds.length,
        }),
      },
      'spiderly.SuccessfullyDeletedListMessage',
      () => this.selection.reset(),
    );
  }

  private openDeleteConfirmation(
    data: DeleteConfirmationData,
    successMessageKey: string,
    onSuccess?: () => void,
  ) {
    this.deleteRef = this.dialogService.open(
      SpiderlyDeleteConfirmationComponent,
      {
        header: this.translocoService.translate('spiderly.AreYouSure'),
        width: '400px',
        data,
      },
    );

    this.deleteRef.onClose
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((deletedSuccessfully: boolean) => {
        if (deletedSuccessfully === true) {
          this.messageService.successMessage(
            this.translocoService.translate(successMessageKey),
          );
          onSuccess?.();
          this.host.reload();
        }
      });
  }
}
