import { Component, Input, OnInit } from '@angular/core';
import { AutoCompleteCompleteEvent, AutoCompleteModule, AutoCompleteSelectEvent, AutoCompleteUnselectEvent } from 'primeng/autocomplete';
import { BaseAutocompleteControl } from '../base-autocomplete-control';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { TooltipModule } from 'primeng/tooltip';
import { InputGroupModule } from 'primeng/inputgroup';
import { ValidatorAbstractService } from '../../services/validator-abstract.service';
import { SpiderlyFormControl } from '../../components/spiderly-form-control/spiderly-form-control';
import { PrimengOption } from '../../entities/primeng-option';

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
    helperFormControl = new SpiderlyFormControl<PrimengOption>(null, {updateOn: 'change'});

    constructor(
        protected override translocoService: TranslocoService,
        private validatorService: ValidatorAbstractService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        super.ngOnInit();

        this.helperFormControl.label = this.control.label;
        this.validatorService.setValidator(this.helperFormControl, this.control.parentClassName);
        
        if (this.control.value != null)
            this.helperFormControl.setValue({label: this.displayName, code: this.control.value});

        if (this.emptyMessage == null) {
            this.emptyMessage = this.translocoService.translate('EmptyMessage');
        }
    }

    search(event: AutoCompleteCompleteEvent){
        this.onTextInput.next(event);
    }

    select(event: AutoCompleteSelectEvent){
        this.control.setValue(event.value.code);
    }

    clear(){
        this.control.setValue(null);
    }

    autocompleteMarkAsDirty(){
        this.dropdownMarkAsDirty();
        this.helperFormControl.markAsDirty();
    }

}
