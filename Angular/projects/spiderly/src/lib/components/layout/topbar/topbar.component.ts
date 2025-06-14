import { ConfigBaseService } from '../../../services/config-base.service';
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AvatarModule } from 'primeng/avatar';
import { ProfileAvatarComponent } from "../profile-avatar/profile-avatar.component";
import { MenubarModule } from "primeng/menubar";
import { SpiderlyMenuItem } from '../sidebar/sidebar-menu.component';
import { AuthBaseService } from '../../../services/auth-base.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'spiderly-topbar',
  templateUrl: './topbar.component.html',
  styleUrl: './topbar.component.scss',
  imports: [
    CommonModule,
    RouterModule,
    MenubarModule,
    AvatarModule,
    ProfileAvatarComponent,
]
})
export class TopBarComponent {
  @Input() menu: SpiderlyMenuItem[] = [];
  companyName = this.config.companyName;
  logoPath = this.config.logoPath;

  private permissionSubscription: Subscription | null = null;

  constructor(
    private authService: AuthBaseService,
    private config: ConfigBaseService,
  ) {

  }

  ngOnInit() {
    this.permissionSubscription = this.authService.currentUserPermissionCodes$.subscribe((currentUserPermissionCodes: string[]) => {
      this.hideMenuItemsBasedOnPermissions(this.menu, currentUserPermissionCodes);
    });
  }

  hideMenuItemsBasedOnPermissions = (menu: SpiderlyMenuItem[], currentUserPermissionCodes: string[]) => {
    menu.forEach(menuItem => {
      if (menuItem.items) {
        this.hideMenuItemsBasedOnPermissions(menuItem.items, currentUserPermissionCodes)
      }
      if (typeof menuItem.hasPermission === 'function') {
        menuItem.visible = menuItem.hasPermission(currentUserPermissionCodes);
      }
    });
  }

  ngOnDestroy() {
      if (this.permissionSubscription) {
          this.permissionSubscription.unsubscribe();
      }
  }

}
