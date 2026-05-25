# External-Provider Icons + Login-Component Refactor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the external-provider sign-in icon off the backend to a frontend `code → icon` map with built-in defaults, and split the mis-factored `AuthComponent` into single-responsibility components while renaming the login component, giving consumers graduated control over the login page.

**Architecture:** In the Spiderly Angular lib, delete `AuthComponent` and replace it with `AuthCardComponent` (presentational shell + branding + content slots) and `ExternalLoginComponent` (provider fetch + buttons + icon resolution + challenge redirect, owns `providerIcons`). Rename `LoginComponent`→`SpiderlyLoginComponent` (selector `app-login`→`spiderly-login`), which composes the two. Remove `IconUrl` from the backend config/DTO/endpoint. PACMS consumes via a thin owned wrapper that passes `providerIcons`.

**Tech Stack:** Angular 19 (standalone components, `ng-content` fallback content), .NET 9, Transloco, PrimeNG.

**Verification policy (read first):** Per `pa-cms/CLAUDE.md` and the spec, this is presentational wiring — **no new unit tests** are written (they would be worthless here; the existing auth flow covers integration). Each task verifies by **building** the affected project and committing; the final task does one **manual runtime** check. This intentionally departs from the writing-plans TDD template, per the project's explicit "don't bloat with worthless tests" rule, which overrides the skill.

**Repos & branches:** `spiderly` (branch `develop`) for lib/backend/init-template; `pa-cms` (branch `master`) for the consumer; `spiderly-website` (branch `develop`) for docs. The workspace root is not a git repo — run git inside each repo dir. Commit only files you touch in each task.

**Spec:** `spiderly/docs/superpowers/specs/2026-05-25-external-provider-icons-and-login-customization-design.md`

---

## Task 1: Backend — remove `IconUrl`

**Repo:** `spiderly` (`develop`)

**Files:**
- Modify: `Spiderly.Shared/Options/ExternalProviderConfig.cs` (remove the `IconUrl` property, ~lines 47-51)
- Modify: `Spiderly.Shared/ExternalAuth/ExternalProviderPublicInfo.cs` (remove `IconUrl`, ~lines 22-23)
- Modify: `Spiderly.Shared/ExternalAuth/ExternalAuthProviderRegistry.cs` (remove `IconUrl = config.IconUrl,` in the `_publicConfigs.Add(...)` projection, line 58)
- Modify: `Spiderly.Security/DTO/ExternalProviderPublicDTO.cs` (remove `IconUrl`, ~lines 25-26)
- Modify: `Spiderly.Security/Services/SecurityServiceBase.cs` (remove `IconUrl = x.IconUrl,`, line 398)
- Modify: `schemas/appsettings.schema.json` (remove the `IconUrl` property from the `ExternalProviders` array item)

- [ ] **Step 1: Remove `IconUrl` from `ExternalProviderConfig.cs`**

Delete this property (keep `Label` above it and the closing brace):

```csharp
        /// <summary>Optional icon URL for the provider's sign-in button.</summary>
        public string IconUrl { get; set; }
```

- [ ] **Step 2: Remove `IconUrl` from `ExternalProviderPublicInfo.cs`**

Delete:

```csharp
        /// <summary>Optional icon URL for the sign-in button.</summary>
        public string IconUrl { get; set; }
```

- [ ] **Step 3: Remove `IconUrl` from the registry projection in `ExternalAuthProviderRegistry.cs`**

The `_publicConfigs.Add(new ExternalProviderPublicInfo { ... })` block becomes:

```csharp
                _publicConfigs.Add(new ExternalProviderPublicInfo
                {
                    Code = config.Code,
                    Authority = ExternalProviderPresets.ResolveAuthority(config.Code, config.Authority),
                    ClientId = config.ClientId,
                    Label = config.Label,
                });
```

- [ ] **Step 4: Remove `IconUrl` from `ExternalProviderPublicDTO.cs`**

Delete:

```csharp
        /// <summary>Optional icon URL for the sign-in button.</summary>
        public string IconUrl { get; set; }
```

