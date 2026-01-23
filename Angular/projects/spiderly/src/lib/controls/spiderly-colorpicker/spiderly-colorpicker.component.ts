import { Component, Input, OnInit } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { ColorPickerModule } from 'primeng/colorpicker';
import { TooltipModule } from 'primeng/tooltip';
import { InputTextModule } from 'primeng/inputtext';

@Component({
  selector: 'spiderly-colorpicker',
  templateUrl: './spiderly-colorpicker.component.html',
  styles: [],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    ColorPickerModule,
    InputTextModule,
    TooltipModule,
    RequiredComponent,
  ],
})
export class SpiderlyColorPickerComponent
  extends BaseControl
  implements OnInit
{
  @Input() showInputTextField: boolean = true;

  constructor(protected override translocoService: TranslocoService) {
    super(translocoService);
  }

  override ngOnInit() {
    this.control.valueChanges.subscribe((value) => {
      this.control.setValue(value, { emitEvent: false }); // Preventing infinite loop
    });

    if (this.control.value == null)
      this.placeholder = this.translocoService.translate(
        'ColorPickerPlaceholder',
      );

    super.ngOnInit();
  }
}
