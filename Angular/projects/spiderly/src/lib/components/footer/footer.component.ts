import { Component } from '@angular/core';
import { ConfigServiceBase } from '../../services/config.service.base';

@Component({
  selector: 'footer',
  templateUrl: './footer.component.html',
  standalone: true,
})
export class FooterComponent {
  companyName: string = this.config.companyName;

  constructor(private config: ConfigServiceBase) {}
}
