import { Component, EventEmitter, Input, Output } from "@angular/core";
import { ButtonModule } from "primeng/button";
import { SpiderlyButtonComponent } from "../spiderly-button/spiderly-button.component";
import { createFakeGoogleWrapper } from "../../../services/helper-functions";

@Component({
    selector: 'google-button',
    templateUrl: './google-button.component.html',
    styles: [],
    imports: [
        ButtonModule,
        SpiderlyButtonComponent
    ]
})
export class GoogleButtonComponent {
  @Input() label: string;
  @Output() loginWithGoogle: EventEmitter<any> = new EventEmitter<any>();

  handleGoogleLogin() {
    this.loginWithGoogle.emit(createFakeGoogleWrapper());
  }

}

declare global {
  interface Window {
    google: any;
  }
}