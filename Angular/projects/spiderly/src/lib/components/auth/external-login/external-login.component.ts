import { CommonModule } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { ApiSecurityService } from '../../../services/api.service.security';
import { AuthServiceBase } from '../../../services/auth.service.base';
import { ConfigServiceBase } from '../../../services/config.service.base';
import { ExternalProviderPublic } from '../../../entities/security-entities';
import { SpiderlyButtonComponent } from '../../spiderly-buttons/spiderly-button/spiderly-button.component';
import { DEFAULT_EXTERNAL_PROVIDER_ICONS } from '../external-provider-icons';

@Component({
  selector: 'spiderly-external-login',
  templateUrl: './external-login.component.html',
  imports: [CommonModule, TranslocoDirective, SpiderlyButtonComponent],
})
export class ExternalLoginComponent implements OnInit {
  /** Per-code icon overrides; unset codes fall back to DEFAULT_EXTERNAL_PROVIDER_ICONS. */
  @Input() providerIcons: Record<string, string> = {};

  // Config-driven: populated from Security/GetExternalProviders (backend is the single source of truth for which providers are enabled).
  externalProviders: ExternalProviderPublic[] = [];

  constructor(
    private config: ConfigServiceBase,
    private authService: AuthServiceBase,
    private apiService: ApiSecurityService,
  ) {}

  ngOnInit() {
    this.apiService.getExternalProviders().subscribe({
      next: (providers) => {
        this.externalProviders = providers ?? [];
      },
      // The global unauthorized interceptor already surfaces the HTTP error to the user; here we just
      // leave the provider buttons hidden instead of letting the error reach the global error handler.
      error: () => {
        this.externalProviders = [];
      },
    });
  }

  iconFor(code: string): string | undefined {
    return this.providerIcons[code] ?? DEFAULT_EXTERNAL_PROVIDER_ICONS[code];
  }

  loginWithExternalProvider(code: string) {
    // Server-side flow (B2): hand off to the backend challenge endpoint. The backend runs the OAuth
    // dance, sets the session cookies, and redirects back to returnUrl.
    const returnUrl = this.config.frontendUrl;
    const browserId = this.authService.getBrowserId();
    window.location.href =
      `${this.config.apiUrl}/Security/ExternalLoginChallenge` +
      `?provider=${encodeURIComponent(code)}` +
      `&returnUrl=${encodeURIComponent(returnUrl)}` +
      `&browserId=${encodeURIComponent(browserId)}`;
  }
}
