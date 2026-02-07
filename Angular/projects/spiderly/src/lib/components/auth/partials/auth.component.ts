import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { AuthServiceBase } from '../../../services/auth.service.base';
import { ConfigServiceBase } from '../../../services/config.service.base';
import { GoogleButtonComponent } from '../../spiderly-buttons/google-button/google-button.component';

@Component({
  selector: 'auth',
  templateUrl: './auth.component.html',
  styles: [],
  imports: [CommonModule, GoogleButtonComponent, TranslocoDirective],
})
export class AuthComponent {
  private initCompanyAuthDialogDetailsSubscription: Subscription | null = null;

  @Output() onCompanyNameChange: EventEmitter<string> = new EventEmitter();

  companyName: string;
  image: string;

  constructor(
    public config: ConfigServiceBase,
    private authService: AuthServiceBase,
  ) {}

  ngOnInit() {
    this.initCompanyDetails();
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

  onGoogleSignIn(googleWrapper: any) {
    googleWrapper.click();
  }

  ngOnDestroy(): void {
    if (this.initCompanyAuthDialogDetailsSubscription) {
      this.initCompanyAuthDialogDetailsSubscription.unsubscribe();
    }
  }
}
