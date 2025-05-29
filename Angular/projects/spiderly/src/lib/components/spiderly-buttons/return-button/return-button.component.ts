import { Component, Input } from "@angular/core";
import { Router } from '@angular/router';
import { CommonModule } from "@angular/common";
import { getParentUrl } from "../../../services/helper-functions";
import { TranslocoDirective } from "@jsverse/transloco";
import { ButtonModule } from "primeng/button";
import { SpiderlyButtonComponent } from "../spiderly-button/spiderly-button.component";

@Component({
    selector: 'return-button',
    templateUrl: './return-button.component.html',
    styles: [],
    imports: [
        CommonModule,
        ButtonModule,
        SpiderlyButtonComponent,
        TranslocoDirective,
    ]
})
export class SpiderlyReturnButtonComponent {
  @Input() navigateUrl: string;

  constructor(private router: Router) {}

  onReturn(){
    if(this.navigateUrl == undefined){
        const currentUrl = this.router.url;
        const parentUrl: string = getParentUrl(currentUrl);
        this.router.navigateByUrl(parentUrl);
    }
    else{
        this.router.navigate([this.navigateUrl]);
    }
  }
}