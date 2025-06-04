import { Component, OnInit } from '@angular/core';
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { BaseAutocompleteControl } from '../base-autocomplete-control';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TooltipModule } from 'primeng/tooltip';

@Component({
    selector: 'spiderly-multiautocomplete',
    templateUrl: './spiderly-multiautocomplete.component.html',
    imports: [
        ReactiveFormsModule,
        FormsModule,
        AutoCompleteModule,
        TooltipModule,
        CommonModule,
        RequiredComponent,
        TranslocoDirective,
    ]
})
export class SpiderlyMultiAutocompleteComponent extends BaseAutocompleteControl implements OnInit {
    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        super.ngOnInit();
    }

    search(event: AutoCompleteCompleteEvent){
        this.onTextInput.next(event);
    }

}
