import { Component, ElementRef, Input, OnDestroy, Renderer2, ViewChild } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { AppSidebarComponent } from './sidebar/sidebar.component';
import { SideMenuTopBarComponent } from './sidemenu-topbar/sidemenu-topbar.component';
import { LayoutBaseService } from '../../services/app-layout-base.service';
import { SpiderlyMenuItem } from './sidebar/sidebar-menu.component';
import { CommonModule } from '@angular/common';
import { FooterComponent } from '../footer/footer.component';
import { TopBarComponent } from './topbar/topbar.component';

@Component({
    selector: 'spiderly-layout',
    templateUrl: './layout.component.html',
    imports: [
    CommonModule,
    RouterModule,
    AppSidebarComponent,
    FooterComponent,
    SideMenuTopBarComponent,
    TopBarComponent,
]
})
export class SpiderlyLayoutComponent implements OnDestroy {
    @Input() menu: SpiderlyMenuItem[] = [];
    sideMenu: SpiderlyMenuItem[] = [];
    topMenu: SpiderlyMenuItem[] = [];
    @Input() isSideMenuLayout: boolean = true;
    /**
     * Determines whether to show background color on hover
     * for root top menu items. Defaults to `false`.
     */
    @Input() showHoverBgOnRootTopMenuItems: boolean = false;

    /**
     * default max-width for the main content area.
     */
  
    @Input() maxWidth: string = '1100px';
  
    overlayMenuOpenSubscription: Subscription;

    menuOutsideClickListener: any;

    profileMenuOutsideClickListener: any;

    @ViewChild(AppSidebarComponent) appSidebar!: AppSidebarComponent;

    @ViewChild(SideMenuTopBarComponent) sidemenuTopbar!: SideMenuTopBarComponent;

    @ViewChild('topbarmenu') topbarmenu!: ElementRef;

    constructor(
        protected layoutService: LayoutBaseService, 
        protected renderer: Renderer2, 
        protected router: Router,
    ) {
        this.overlayMenuOpenSubscription = this.layoutService.overlayOpen$.subscribe(() => {
            if (!this.menuOutsideClickListener) {
                this.menuOutsideClickListener = this.renderer.listen('document', 'click', event => {
                    const isOutsideClicked = !(
                        this.appSidebar?.el.nativeElement.isSameNode(event.target) || 
                        this.appSidebar?.el.nativeElement.contains(event.target) ||
                        this.sidemenuTopbar?.menuButton?.nativeElement.isSameNode(event.target) || 
                        this.sidemenuTopbar?.menuButton?.nativeElement.contains(event.target) ||
                        (event.target.closest('.p-autocomplete-items')) ||
                        (event.target.closest('.p-autocomplete-clear-icon'))
                    );
                    
                    if (isOutsideClicked) {
                        this.hideMenu();
                    }
                });
            }

            if (!this.profileMenuOutsideClickListener) {
                this.profileMenuOutsideClickListener = this.renderer.listen('document', 'click', event => {
                    const isOutsideClicked = !(
                        this.topbarmenu?.nativeElement.isSameNode(event.target) || 
                        this.topbarmenu?.nativeElement.contains(event.target)
                    );

                    if (isOutsideClicked) {
                        this.hideProfileMenu();
                    }
                });
            }

            if (this.layoutService.state.staticMenuMobileActive) {
                this.blockBodyScroll();
            }
        });

        this.router.events.pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => {
                this.hideMenu();
                this.hideProfileMenu();
            });
    }

    ngOnInit() {
        if (this.isSideMenuLayout) {
            this.sideMenu = [...this.menu];
        }
        else{
            this.topMenu = [...this.menu[0].items];
        }
    }

    hideMenu() {
        this.layoutService.state.overlayMenuActive = false;
        this.layoutService.state.staticMenuMobileActive = false;
        this.layoutService.state.menuHoverActive = false;
        if (this.menuOutsideClickListener) {
            this.menuOutsideClickListener();
            this.menuOutsideClickListener = null;
        }
        this.unblockBodyScroll();
    }

    hideProfileMenu() {
        this.layoutService.state.profileSidebarVisible = false;
        if (this.profileMenuOutsideClickListener) {
            this.profileMenuOutsideClickListener();
            this.profileMenuOutsideClickListener = null;
        }
    }

    blockBodyScroll(): void {
        if (document.body.classList) {
            document.body.classList.add('blocked-scroll');
        }
        else {
            document.body.className += ' blocked-scroll';
        }
    }

    unblockBodyScroll(): void {
        if (document.body.classList) {
            document.body.classList.remove('blocked-scroll');
        }
        else {
            document.body.className = document.body.className.replace(new RegExp('(^|\\b)' +
                'blocked-scroll'.split(' ').join('|') + '(\\b|$)', 'gi'), ' ');
        }
    }

    get containerClass() {
        return {
            'layout-theme-light': this.layoutService.layoutConfig.colorScheme === 'light',
            'layout-theme-dark': this.layoutService.layoutConfig.colorScheme === 'dark',
            'layout-overlay': this.layoutService.layoutConfig.menuMode === 'overlay',
            'layout-static': this.layoutService.layoutConfig.menuMode === 'static',
            'layout-static-inactive': this.layoutService.state.staticMenuDesktopInactive && this.layoutService.layoutConfig.menuMode === 'static',
            'layout-overlay-active': this.layoutService.state.overlayMenuActive,
            'layout-mobile-active': this.layoutService.state.staticMenuMobileActive,
            'p-input-filled': this.layoutService.layoutConfig.inputStyle === 'filled',
            'p-ripple-disabled': !this.layoutService.layoutConfig.ripple
        }
    }

    ngOnDestroy() {
        if (this.overlayMenuOpenSubscription) {
            this.overlayMenuOpenSubscription.unsubscribe();
        }

        if (this.menuOutsideClickListener) {
            this.menuOutsideClickListener();
        }

        this.onAfterNgDestroy();
    }

    onAfterNgDestroy = () => {}
    
}

