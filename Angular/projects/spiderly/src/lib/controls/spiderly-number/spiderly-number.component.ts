import { Component, Input, OnInit } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { InputNumberModule } from 'primeng/inputnumber';
import { TooltipModule } from 'primeng/tooltip';

@Component({
    selector: 'spiderly-number',
    templateUrl: './spiderly-number.component.html',
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        InputNumberModule,
        TooltipModule,
        RequiredComponent
    ]
})
export class SpiderlyNumberComponent extends BaseControl implements OnInit {
    @Input() prefix: string;
    @Input() showButtons: boolean = true;
    @Input() decimal: boolean;
    @Input() maxFractionDigits: number = 0;

    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        super.ngOnInit();
    }
    
}
