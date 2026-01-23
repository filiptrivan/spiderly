import { ConfigServiceBase } from '../../../services/config.service.base';
import { TranslocoService } from '@jsverse/transloco';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { Component, ElementRef, ViewChild } from '@angular/core';
import { filter } from 'rxjs';
import { LayoutServiceBase } from '../../../services/app-layout.service.base';
import { CommonModule } from '@angular/common';
import { AvatarModule } from 'primeng/avatar';
import { BadgeModule } from 'primeng/badge';
import { ProfileAvatarComponent } from '../profile-avatar/profile-avatar.component';

@Component({
  selector: 'spiderly-sidemenu-topbar',
  templateUrl: './sidemenu-topbar.component.html',
  imports: [
    CommonModule,
    RouterModule,
    AvatarModule,
    BadgeModule,
    ProfileAvatarComponent,
  ],
})
export class SideMenuTopBarComponent {
  companyName = this.config.companyName;
  @ViewChild('menubutton') menuButton!: ElementRef;

  constructor(
    public layoutService: LayoutServiceBase,
    protected router: Router,
    private config: ConfigServiceBase,
    private translocoService: TranslocoService,
  ) {}

  async ngOnInit() {
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        this.layoutService.state.profileDropdownSidebarVisible = false;
      });
  }
}