- [ ] **Step 5: Remove `IconUrl` from the DTO projection in `SecurityServiceBase.cs`**

The `GetExternalProviders()` projection becomes:

```csharp
                .Select(x => new ExternalProviderPublicDTO
                {
                    Code = x.Code,
                    Authority = x.Authority,
                    ClientId = x.ClientId,
                    Label = x.Label,
                })
```

- [ ] **Step 6: Remove `IconUrl` from `schemas/appsettings.schema.json`**

Open the file, locate the `ExternalProviders` array's item `properties`, and delete the `"IconUrl": { ... }` property entry. Leave `Code`, `Authority`, `ClientId`, `ClientSecret`, `Scopes`, `Label` intact.

- [ ] **Step 7: Build**

Run (from `spiderly/`): `dotnet build Spiderly.Security/Spiderly.Security.csproj`
Expected: Build succeeded, 0 errors. (No remaining references to `IconUrl`.)

- [ ] **Step 8: Commit**

```bash
cd spiderly
git add Spiderly.Shared/Options/ExternalProviderConfig.cs Spiderly.Shared/ExternalAuth/ExternalProviderPublicInfo.cs Spiderly.Shared/ExternalAuth/ExternalAuthProviderRegistry.cs Spiderly.Security/DTO/ExternalProviderPublicDTO.cs Spiderly.Security/Services/SecurityServiceBase.cs schemas/appsettings.schema.json
git commit -m "refactor(security): drop IconUrl from external-provider config and public DTO

The provider sign-in icon moves to the frontend (code->icon map); the
backend no longer owns or exposes it.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Lib — icon defaults + `ExternalLoginComponent`

**Repo:** `spiderly` (`develop`)

**Files:**
- Create: `Angular/projects/spiderly/src/lib/components/auth/external-provider-icons.ts`
- Create: `Angular/projects/spiderly/src/lib/components/auth/external-login/external-login.component.ts`
- Create: `Angular/projects/spiderly/src/lib/components/auth/external-login/external-login.component.html`
- Modify: `Angular/projects/spiderly/src/public-api.ts`

- [ ] **Step 1: Create the default-icons module**

`external-provider-icons.ts` — the official Google "G" stored as SVG source and exposed as a URL-encoded data URI (no network, no CSP, no asset pipeline):

```ts
const GOOGLE_G_SVG = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" width="18" height="18"><path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/><path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/><path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.55 10.78l7.98-6.19z"/><path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/></svg>`;

/**
 * Built-in default icons for external auth providers, keyed by provider code.
 * Values are inline data URIs — no network request, CSP entry, or asset wiring,
 * and they render offline. Consumers override per code via the `providerIcons`
 * input on ExternalLoginComponent / SpiderlyLoginComponent.
 */
export const DEFAULT_EXTERNAL_PROVIDER_ICONS: Record<string, string> = {
  google: `data:image/svg+xml,${encodeURIComponent(GOOGLE_G_SVG)}`,
};
```

- [ ] **Step 2: Create `ExternalLoginComponent`**

`external-login/external-login.component.ts` — carves the provider fetch + buttons + redirect out of the old `AuthComponent`, adds `providerIcons` + `iconFor`:

```ts
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
    this.apiService.getExternalProviders().subscribe((providers) => {
      this.externalProviders = providers ?? [];
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
```

- [ ] **Step 3: Create the `ExternalLoginComponent` template**

`external-login/external-login.component.html` — the "or" separator + provider buttons (moved out of `AuthComponent`):

```html
<ng-container *transloco="let t">
  <div *ngIf="externalProviders.length > 0">
    <div
      style="display: flex; align-items: center; gap: 7px; justify-content: center; margin-bottom: 16px;"
    >
      <div class="separator"></div>
      <div>{{ t("or") }}</div>
      <div class="separator"></div>
    </div>
    <div style="display: flex; flex-direction: column; gap: 10px">
      <spiderly-button
        *ngFor="let provider of externalProviders"
        (onClick)="loginWithExternalProvider(provider.code)"
        [label]="provider.label || (provider.code | titlecase)"
        [iconUrl]="iconFor(provider.code)"
        styleClass="w-full"
      ></spiderly-button>
    </div>
  </div>
</ng-container>
```

