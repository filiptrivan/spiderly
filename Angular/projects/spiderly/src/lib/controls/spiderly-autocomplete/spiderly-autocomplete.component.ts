import { CommonModule } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AutoCompleteCompleteEvent, AutoCompleteModule, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { TooltipModule } from 'primeng/tooltip';
import { RequiredComponent } from '../../components/required/required.component';
import { ValidatorAbstractService } from '../../services/validator-abstract.service';
import { BaseAutocompleteControl } from '../base-autocomplete-control';
import { SpiderlyFormControl } from '../../components/spiderly-form-control/spiderly-form-control';
import { Namebook } from '../../entities/namebook';

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
    helperFormControl = new SpiderlyFormControl<Namebook>(null, {updateOn: 'change'});

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
            this.helperFormControl.setValue({displayName: this.displayName, id: this.control.value});

        if (this.emptyMessage == null) {
            this.emptyMessage = this.translocoService.translate('EmptyMessage');
        }
    }

    search(event: AutoCompleteCompleteEvent){
        this.onTextInput.next(event);
    }

    select(event: AutoCompleteSelectEvent){
        const selectedOption: Namebook = event.value;
        this.control.setValue(selectedOption.id);
        this.helperFormControl.setValue({displayName: selectedOption.displayName, id: selectedOption.id});
    }

    clear(){
        this.control.setValue(null);
        this.helperFormControl.setValue(null);
    }

    autocompleteMarkAsDirty(){
        this.dropdownMarkAsDirty();
        this.helperFormControl.markAsDirty();
    }

}
