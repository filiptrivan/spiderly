import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { TranslocoDirective } from '@jsverse/transloco';
import { AuthServiceBase } from '../../../services/auth.service.base';
import { provideSpiderlyScope } from '../../../i18n/spiderly-scope';

@Component({
  selector: 'spiderly-auth-card',
  templateUrl: './auth-card.component.html',
  providers: [provideSpiderlyScope()],
  imports: [CommonModule, TranslocoDirective],
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
