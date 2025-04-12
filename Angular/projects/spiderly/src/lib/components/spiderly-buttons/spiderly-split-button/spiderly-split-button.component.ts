import { Component, Input } from "@angular/core";
import { CommonModule } from "@angular/common";
import { ButtonModule } from "primeng/button";
import { SplitButtonModule } from "primeng/splitbutton";

import { MenuItem } from "primeng/api";
import { SpiderlyButtonBaseComponent } from "../spiderly-button-base/spiderly-button-base";

@Component({
  selector: 'spiderly-split-button',
  templateUrl: './spiderly-split-button.component.html',
  styles: [],
  imports: [
    CommonModule,
    ButtonModule,
    SplitButtonModule
  ],
  standalone: true,
})
export class SpiderlySplitButtonComponent extends SpiderlyButtonBaseComponent {
  @Input() dropdownItems: MenuItem[];



}