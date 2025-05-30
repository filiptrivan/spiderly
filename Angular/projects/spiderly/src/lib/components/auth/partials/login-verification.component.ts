import { Component, Input, OnInit } from '@angular/core';
import { VerificationWrapperComponent } from './verification-wrapper.component';
import { TranslocoService } from '@jsverse/transloco';
import { SpiderlyMessageService } from '../../../services/spiderly-message.service';
import { AuthBaseService } from '../../../services/auth-base.service';

@Component({
    selector: 'login-verification',
    templateUrl: './login-verification.component.html',
    imports: [
        VerificationWrapperComponent
    ]
})
export class LoginVerificationComponent implements OnInit {
    @Input() email: string;
    @Input() userId: number;

    constructor(
      private authService: AuthBaseService, 
      private messageService: SpiderlyMessageService,
      private translocoService: TranslocoService,
    ) { 
    }

    ngOnInit(){
    }
    
    resendVerificationToken(){
        this.authService.sendLoginVerificationEmail({email: this.email}).subscribe(() => {
            this.messageService.successMessage(this.translocoService.translate('SuccessfullySentVerificationCode'));
        });
    }

    onCodeSubmit(event: string){
        this.authService.login({email: this.email, verificationCode: event}).subscribe(() => {
            this.messageService.successMessage(this.translocoService.translate('YouHaveSuccessfullyVerifiedYourAccount'));
            this.authService.navigateToDashboard();
        });
    }
}

