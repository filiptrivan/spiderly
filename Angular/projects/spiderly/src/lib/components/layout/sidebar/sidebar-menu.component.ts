import { TranslocoService } from '@jsverse/transloco';
import { Input, OnInit } from '@angular/core';
import { Component } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { LayoutBaseService } from '../../../services/app-layout-base.service';
import { AuthBaseService } from '../../../services/auth-base.service';
import { ConfigBaseService } from '../../../services/config-base.service';
import { MenuitemComponent } from './menuitem.component';
import { CommonModule } from '@angular/common';
import { PrimeNGIcon } from '../../../entities/primeng-icon.enum';


export interface SpiderlyMenuItem extends Omit<MenuItem, 'icon'> {

export interface SpiderlyMenuItem extends MenuItem {
    hasPermission?: (permissionCodes: string[]) => boolean;
    showPartnerDialog?: boolean;
    icon?: PrimeNGIcon;
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
        public layoutService: LayoutBaseService, 
        private authService: AuthBaseService,
        private translocoService: TranslocoService,
        private config: ConfigBaseService
    ) {
        
    }

    ngOnInit() {
    }


    ngOnDestroy(): void {

    }

}
