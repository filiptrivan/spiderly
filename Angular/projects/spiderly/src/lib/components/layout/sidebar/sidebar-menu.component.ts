import { TranslocoService } from '@jsverse/transloco';
import { Input, OnInit } from '@angular/core';
import { Component } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { LayoutServiceBase } from '../../../services/app-layout.service.base';
import { AuthServiceBase } from '../../../services/auth.service.base';
import { ConfigServiceBase } from '../../../services/config.service.base';
import { MenuitemComponent } from './menuitem.component';
import { CommonModule } from '@angular/common';

export interface SpiderlyMenuItem extends MenuItem {
    hasPermission?: (permissionCodes: string[]) => boolean;
    showPartnerDialog?: boolean; 
}

@Component({
    selector: 'sidebar-menu', // FT: Don't change selector to 'menu', because other style will apply to it
    templateUrl: './sidebar-menu.component.html',
    imports: [
        CommonModule,
        MenuitemComponent
    ]
})
export class SidebarMenuComponent implements OnInit {
    @Input() menu: SpiderlyMenuItem[];

    constructor(
        public layoutService: LayoutServiceBase, 
        private authService: AuthServiceBase,
        private translocoService: TranslocoService,
        private config: ConfigServiceBase
    ) {
        
    }

    ngOnInit() {
    }


    ngOnDestroy(): void {

    }

}
