import { Component, Input, OnInit } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { PasswordModule } from 'primeng/password';
import { TooltipModule } from 'primeng/tooltip';

@Component({
    selector: 'spiderly-password',
    templateUrl: './spiderly-password.component.html',
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        PasswordModule,
        TooltipModule,
        RequiredComponent
    ]
})
export class SpiderlyPasswordComponent extends BaseControl implements OnInit {
    @Input() showPasswordStrength: boolean = false;

    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        super.ngOnInit();
    }

}
