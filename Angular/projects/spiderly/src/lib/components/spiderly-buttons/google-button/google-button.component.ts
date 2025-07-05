import { Component, EventEmitter, Input, Output } from "@angular/core";
import { ButtonModule } from "primeng/button";
import { SpiderlyButtonComponent } from "../spiderly-button/spiderly-button.component";

@Component({
    selector: 'google-button',
    templateUrl: './google-button.component.html',
    styles: [],
    imports: [
        ButtonModule,
        SpiderlyButtonComponent
    ]
})
export class GoogleButtonComponent { // Custom styling of the google button - https://medium.com/simform-engineering/implement-custom-google-sign-in-using-angular-16-9c93aeff6252
  @Input() label: string;
  @Output() loginWithGoogle: EventEmitter<any> = new EventEmitter<any>();

  createFakeGoogleWrapper = () => {
    const googleLoginWrapper = document.createElement('div');
    googleLoginWrapper.style.display = 'none';
    googleLoginWrapper.classList.add('custom-google-button');
    document.body.appendChild(googleLoginWrapper);
    window.google.accounts.id.renderButton(googleLoginWrapper, {
      type: 'icon',
      width: '200',
    });
  
    const googleLoginWrapperButton = googleLoginWrapper.querySelector(
      'div[role=button]'
    ) as HTMLElement;
  
    return {
      click: () => {
        googleLoginWrapperButton?.click();
      },
    };
  };

  handleGoogleLogin() {
    this.loginWithGoogle.emit(this.createFakeGoogleWrapper());
  }

}

declare global {
  interface Window {
    google: any;
  }
}