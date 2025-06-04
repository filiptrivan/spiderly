import { Component, OnInit } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';

@Component({
    selector: 'spiderly-textarea',
    templateUrl: './spiderly-textarea.component.html',
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        TextareaModule,
        TooltipModule,
        RequiredComponent
    ]
})
export class SpiderlyTextareaComponent extends BaseControl implements OnInit {

    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        super.ngOnInit();
    }
}
