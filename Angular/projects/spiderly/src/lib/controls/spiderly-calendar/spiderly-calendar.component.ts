import { Component, Input, OnInit } from '@angular/core';
import { BaseControl } from '../base-control';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { TranslocoService } from '@jsverse/transloco';
import { DatePickerModule } from 'primeng/datepicker';
import { TooltipModule } from 'primeng/tooltip';

@Component({
    selector: 'spiderly-calendar',
    templateUrl: './spiderly-calendar.component.html',
    styles: [],
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        DatePickerModule,
        TooltipModule,
        RequiredComponent
    ]
})
export class SpiderlyCalendarComponent extends BaseControl implements OnInit {
    @Input() showTime: boolean = false;

    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        super.ngOnInit();
    }

    setDate(event:Date) { 
        
    }
}
