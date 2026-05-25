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
