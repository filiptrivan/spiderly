import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
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
import { ConfigServiceBase } from '../../../services/config.service.base';
import { SpiderlyMessageService } from '../../../services/spiderly-message.service';
import { BaseFormCopy } from '../../base-form/base-form copy';
import { SpiderlyFormGroup } from '../../spiderly-form-control/spiderly-form-control';
import { AuthComponent } from '../partials/auth.component';
import { LoginVerificationComponent } from '../partials/login-verification.component';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AuthComponent,
    SpiderlyControlsModule,
    LoginVerificationComponent,
    TranslocoDirective,
  ],
})
export class LoginComponent extends BaseFormCopy implements OnInit {
  loginFormGroup = new SpiderlyFormGroup<Login>({});

  companyName: string;
  showEmailSentDialog: boolean = false;

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
    private config: ConfigServiceBase,
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

  companyNameChange(companyName: string) {
    this.companyName = companyName;
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