- [ ] **Step 4: Export from `public-api.ts`**

Add these lines near the other auth exports (around line 24-26):

```ts
export * from './lib/components/auth/external-provider-icons';
export * from './lib/components/auth/external-login/external-login.component';
```

- [ ] **Step 5: Build the lib**

Run (from `spiderly/Angular`): `npm install && npx ng build spiderly`
Expected: build succeeds, 0 errors.

- [ ] **Step 6: Commit**

```bash
cd spiderly
git add Angular/projects/spiderly/src/lib/components/auth/external-provider-icons.ts Angular/projects/spiderly/src/lib/components/auth/external-login/ Angular/projects/spiderly/src/public-api.ts
git commit -m "feat(auth): add ExternalLoginComponent with frontend code->icon map

Carves the external-provider feature out of AuthComponent into a
self-contained component that owns providerIcons (default Google icon
shipped inline). Source of truth for which providers exist stays backend.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Lib — `AuthCardComponent`

**Repo:** `spiderly` (`develop`)

**Files:**
- Create: `Angular/projects/spiderly/src/lib/components/auth/auth-card/auth-card.component.ts`
- Create: `Angular/projects/spiderly/src/lib/components/auth/auth-card/auth-card.component.html`
- Modify: `Angular/projects/spiderly/src/public-api.ts`

- [ ] **Step 1: Create `AuthCardComponent`**

`auth-card/auth-card.component.ts` — the presentational shell + branding, no output back-channel (carved from `AuthComponent`):

```ts
import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { AuthServiceBase } from '../../../services/auth.service.base';

@Component({
  selector: 'spiderly-auth-card',
  templateUrl: './auth-card.component.html',
  imports: [CommonModule],
})
export class AuthCardComponent implements OnInit, OnDestroy {
  private companyDetailsSubscription: Subscription | null = null;

  companyName: string;
  image: string;

  constructor(private authService: AuthServiceBase) {}

  ngOnInit() {
    this.companyDetailsSubscription = this.authService
      .initCompanyAuthDialogDetails()
      .subscribe((details) => {
        if (details != null) {
          this.image = details.image;
          this.companyName = details.companyName;
        }
      });
  }

  ngOnDestroy(): void {
    this.companyDetailsSubscription?.unsubscribe();
  }
}
```

- [ ] **Step 2: Create the `AuthCardComponent` template**

`auth-card/auth-card.component.html` — card shell + logo with Angular-19 `ng-content` fallback content for the `[auth-logo]` slot, plus default content slot and `[auth-footer]`:

```html
<div class="flex min-h-screen overflow-hidden p-5">
  <div class="flex flex-col w-full">
    <div
      class="w-full sm:w-120"
      style="
        margin: auto;
        border-radius: 50px;
        padding: 0.3rem;
        background: linear-gradient(
          180deg,
          var(--p-primary-color) 10%,
          rgba(33, 150, 243, 0) 30%
        );
      "
    >
      <div class="surface-card py-12 px-8 sm:px-12" style="border-radius: 45px">
        <div class="flex justify-center" style="margin-bottom: 38px">
          <ng-content select="[auth-logo]">
            <img
              *ngIf="image != null"
              [src]="image"
              alt="{{ companyName }} Logo"
              title="{{ companyName }} Logo"
              class="max-h-15"
            />
            <i
              *ngIf="image == null"
              class="pi pi-spin pi-spinner primary-color"
              style="font-size: 2rem"
            ></i>
          </ng-content>
        </div>

        <ng-content></ng-content>

        <ng-content select="[auth-footer]"></ng-content>
      </div>
    </div>
  </div>
</div>
```

- [ ] **Step 3: Export from `public-api.ts`**

Add:

```ts
export * from './lib/components/auth/auth-card/auth-card.component';
```

- [ ] **Step 4: Build the lib**

Run (from `spiderly/Angular`): `npx ng build spiderly`
Expected: build succeeds, 0 errors.

- [ ] **Step 5: Commit**

```bash
cd spiderly
git add Angular/projects/spiderly/src/lib/components/auth/auth-card/ Angular/projects/spiderly/src/public-api.ts
git commit -m "feat(auth): add AuthCardComponent presentational shell

