import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';

@Component({
    selector: 'spiderly-textbox',
    templateUrl: './spiderly-textbox.component.html',
    styleUrl: '../base-controls.scss',
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        InputTextModule,
        TooltipModule,
        RequiredComponent
    ]
})
export class SpiderlyTextboxComponent extends BaseControl implements OnInit {
    @Input() showButton: boolean = false;
    @Input() buttonIcon: string;
    @Output() onButtonClick = new EventEmitter();
    
    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        super.ngOnInit();
    }

    
    buttonClick() {
        this.onButtonClick.next(null);
    }
    
}
