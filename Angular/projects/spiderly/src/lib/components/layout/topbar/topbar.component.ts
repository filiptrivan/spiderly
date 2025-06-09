import { ConfigBaseService } from '../../../services/config-base.service';
import { MegaMenu } from 'primeng/megamenu';
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MegaMenuItem } from 'primeng/api';
import { AvatarModule } from 'primeng/avatar';
import { ProfileAvatarComponent } from "../profile-avatar/profile-avatar.component";
import { TopBarMenuItemComponent } from "./topbar-menuitem.component";

@Component({
  selector: 'spiderly-topbar',
  templateUrl: './topbar.component.html',
  styleUrl: './topbar.component.scss',
  imports: [
    CommonModule,
    RouterModule,
    MegaMenu,
    AvatarModule,
    ProfileAvatarComponent,
    TopBarMenuItemComponent
]
})
export class TopBarComponent {
  @Input() menu: MegaMenuItem[];
  companyName = this.config.companyName;
  isSideMenuActive = false;

  constructor(
    private config: ConfigBaseService,
  ) {

  }

  ngOnInit() {

  }

}
