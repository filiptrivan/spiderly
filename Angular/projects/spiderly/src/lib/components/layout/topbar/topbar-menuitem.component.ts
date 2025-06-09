import { AuthBaseService } from './../../../services/auth-base.service';
import { ConfigBaseService } from '../../../services/config-base.service';
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AvatarModule } from 'primeng/avatar';
import { Subscription } from 'rxjs';

@Component({
  selector: 'spiderly-topbar-menuitem',
  templateUrl: './topbar-menuitem.component.html',
  imports: [
    CommonModule,
    RouterModule,
    AvatarModule,
]
})
export class TopBarMenuItemComponent {
  @Input() item: any;

  private permissionSubscription: Subscription | null = null;

  constructor(
    private config: ConfigBaseService,
    private authService: AuthBaseService,
  ) {

  }

  ngOnInit() {
    console.log(this.item)
    this.permissionSubscription = this.authService.currentUserPermissionCodes$.subscribe((currentUserPermissionCodes: string[]) => {
      if (this.item && typeof this.item.hasPermission === 'function') {
        this.item.visible = this.item.hasPermission(currentUserPermissionCodes);
      }
    });
  }

}
