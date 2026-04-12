import { AuthServiceBase } from './auth.service.base';
import { ApiSecurityService } from './api.service.security';
import { Injectable, OnDestroy } from '@angular/core';
import {
  map,
  Observable,
  of,
  Subject,
  Subscription,
} from 'rxjs';
import { InitTopBarData } from '../entities/init-top-bar-data';
import { ConfigServiceBase } from './config.service.base';
import { AutoCompleteCompleteEvent } from 'primeng/autocomplete';
import { Namebook } from '../entities/namebook';

export interface AppConfig {
  inputStyle: string;
  colorScheme: string;
  theme: string;
  ripple: boolean;
  menuMode: string;
  scale: number;
  color: string;
}

interface LayoutState {
  staticMenuDesktopInactive: boolean;
  overlayMenuActive: boolean;
  profileSidebarVisible: boolean;
  profileDropdownSidebarVisible: boolean;
  configSidebarVisible: boolean;
  staticMenuMobileActive: boolean;
  menuHoverActive: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class LayoutServiceBase implements OnDestroy {
  userSubscription: Subscription;

  layoutConfig: AppConfig = {
    ripple: false,
    inputStyle: 'outlined',
    menuMode: 'static',
    colorScheme: 'light',
    theme: 'lara-light-indigo',
    scale: 14,
    color: `var(--p-primary-color)`,
  };

  state: LayoutState = {
    staticMenuDesktopInactive: false,
    overlayMenuActive: false,
    profileSidebarVisible: false,
    profileDropdownSidebarVisible: false,
    configSidebarVisible: false,
    staticMenuMobileActive: false,
    menuHoverActive: false,
  };

  private configUpdate = new Subject<AppConfig>();

  private overlayOpen = new Subject<any>();

  configUpdate$ = this.configUpdate.asObservable();

  overlayOpen$ = this.overlayOpen.asObservable();

  constructor(
    protected apiService: ApiSecurityService,
    protected config: ConfigServiceBase,
    protected authService: AuthServiceBase,
  ) {}

  onMenuToggle() {
    if (this.isOverlay()) {
      this.state.overlayMenuActive = !this.state.overlayMenuActive;
      if (this.state.overlayMenuActive) {
        this.overlayOpen.next(null);
      }
    }

    if (this.isDesktop()) {
      this.state.staticMenuDesktopInactive =
        !this.state.staticMenuDesktopInactive;
    } else {
      this.state.staticMenuMobileActive = !this.state.staticMenuMobileActive;

      if (this.state.staticMenuMobileActive) {
        this.overlayOpen.next(null);
      }
    }
  }

  showProfileSidebar() {
    this.state.profileSidebarVisible = !this.state.profileSidebarVisible;
    if (this.state.profileSidebarVisible) {
      this.overlayOpen.next(null);
    }
  }

  showProfileDropdownSidebar() {
    this.state.profileDropdownSidebarVisible =
      !this.state.profileDropdownSidebarVisible;
    if (this.state.profileDropdownSidebarVisible) {
      this.overlayOpen.next(null);
    }
  }

  showConfigSidebar() {
    this.state.configSidebarVisible = true;
  }

  isOverlay() {
    return this.layoutConfig.menuMode === 'overlay';
  }

  isDesktop() {
    return window.innerWidth > 991;
  }

  isMobile() {
    return !this.isDesktop();
  }

  onConfigUpdate() {
    this.configUpdate.next(this.layoutConfig);
  }

  //#region Top Bar

  initTopBarData = (): Observable<InitTopBarData> => {
    return this.authService.user$.pipe(
      map((currentUser) => {
        return new InitTopBarData({
          companyName: this.config.companyName,
          userProfilePath: `/administration/users/${currentUser?.id}`,
          showProfileIcon: true,
          currentUser: currentUser,
        });
      }),
    );
  };

  //#endregion

  //#region Side Bar

  searchPartners = (
    event: AutoCompleteCompleteEvent,
  ): Observable<Namebook[]> => {
    return of();
  };

  partnersAutocompleteButtonClick = (selectedPartner: Namebook) => {};

  //#endregion

  ngOnDestroy(): void {
    if (this.userSubscription) {
      this.userSubscription.unsubscribe();
    }
  }
}
