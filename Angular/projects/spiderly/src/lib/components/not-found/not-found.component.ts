import { Component } from '@angular/core';
import { ConfigBaseService } from '../../services/config-base.service';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
    selector: 'not-found',
    templateUrl: './not-found.component.html',
    imports: [
        RouterModule,
        ButtonModule,
    ]
})
export class NotFoundComponent { 
    companyName = this.config.companyName;
    logoPath: string = this.config.logoPath;

    constructor(
        private config: ConfigBaseService
    ) { 

    }
}