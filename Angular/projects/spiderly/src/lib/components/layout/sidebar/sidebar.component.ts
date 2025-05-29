import { Component, ElementRef, Input } from '@angular/core';
import { SidebarMenuComponent, SpiderlyMenuItem } from './sidebar-menu.component';

@Component({
    selector: 'sidebar',
    templateUrl: './sidebar.component.html',
    imports: [
        SidebarMenuComponent
    ]
})
export class AppSidebarComponent {
    @Input() menu: SpiderlyMenuItem[];

    constructor(public el: ElementRef) { 
    }

    ngOnInit(){
    }
}

