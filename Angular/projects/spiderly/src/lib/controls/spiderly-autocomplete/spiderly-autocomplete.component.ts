import { Component, Input, OnInit } from '@angular/core';
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { BaseAutocompleteControl } from '../base-autocomplete-control';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { TooltipModule } from 'primeng/tooltip';
import { InputGroupModule } from 'primeng/inputgroup';

@Component({
    selector: 'spiderly-autocomplete',
    templateUrl: './spiderly-autocomplete.component.html',
    imports: [
        ReactiveFormsModule,
        FormsModule,
        AutoCompleteModule,
        InputGroupAddonModule,
        InputGroupModule,
        TooltipModule,
        CommonModule,
        RequiredComponent,
        TranslocoDirective,
    ]
})
export class SpiderlyAutocompleteComponent extends BaseAutocompleteControl implements OnInit {
    @Input() appendTo: any = 'body';
    @Input() showClear: boolean = true;
    @Input() emptyMessage: string;
    @Input() displayName: string; // Added because when we initialize the object options are null

    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        super.ngOnInit();

        if (this.options == null && this.control.value != null) {
            this.options = [
                { label: this.displayName, code: this.control.value }
            ];
        }

        if (this.emptyMessage == null) {
            this.emptyMessage = this.translocoService.translate('EmptyMessage');
        }
    }

    search(event: AutoCompleteCompleteEvent){
        this.onTextInput.next(event);
    }

    select(event){
    }

}
