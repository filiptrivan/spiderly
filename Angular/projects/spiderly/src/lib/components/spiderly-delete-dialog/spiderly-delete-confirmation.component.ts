import { ConfirmationService } from 'primeng/api';
import { Component } from '@angular/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { TranslocoDirective } from '@jsverse/transloco';
import { SpiderlyButtonComponent } from '../spiderly-buttons/spiderly-button/spiderly-button.component';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { Observable } from 'rxjs';

export type DeleteConfirmationData =
  | {
      message: string;
      deleteItemFromTableObservableMethod: (id: number) => Observable<any>;
      id: number;
    }
  | {
      message: string;
      deleteListFromTableObservableMethod: (ids: number[]) => Observable<any>;
      ids: number[];
    };

@Component({
  selector: 'spiderly-delete-confirmation',
  templateUrl: './spiderly-delete-confirmation.component.html',
  styles: [],
  imports: [SpiderlyButtonComponent, TranslocoDirective, ConfirmDialogModule],
  providers: [ConfirmationService],
})
export class SpiderlyDeleteConfirmationComponent {
  constructor(
    public ref: DynamicDialogRef,
    public config: DynamicDialogConfig<DeleteConfirmationData>,
  ) {}

  accept() {
    const data = this.config.data;
    const observable =
      'deleteListFromTableObservableMethod' in data
        ? data.deleteListFromTableObservableMethod(data.ids)
        : data.deleteItemFromTableObservableMethod(data.id);

    observable.subscribe({
      next: () => {
        this.ref.close(true); // deleted succesfully
      },
      error: () => {
        this.ref.close(false); // not deleted succesfully
      },
    });
  }

  reject() {
    this.ref.close(false);
  }
}
