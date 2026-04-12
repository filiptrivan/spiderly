import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { AvatarModule } from 'primeng/avatar';
import { LayoutServiceBase } from '../../../services/app-layout.service.base';
import { UserBase } from '../../../entities/security-entities';
import { filter, Subscription } from 'rxjs';
import { AuthServiceBase } from '../../../services/auth.service.base';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { SpiderlyButtonComponent } from '../../spiderly-buttons/spiderly-button/spiderly-button.component';
import { ConfigServiceBase } from '../../../services/config.service.base';

@Component({
  selector: 'spiderly-profile-avatar',
  templateUrl: './profile-avatar.component.html',
  styleUrl: './profile-avatar.component.scss',
  imports: [
    CommonModule,
    RouterModule,
    AvatarModule,
    SpiderlyButtonComponent,
    TranslocoDirective,
  ],
})
export class ProfileAvatarComponent {
  @Input() isSideMenuLayout = true;
  @Input() routeOnLargeProfileAvatarClick = true;
  @Input() showLoginButton = true;
  @Input() routeToLoginPage = true;
  @Input() loginButtonOutlined = false;
  @Input() loginButtonSeverity:
    | 'success'
    | 'info'
    | 'warn'
    | 'danger'
    | 'help'
    | 'primary'
    | 'secondary'
    | 'contrast'
    | null
    | undefined = 'primary';
  @Input() loginButtonSize: 'small' | 'large' | undefined;
  @Output() onLoginButtonClick = new EventEmitter();
  @Input() menuItems: ProfileAvatarModalMenuItem[] = [];

  private initTopBarSubscription: Subscription | null = null;

  currentUser: UserBase;
  userProfilePath: string;
  avatarLabel: string;
  showProfileIcon = false;

  @ViewChild('topbarmenu') menu!: ElementRef;

  @ViewChild('topbarprofiledropdownmenubutton')
  topbarProfileDropdownMenuButton!: ElementRef;

  constructor(
    public layoutService: LayoutServiceBase,
    private authService: AuthServiceBase,
    protected router: Router,
    private translocoService: TranslocoService,
    public config: ConfigServiceBase,
  ) {}

  async ngOnInit() {
    if (this.menuItems.length === 0) {
      this.menuItems = [
        {
          label: this.translocoService.translate('YourProfile'),
          icon: 'pi-user',
          showSeparator: true,
          onClick: () => {
            this.routeToUserPage();
          },
        },
        {
          label: this.translocoService.translate('Logout'),
          icon: 'pi-sign-out',
          showSeparator: true,
          onClick: () => {
            this.authService.logout();
          },
        },
      ];
    }

    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        this.layoutService.state.profileDropdownSidebarVisible = false;
      });

    this.onAfterNgOnInit();
  }

  onAfterNgOnInit = () => {
    this.initTopBarSubscription = this.layoutService
      .initTopBarData()
      .subscribe((initTopBarData) => {
        this.userProfilePath = initTopBarData.userProfilePath;
        this.showProfileIcon = initTopBarData.showProfileIcon;
        this.currentUser = initTopBarData.currentUser;
        this.avatarLabel = initTopBarData.currentUser?.email
          .charAt(0)
          .toLocaleUpperCase();
      });
  };

  onDocumentClick(event: any) {
    if (!this.menu.nativeElement.contains(event.target)) {
      if (this.layoutService.state.profileDropdownSidebarVisible == true) {
        this.layoutService.state.profileDropdownSidebarVisible = false;
      }
    }
  }

  routeToUserPage() {
    if (this.routeOnLargeProfileAvatarClick) {
      this.router.navigateByUrl(this.userProfilePath);
    }
  }

  loginButtonClick() {
    if (this.routeToLoginPage) {
      this.router.navigateByUrl(this.config.loginSlug);
    }

    this.onLoginButtonClick.next(null);
  }

  ngOnDestroy(): void {
    if (this.initTopBarSubscription) {
      this.initTopBarSubscription.unsubscribe();
    }
  }
}

export interface ProfileAvatarModalMenuItem {
  label?: string;
  icon?: string;
  showSeparator?: boolean;
  onClick?: () => void;
}
