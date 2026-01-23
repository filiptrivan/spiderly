import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
  EventEmitter,
  Input,
  KeyValueDiffers,
  OnInit,
  Output,
} from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { SpiderlyControlsModule } from '../../../controls/spiderly-controls.module';
import { VerificationTokenRequest } from '../../../entities/security-entities';
import { BaseFormService } from '../../../services/base-form.service';
import { SpiderlyMessageService } from '../../../services/spiderly-message.service';
import { BaseFormCopy } from '../../base-form/base-form copy';
import { SpiderlyFormGroup } from '../../spiderly-form-control/spiderly-form-control';
import { SpiderlyPanelsModule } from '../../spiderly-panels/spiderly-panels.module';

@Component({
  selector: 'verification-wrapper',
  templateUrl: './verification-wrapper.component.html',
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SpiderlyControlsModule,
    SpiderlyPanelsModule,
    ButtonModule,
    TranslocoDirective,
  ],
})
export class VerificationWrapperComponent
  extends BaseFormCopy
  implements OnInit
{
  verificationTokenRequestFormGroup =
    new SpiderlyFormGroup<VerificationTokenRequest>({});

  @Input() email: string;
  @Output() onResendVerificationToken: EventEmitter<any> = new EventEmitter();
  @Output() onCodeSubmit: EventEmitter<string> = new EventEmitter();

  constructor(
    protected override differs: KeyValueDiffers,
    protected override http: HttpClient,
    protected override messageService: SpiderlyMessageService,
    protected override changeDetectorRef: ChangeDetectorRef,
    protected override router: Router,
    protected override route: ActivatedRoute,
    protected override translocoService: TranslocoService,
    protected override baseFormService: BaseFormService,
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
    this.initVerificationTokenRequestFormGroup(
      new VerificationTokenRequest({ email: this.email }),
    );
  }

  initVerificationTokenRequestFormGroup(model: VerificationTokenRequest) {
    this.baseFormService.initFormGroup(
      this.verificationTokenRequestFormGroup,
      VerificationTokenRequest,
      model,
      ['verificationCode'],
    );
  }

  codeSubmit() {
    let isValid: boolean = this.baseFormService.isControlValid(
      this.verificationTokenRequestFormGroup,
    );

    if (isValid) {
      this.onCodeSubmit.next(
        this.verificationTokenRequestFormGroup.controls.verificationCode.getRawValue(),
      );
    }
  }

  resendVerificationToken() {
    this.onResendVerificationToken.next(null);
  }
}
