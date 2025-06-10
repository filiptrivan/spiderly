import { ConfigBaseService } from '../../../services/config-base.service';
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { AvatarModule } from 'primeng/avatar';
import { ProfileAvatarComponent } from "../profile-avatar/profile-avatar.component";
import { MenubarModule } from "primeng/menubar";

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
  @Input() menu: MenuItem[];
  companyName = this.config.companyName;
  logoPath = this.config.logoPath;
  isSideMenuActive = false;

  constructor(
    private config: ConfigBaseService,
  ) {

  }

  ngOnInit() {

  }

}