Carves the branded login card + logo out of AuthComponent into a pure
presentational component with named content slots (auth-logo with
fallback, default, auth-footer). No company-name output back-channel.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Lib — rename `LoginComponent`→`SpiderlyLoginComponent`, recompose, delete `AuthComponent`

**Repo:** `spiderly` (`develop`)

**Files:**
- Modify: `Angular/projects/spiderly/src/lib/components/auth/login/login.component.ts`
- Modify: `Angular/projects/spiderly/src/lib/components/auth/login/login.component.html`
- Delete: `Angular/projects/spiderly/src/lib/components/auth/partials/auth.component.ts`
- Delete: `Angular/projects/spiderly/src/lib/components/auth/partials/auth.component.html`
- Modify: `Angular/projects/spiderly/src/lib/entities/security-entities.ts` (remove `iconUrl` from `ExternalProviderPublic`)

- [ ] **Step 1: Rewrite `login.component.ts` as `SpiderlyLoginComponent`**

Replace the whole file. Class renamed, selector `spiderly-login`, `providerIcons` input added, `AuthComponent` import swapped for `AuthCardComponent` + `ExternalLoginComponent`, the dead `companyName`/`companyNameChange` and unused `config` injection removed:

```ts
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
  Input,
  KeyValueDiffers,
  OnInit,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { SpiderlyControlsModule } from '../../../controls/spiderly-controls.module';
import { Login } from '../../../entities/security-entities';
import { AuthServiceBase } from '../../../services/auth.service.base';
import { BaseFormService } from '../../../services/base-form.service';
import { SpiderlyMessageService } from '../../../services/spiderly-message.service';
import { BaseFormComponent } from '../../base-form/base-form.component';
import { SpiderlyFormGroup } from '../../spiderly-form-control/spiderly-form-control';
import { AuthCardComponent } from '../auth-card/auth-card.component';
import { ExternalLoginComponent } from '../external-login/external-login.component';
import { LoginVerificationComponent } from '../partials/login-verification.component';

@Component({
  selector: 'spiderly-login',
  templateUrl: './login.component.html',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AuthCardComponent,
    ExternalLoginComponent,
    SpiderlyControlsModule,
    LoginVerificationComponent,
    TranslocoDirective,
  ],
})
export class SpiderlyLoginComponent extends BaseFormComponent implements OnInit {
  loginFormGroup = new SpiderlyFormGroup<Login>({});
  showEmailSentDialog: boolean = false;

  /** Per-code provider icon overrides, forwarded to <spiderly-external-login>. */
  @Input() providerIcons: Record<string, string> = {};

  constructor(
    protected override differs: KeyValueDiffers,
    protected override http: HttpClient,
    protected override messageService: SpiderlyMessageService,
    protected override changeDetectorRef: ChangeDetectorRef,
    protected override router: Router,
    protected override route: ActivatedRoute,
    protected override translocoService: TranslocoService,
    protected override baseFormService: BaseFormService,
    private authService: AuthServiceBase,
  ) {
    super(
      differs,
      http,
      messageService,
      changeDetectorRef,
      router,
      route,
      translocoService,
      baseFormService,
    );
  }

  override ngOnInit() {
    this.initLoginFormGroup(new Login({}));
  }

  initLoginFormGroup(model: Login) {
    this.baseFormService.initFormGroup(this.loginFormGroup, Login, model, [
      'email',
    ]);
  }

  sendLoginVerificationEmail() {
    let isFormGroupValid: boolean = this.baseFormService.isControlValid(
      this.loginFormGroup,
    );

    if (isFormGroupValid == false) {
      this.baseFormService.showInvalidFieldsMessage();
      return;
    }

    this.authService
      .sendLoginVerificationEmail(this.loginFormGroup.getRawValue())
      .subscribe((result) => {
        if (result.message) {
          this.messageService.successMessage(result.message);
        }
        this.showEmailSentDialog = true;
      });
  }
}
```

