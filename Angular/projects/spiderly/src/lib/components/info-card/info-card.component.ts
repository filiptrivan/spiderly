import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { SpiderlyPanelsModule } from "../spiderly-panels/spiderly-panels.module";

@Component({
    selector: 'info-card',
    templateUrl: './info-card.component.html',
    styleUrl: './info-card.component.scss',
    imports: [
        CommonModule,
        SpiderlyPanelsModule
    ]
})
export class InfoCardComponent {
    @Input() header = '';
    @Input() showSmallIcon = true;
    @Input() icon = 'pi pi-info-circle';
    @Input() textColor = '';
    
    constructor(
        protected formBuilder: FormBuilder,
        ) {

        }

    ngOnInit(){
    }

}