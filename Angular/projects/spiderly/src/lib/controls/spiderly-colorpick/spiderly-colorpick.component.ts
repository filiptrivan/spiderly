import { Component, OnInit } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { ColorPickerModule } from 'primeng/colorpicker';
import { TooltipModule } from 'primeng/tooltip';

@Component({
    selector: 'spiderly-colorpick',
    templateUrl: './spiderly-colorpick.component.html',
    styles: [],
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        ColorPickerModule,
        TooltipModule,
        RequiredComponent
    ]
})
export class SpiderlyColorpickComponent extends BaseControl implements OnInit {

    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        this.control.valueChanges.subscribe((value) => {
            this.control.setValue(value, { emitEvent: false }); // Preventing infinite loop
        });

        if (this.control.value == null)
            this.placeholder = this.translocoService.translate('SelectAColor');

        super.ngOnInit();
    }

}
