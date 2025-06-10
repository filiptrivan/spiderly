import { Component, ElementRef, Input, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { AvatarModule } from 'primeng/avatar';
import { LayoutBaseService } from '../../../services/app-layout-base.service';
import { User } from '../../../entities/security-entities';
import { filter, Subscription } from 'rxjs';
import { AuthBaseService } from '../../../services/auth-base.service';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { BadgeModule } from 'primeng/badge';
import { SpiderlyButtonComponent } from "../../spiderly-buttons/spiderly-button/spiderly-button.component";
import { ConfigBaseService } from '../../../services/config-base.service';

interface ProfileAvatarModalMenuItem {
  label?: string;
  icon?: string;
  showSeparator?: boolean;
  onClick?: () => void;
  showNotificationBadge?: boolean;
}

@Component({
  selector: 'spiderly-profile-avatar',
  templateUrl: './profile-avatar.component.html',
  imports: [
    CommonModule,
    RouterModule,
    AvatarModule,
    BadgeModule,
    SpiderlyButtonComponent,
    TranslocoDirective,
]
})
export class ProfileAvatarComponent {
  @Input() isSideMenuLayout = true;

  private initTopBarSubscription: Subscription | null = null;

  currentUser: User;
  userProfilePath: string;
  unreadNotificationsCount: number;
  menuItems: ProfileAvatarModalMenuItem[] = [];
  avatarLabel: string;
  showProfileIcon = false;

  notificationMenuItem: ProfileAvatarModalMenuItem =
  {
    label: this.translocoService.translate('Notifications'),
    icon: 'pi-bell',
    showNotificationBadge: true,
    onClick: () => {
      this.router.navigateByUrl(`/notifications`);
    },
  };

  @ViewChild('topbarmenu') menu!: ElementRef;

  @ViewChild('topbarprofiledropdownmenubutton') topbarProfileDropdownMenuButton!: ElementRef;

  constructor(
    public layoutService: LayoutBaseService, 
    private authService: AuthBaseService, 
    protected router: Router,
    private translocoService: TranslocoService,
    public config: ConfigBaseService,
  ) { 
  }

  async ngOnInit(){
    this.menuItems = [
      {
        label: this.translocoService.translate('YourProfile'),
        icon: 'pi-user',
        showSeparator: true,
        onClick: () => {
          this.routeToUserPage();
        }
      },
      this.notificationMenuItem,
      {
        label: this.translocoService.translate('Logout'),
        icon: 'pi-sign-out',
        showSeparator: true,
        onClick: () => {
          this.authService.logout();
        }
      }
    ];

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        this.layoutService.state.profileDropdownSidebarVisible = false;
      });

    this.onAfterNgOnInit();
  }

  onAfterNgOnInit = () => {
    this.initTopBarSubscription = this.layoutService.initTopBarData().subscribe(initTopBarData => {
      this.userProfilePath = initTopBarData.userProfilePath;
      this.unreadNotificationsCount = initTopBarData.unreadNotificationsCount;
      this.notificationMenuItem.showNotificationBadge = initTopBarData.unreadNotificationsCount > 0;
      this.showProfileIcon = initTopBarData.showProfileIcon;
      this.currentUser = initTopBarData.currentUser;
      this.avatarLabel = initTopBarData.currentUser?.email.charAt(0).toLocaleUpperCase();
    });
  }

  onDocumentClick(event: any) {
    if (
      !this.menu.nativeElement.contains(event.target) 
    ) {
      if (this.layoutService.state.profileDropdownSidebarVisible == true) {
        this.layoutService.state.profileDropdownSidebarVisible = false;
      }
    }
  }

  routeToUserPage(){
    this.router.navigateByUrl(this.userProfilePath);
  }

  ngOnDestroy(): void {
    if (this.initTopBarSubscription) {
      this.initTopBarSubscription.unsubscribe();
    }
  }

}