> If the build in Step 5 reports `authService` (or any other) as unused, that is unexpected (it is used in `sendLoginVerificationEmail`). If it reports `config` missing, ensure no leftover reference remains in the template.

- [ ] **Step 2: Rewrite `login.component.html`**

Replace `<auth>` with `<spiderly-auth-card>`, move the form in as default content, add `<spiderly-external-login [providerIcons]>`, and forward the two slots via `ngProjectAs`:

```html
<ng-container *transloco="let t">
  @if (loginFormGroup != null) {
    @if (showEmailSentDialog == false) {
      <spiderly-auth-card>
        <ng-content select="[auth-logo]" ngProjectAs="[auth-logo]"></ng-content>

        <form [formGroup]="loginFormGroup" style="margin-bottom: 16px">
          <div>
            <spiderly-textbox
              [control]="loginFormGroup.getControl('email')"
            ></spiderly-textbox>
          </div>

          <div class="mt-4 mb-6">
            <div class="text-center" style="font-size: smaller">
              {{ t("AgreementsOnRegister") }}
              <b
                routerLink="/user-agreement"
                class="primary-color cursor-pointer"
                >{{ t("UserAgreement") }}</b
              >
              {{ t("and") }}
              <b
                routerLink="/privacy-policy"
                class="primary-color cursor-pointer"
                >{{ t("PrivacyPolicy") }}</b
              >.
            </div>
          </div>

          <div style="display: flex; flex-direction: column; gap: 16px">
            <spiderly-button
              [label]="t('Login')"
              (onClick)="sendLoginVerificationEmail()"
              [outlined]="true"
              [style]="{ width: '100%' }"
              type="submit"
            ></spiderly-button>
          </div>
        </form>

        <spiderly-external-login
          [providerIcons]="providerIcons"
        ></spiderly-external-login>

        <ng-content select="[auth-footer]" ngProjectAs="[auth-footer]"></ng-content>
      </spiderly-auth-card>
    } @else {
      <login-verification
        [email]="loginFormGroup.controls.email.getRawValue()"
      ></login-verification>
    }
  }
</ng-container>
```

- [ ] **Step 3: Delete the old `AuthComponent`**

```bash
cd spiderly
git rm Angular/projects/spiderly/src/lib/components/auth/partials/auth.component.ts Angular/projects/spiderly/src/lib/components/auth/partials/auth.component.html
```

- [ ] **Step 4: Remove `iconUrl` from `ExternalProviderPublic` in `security-entities.ts`**

In `Angular/projects/spiderly/src/lib/entities/security-entities.ts`, the `ExternalProviderPublic` class (around line 147) — delete every `iconUrl` occurrence: the field declaration (line 154), the constructor destructure param (161) and its type (167), the assignment (175), and the `schema` entry (191-193). The class afterward keeps only `code`, `authority`, `clientId`, `label` in all four places. The schema object becomes:

```ts
  static readonly schema = {
    code: {
      type: 'string',
    },
    authority: {
      type: 'string',
    },
    clientId: {
      type: 'string',
    },
    label: {
      type: 'string',
    },
  };
```

