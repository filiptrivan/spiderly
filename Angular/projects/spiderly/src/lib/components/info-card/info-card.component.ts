import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { SpiderlyPanelsModule } from "../spiderly-panels/spiderly-panels.module";
import { PrimengModule } from '../../modules/primeng.module';

@Component({
    selector: 'info-card',
    templateUrl: './info-card.component.html',
    imports: [
        CommonModule,
        PrimengModule,
        SpiderlyPanelsModule
    ]
})
export class InfoCardComponent {
    @Input() public header: string = '';
    @Input() public description: string;
    
    constructor(
        protected formBuilder: FormBuilder,
        ) {

        }

    ngOnInit(){
    }

}