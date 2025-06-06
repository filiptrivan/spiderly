import { ConfirmationService } from 'primeng/api';
import { Component } from "@angular/core";
import { DynamicDialogConfig, DynamicDialogRef } from "primeng/dynamicdialog";
import { TranslocoDirective } from '@jsverse/transloco';
import { SpiderlyButtonComponent } from '../spiderly-buttons/spiderly-button/spiderly-button.component';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

@Component({
    selector: 'spiderly-delete-confirmation',
    templateUrl: './spiderly-delete-confirmation.component.html',
    styles: [],
    imports: [
        SpiderlyButtonComponent,
        TranslocoDirective,
        ConfirmDialogModule,
    ],
    providers: [
        ConfirmationService
    ]
})
export class SpiderlyDeleteConfirmationComponent {

  constructor(public ref: DynamicDialogRef, public config: DynamicDialogConfig) {}

  accept(){
    this.config.data.deleteItemFromTableObservableMethod(this.config.data.id).subscribe({
      next: () => {
        this.ref.close(true); // deleted succesfully
      },
      error: () => {
        this.ref.close(false); // not deleted succesfully
      },
    });
  }

  reject(){
    this.ref.close(false);
  }
}