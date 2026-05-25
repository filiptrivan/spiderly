import { CommonModule } from '@angular/common';
import { Component, EventEmitter, OnDestroy, OnInit, Output } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { AuthServiceBase } from '../../../services/auth.service.base';
import { ConfigServiceBase } from '../../../services/config.service.base';
import { ApiSecurityService } from '../../../services/api.service.security';
import { ExternalProviderPublic } from '../../../entities/security-entities';
import { SpiderlyButtonComponent } from '../../spiderly-buttons/spiderly-button/spiderly-button.component';

@Component({
  selector: 'auth',
  templateUrl: './auth.component.html',
  styles: [],
  imports: [CommonModule, TranslocoDirective, SpiderlyButtonComponent],
})
export class AuthComponent implements OnInit, OnDestroy {
  private initCompanyAuthDialogDetailsSubscription: Subscription | null = null;

  @Output() onCompanyNameChange: EventEmitter<string> = new EventEmitter();

  companyName: string;
  image: string;

  // Config-driven: populated from Security/GetExternalProviders (backend is the single source of truth).
  externalProviders: ExternalProviderPublic[] = [];

  constructor(
    public config: ConfigServiceBase,
    private authService: AuthServiceBase,
    private apiService: ApiSecurityService,
  ) {}

  ngOnInit() {
    this.initCompanyDetails();

    this.apiService.getExternalProviders().subscribe((providers) => {
      this.externalProviders = providers ?? [];
    });
  }

  initCompanyDetails() {
    this.initCompanyAuthDialogDetailsSubscription = this.authService
      .initCompanyAuthDialogDetails()
      .subscribe((initCompanyAuthDialogDetails) => {
        if (initCompanyAuthDialogDetails != null) {
          this.image = initCompanyAuthDialogDetails.image;
          this.companyName = initCompanyAuthDialogDetails.companyName;
          this.onCompanyNameChange.next(this.companyName);
        }
      });
  }

  loginWithExternalProvider(code: string) {
    // Server-side flow (B2): hand off to the backend challenge endpoint. The backend runs the OAuth
    // dance, sets the session cookies, and redirects back to returnUrl — where the app-init refresh
    // picks up the cookie session.
    const returnUrl = this.config.frontendUrl;
    const browserId = this.authService.getBrowserId();
    window.location.href =
      `${this.config.apiUrl}/Security/ExternalLoginChallenge` +
      `?provider=${encodeURIComponent(code)}` +
      `&returnUrl=${encodeURIComponent(returnUrl)}` +
      `&browserId=${encodeURIComponent(browserId)}`;
  }

  ngOnDestroy(): void {
    if (this.initCompanyAuthDialogDetailsSubscription) {
      this.initCompanyAuthDialogDetailsSubscription.unsubscribe();
    }
  }
}
