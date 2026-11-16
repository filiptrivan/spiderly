import { formatDate, formatNumber } from '@angular/common';
import { inject, LOCALE_ID, QueryList, TemplateRef } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import {
  CellTemplateContext,
  SpiderlyCellTemplateDirective,
} from '../../directives/spiderly-cell-template.directive';
import {
  getHtmlImgDisplayString64,
  parseDateOnlyLocal,
} from '../../services/helper-functions';
import { SpiderlyFormControl } from '../spiderly-form-control/spiderly-form-control';
import { Action, Column } from './data-table-column';

/** The table's projected cell templates, and the inputs an editable cell reads its control from. */
export interface CellDisplayHost {
  readonly cellTemplates: QueryList<SpiderlyCellTemplateDirective>;
  readonly getFormControl: (
    formControlName: string,
    index: number,
    additionalIndexes?: any,
  ) => SpiderlyFormControl;
  readonly additionalIndexes: any;
}

/**
 * How a `spiderly-data-table` shows a cell: the consumer's template or its formatted value by the
 * column's `filterType`, its alignment, an editable cell's control, and its action icons.
 * Construct it in the table's injection context, for the locale and `TranslocoService`.
 */
export class CellDisplay {
  private readonly locale = inject(LOCALE_ID);
  private readonly translocoService = inject(TranslocoService);

  constructor(private readonly host: CellDisplayHost) {}

  getRowData(rowData: any, col: Column): string {
    switch (col.filterType) {
      case 'text':
        return rowData[col.field];
      case 'date':
        if (rowData[col.field] == null) return null;

        if (col.showTime)
          return formatDate(rowData[col.field], 'short', this.locale);

        const raw = rowData[col.field];
        const local = typeof raw === 'string' ? parseDateOnlyLocal(raw) : null;
        return formatDate(local ?? raw, 'shortDate', this.locale);
      case 'multiselect': {
        // Translate through the column's own option list. Without this the cell shows the stored
        // value, so an enum column reads `1` instead of "Info", and an FK column reads the id.
        //
        // Match on `code`, not `value`: that is the property PrimengOption carries (see the note
        // on the class). The raw value is the fallback rather than an empty cell, because the
        // options are usually filled asynchronously after the first paint — blanking the column
        // until they land would be a worse regression than the number this replaces.
        const multiselectValue = rowData[col.field];
        if (multiselectValue == null) return null;

        const option = col.dropdownOrMultiselectValues?.find(
          (x) => x.code === multiselectValue,
        );
        return option?.label ?? multiselectValue;
      }
      case 'boolean':
        if (col.triState && rowData[col.field] == null) return null;
        return rowData[col.field] == true
          ? this.translocoService.translate('spiderly.Yes')
          : this.translocoService.translate('spiderly.No');
      case 'numeric':
        if (rowData[col.field] == null) return null;
        return formatNumber(
          rowData[col.field],
          this.locale,
          col.decimalPlaces != null
            ? `1.${col.decimalPlaces}-${col.decimalPlaces}`
            : '1.0-2',
        );
      case 'blob':
        return getHtmlImgDisplayString64(rowData[col.field]);
      default:
        return null;
    }
  }

  /**
   * The template that renders this column's cells, or null to use the built-in rendering.
   * Actions columns have no `field`, so they never match one.
   *
   * Read straight off the QueryList, which is live — a consumer declaring its templates inside a
   * `@for`/`@if` is served with no subscription and nothing to invalidate. The scan is over the
   * TEMPLATED columns only (typically one or two), not over `cols`.
   */
  getCellTemplate(col: Column): TemplateRef<CellTemplateContext> | null {
    if (col.field == null) return null;
    return (
      this.host.cellTemplates?.find((directive) => directive.field === col.field)
        ?.template ?? null
    );
  }

  // Client side table: can do it with Id also, because we are never adding the new record in the
  // table at the same page.
  getFormArrayControlByIndex(
    formControlName: string,
    index: number,
  ): SpiderlyFormControl {
    if (this.host.getFormControl) {
      return this.host.getFormControl(
        formControlName,
        index,
        this.host.additionalIndexes,
      );
    } else {
      return null;
    }
  }

  getStyleForBodyColumn(col: Column<any>) {
    switch (col.filterType) {
      case 'numeric':
        return 'text-align: right;';
      default:
        return null;
    }
  }

  actionTrackByFn(index, item: Action) {
    return `${index}${item.field}`;
  }

  getClassForAction(action: Action): string {
    switch (action.field) {
      case 'Details':
        return 'pi pi-pencil';
      case 'Delete':
        return 'pi pi-trash';
      default:
        return `${action.icon} ${action.styleClass}`;
    }
  }

  getStyleForAction(action: Action): string {
    switch (action.field) {
      case 'Delete':
        return 'cursor: pointer; color: var(--p-button-danger-background);';
      default:
        return `cursor: pointer; ${action.style}`;
    }
  }
}
