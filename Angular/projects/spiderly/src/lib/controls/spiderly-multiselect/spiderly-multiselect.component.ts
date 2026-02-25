import { Component, OnInit } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RequiredComponent } from '../../components/required/required.component';
import { BaseDropdownControl } from '../base-dropdown-control';
import { TranslocoService } from '@jsverse/transloco';
import { MultiSelectModule } from 'primeng/multiselect';

@Component({
  selector: 'spiderly-multiselect',
  templateUrl: './spiderly-multiselect.component.html',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MultiSelectModule,
    RequiredComponent,
  ],
})
export class SpiderlyMultiSelectComponent
  extends BaseDropdownControl
  implements OnInit
{
  constructor(protected override translocoService: TranslocoService) {
    super(translocoService);
  }

  override ngOnInit() {
    super.ngOnInit();
  }
}
