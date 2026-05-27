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
    this.showExternalAuthErrorIfPresent();
  }

  // Surface a friendly message when the server-side external login bounced back with an error (captured from
  // the URL at bootstrap by AuthServiceBase). "expired" = the user lingered on the provider's account picker.
  private showExternalAuthErrorIfPresent() {
    const code = this.authService.externalAuthErrorCode;
    if (!code) {
      return;
    }
    this.authService.externalAuthErrorCode = null; // show once
    const messageKey =
      code === 'expired' ? 'ExternalLoginExpiredDetails' : 'ExternalLoginFailedDetails';
    this.messageService.warningMessage(this.translocoService.translate(messageKey));
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
