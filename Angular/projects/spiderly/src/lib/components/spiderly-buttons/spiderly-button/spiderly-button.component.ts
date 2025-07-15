import { Component, Input } from "@angular/core";
import { CommonModule } from "@angular/common";
import { ButtonModule } from "primeng/button";
import { SplitButtonModule } from "primeng/splitbutton";
import { SpiderlyButtonBaseComponent } from "../spiderly-button-base/spiderly-button-base";

@Component({
    selector: 'spiderly-button',
    templateUrl: './spiderly-button.component.html',
    styles: [],
    imports: [
        CommonModule,
        ButtonModule,
        SplitButtonModule
    ]
})
export class SpiderlyButtonComponent extends SpiderlyButtonBaseComponent {

  // constructor() {
  //   super();
    
  // }
  @Input() type: 'button' | 'submit' | 'reset' = 'button';

}