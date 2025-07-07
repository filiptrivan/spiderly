import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { MenuItem } from 'primeng/api';
import { SpiderlyPanelsModule } from "../spiderly-panels/spiderly-panels.module";

@Component({
    selector: 'index-card',
    templateUrl: './index-card.component.html',
    imports: [
        CommonModule,
        SpiderlyPanelsModule
    ]
})
export class IndexCardComponent {
    @Input() last: boolean;
    @Input() index: number;
    @Input() header: string = '';
    @Input() description: string;
    @Input() showRemoveIcon: boolean;
    @Input() showCrudMenu: boolean = true;

    @Input() crudMenu: MenuItem[];

    @Output() onMenuIconClick = new EventEmitter<number>();
    @Output() onRemoveIconClick = new EventEmitter<null>();
    
    constructor(
        protected formBuilder: FormBuilder,
    ) {

    }

    ngOnInit(){
    }

    menuIconClick(index: number){
        this.onMenuIconClick.next(index);
    }

    removeIconClick(){
        this.onRemoveIconClick.next(null);
    }

}