(Adjust the trailing lines after `label` to match the existing file's closing structure.)

- [ ] **Step 5: Build the lib**

Run (from `spiderly/Angular`): `npx ng build spiderly`
Expected: build succeeds, 0 errors. No remaining reference to `AuthComponent` or `iconUrl`.

- [ ] **Step 6: Verify the public-api export still resolves**

Run: `grep -n "login.component" spiderly/Angular/projects/spiderly/src/public-api.ts`
Expected: the existing `export * from './lib/components/auth/login/login.component';` line is still present (it now re-exports `SpiderlyLoginComponent`). No change needed — wildcard export follows the rename.

- [ ] **Step 7: Commit**

```bash
cd spiderly
git add Angular/projects/spiderly/src/lib/components/auth/login/ Angular/projects/spiderly/src/lib/entities/security-entities.ts
git commit -m "refactor(auth)!: rename LoginComponent->SpiderlyLoginComponent, delete AuthComponent

LoginComponent (selector app-login) -> SpiderlyLoginComponent (spiderly-login),
freeing the conventional name/selector for consumers. The page now composes
AuthCardComponent + ExternalLoginComponent and forwards providerIcons + slots.
Removes iconUrl from the ExternalProviderPublic frontend type.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Lib — init template + e2e helper

**Repo:** `spiderly` (`develop`)

**Files:**
- Modify: `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs:1305`
- Check (modify only if needed): the Playwright login helper in `Angular` e2e fixtures

- [ ] **Step 1: Update the init-template login route**

In `NetAndAngularFilesGenerator.cs`, line 1305 currently reads:

```
        loadComponent: () => import('spiderly').then(c => c.LoginComponent),
```

Change to:

```
        loadComponent: () => import('spiderly').then(c => c.SpiderlyLoginComponent),
```

- [ ] **Step 2: Check the e2e login helper for the old selector**

Run (from `spiderly/Angular`): `grep -rn "app-login" projects/spiderly/ e2e* playwright* 2>/dev/null || echo "no app-login references"`
- If a Playwright helper navigates by the `app-login` tag selector, update it to `spiderly-login`.
- The route path `/login` is unchanged, so a helper that uses `goto('/login')` + queries the email input needs no change. Note the result.

- [ ] **Step 3: Build**

Run (from `spiderly/`): `dotnet build Spiderly.Shared/Spiderly.Shared.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
cd spiderly
git add Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs
# add the e2e helper file too only if Step 2 changed it
git commit -m "chore(init): point generated login route at SpiderlyLoginComponent

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: PACMS — consume via wrapper, drop backend icon config

**Repo:** `pa-cms` (`master`)

**Files:**
- Create: `Frontend/src/app/pages/auth/login.component.ts`
- Create: `Frontend/src/assets/icons/google.svg`
- Modify: `Frontend/src/app/app.routes.ts:330`
- Modify: `Backend/PACMS.WebAPI/appsettings.json:101-108` (remove `IconUrl`)

**Prereq:** the local-dev `spiderly` paths map in `Frontend/tsconfig.json` is enabled (per the external-auth memory) so the admin compiles against local lib source containing `SpiderlyLoginComponent`.

- [ ] **Step 1: Add the self-hosted Google icon asset**

Create `Frontend/src/assets/icons/google.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" width="18" height="18"><path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/><path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/><path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.55 10.78l7.98-6.19z"/><path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/></svg>
```

- [ ] **Step 2: Create the PACMS login wrapper**

`Frontend/src/app/pages/auth/login.component.ts` — owns the icon map, passes it down. No inheritance, no logic:

```ts
import { Component } from '@angular/core';
import { SpiderlyLoginComponent } from 'spiderly';

@Component({
  selector: 'app-login',
  imports: [SpiderlyLoginComponent],
  template: `<spiderly-login [providerIcons]="providerIcons" />`,
})
export class LoginComponent {
  providerIcons: Record<string, string> = {
    google: 'assets/icons/google.svg',
  };
}
```

- [ ] **Step 3: Route to the wrapper**

In `Frontend/src/app/app.routes.ts`, the `login` route (line 330-331) currently:

```ts
        path: 'login',
        loadComponent: () => import('spiderly').then((c) => c.LoginComponent),
```

becomes:

```ts
        path: 'login',
        loadComponent: () => import('./pages/auth/login.component').then((c) => c.LoginComponent),
```

- [ ] **Step 4: Remove `IconUrl` from backend config**

In `Backend/PACMS.WebAPI/appsettings.json`, the `ExternalProviders` entry becomes (drop the `IconUrl` line, keep the rest):

```json
      "ExternalProviders": [
        {
          "Code": "google",
          "ClientId": "985227526511-2fknq706e90lrj7iiptnb2hruqdhqcb8.apps.googleusercontent.com",
          "Label": "Nastavi sa Google nalogom"
        }
      ]
```

- [ ] **Step 5: Build the admin frontend**

Run (from `pa-cms/Frontend`): `npm install && npx ng build`
Expected: build succeeds, 0 errors; `SpiderlyLoginComponent` resolves from the local `spiderly` source.

- [ ] **Step 6: Commit**

```bash
cd pa-cms
git add Frontend/src/app/pages/auth/login.component.ts Frontend/src/assets/icons/google.svg Frontend/src/app/app.routes.ts Backend/PACMS.WebAPI/appsettings.json
git commit -m "feat(admin): self-host Google sign-in icon via login wrapper

Route /login to a thin app-side LoginComponent that passes a code->icon
map (google -> assets/icons/google.svg) to SpiderlyLoginComponent. Drops
the hotlinked IconUrl from backend config.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

> **BA instance:** if `prodajaalata.ba`'s backend config carries an `ExternalProviders[].IconUrl`, remove it the same way. (BA is manual-CMS; confirm whether it has Google login configured at all.)

---

## Task 7: Docs

**Repos:** `spiderly` (`develop`) + `spiderly-website` (`develop`)

**Files:**
- Modify: `spiderly/docs/external-auth-providers.md`
- Modify: the external-auth page in `spiderly-website` (locate it)

- [ ] **Step 1: Update the Spiderly design doc**

In `spiderly/docs/external-auth-providers.md`, update the frontend section to reflect: icon is a frontend `code → icon` map (`DEFAULT_EXTERNAL_PROVIDER_ICONS` + `providerIcons` input), `IconUrl` removed from backend config/DTO, and the `AuthComponent` → `AuthCardComponent` + `ExternalLoginComponent` split with the `SpiderlyLoginComponent` rename and the consumer control levels.

- [ ] **Step 2: Locate and update the website docs**

Run (from `spiderly-website/`): `grep -rln "external" docs/ src/ content/ 2>/dev/null | head` to find the external-auth doc page, then update it to match Step 1 (icon-by-code on the frontend, the component split + rename, the control levels). If no external-auth page exists yet on the website, note that and skip.

- [ ] **Step 3: Commit (each repo separately)**

```bash
cd spiderly
git add docs/external-auth-providers.md
git commit -m "docs: external-provider icons move to frontend; AuthComponent split

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

```bash
cd spiderly-website
# only if Step 2 changed a file
git add <the-updated-doc>
git commit -m "docs: external-provider icons + login customization

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Manual runtime verification

**Repo:** none (verification only)

The user owns the backend / dev-server processes — **do not start or kill them**. Ask the user to run the stack (backend + `ng serve` for the admin) and confirm:

- [ ] **Step 1:** Open the admin `/login`. The "Nastavi sa Google nalogom" button shows the Google "G" loaded from `assets/icons/google.svg` (check DevTools Network — **no request to `developers.google.com`**).
- [ ] **Step 2:** The company logo/branding still renders at the top of the card (AuthCardComponent self-brands).
- [ ] **Step 3:** Email-code login still works end-to-end (enter email → receive code → verify → dashboard).
- [ ] **Step 4:** Clicking the Google button redirects to the backend `ExternalLoginChallenge` and completes Google login (rides on the existing external-auth flow).
- [ ] **Step 5:** Report results. If the forwarded `[auth-logo]`/`[auth-footer]` slots misbehave through `ngProjectAs` re-projection, note it — the fallback is that slots work via direct Level-2 composition; default-page slot forwarding can be dropped if unreliable.

---

## Self-review notes

- **Spec coverage:** icon-by-code (T2, T6), backend `IconUrl` removal (T1, T6), `AuthComponent` split (T2 external-login, T3 auth-card, T4 delete), rename D1 (T4, T5, T6), slots D3-unaffected/levels (T3, T4), init template (T5), docs (T7), runtime (T8). Label stays backend (T1 leaves it). All spec sections mapped.
- **Type consistency:** `providerIcons: Record<string, string>` and `iconFor(code: string): string | undefined` are identical across `ExternalLoginComponent` (T2) and `SpiderlyLoginComponent` (T4). `DEFAULT_EXTERNAL_PROVIDER_ICONS` defined T2, consumed T2. Selectors `spiderly-auth-card` / `spiderly-external-login` / `spiderly-login` consistent across T3/T4/T6.
- **No placeholders:** every code step shows full content.
