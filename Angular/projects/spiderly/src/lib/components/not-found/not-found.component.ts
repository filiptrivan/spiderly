import { Component } from '@angular/core';
import { ConfigServiceBase } from '../../services/config.service.base';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TranslocoDirective } from '@jsverse/transloco';
import { provideSpiderlyScope } from '../../i18n/spiderly-scope';

@Component({
  selector: 'not-found',
  templateUrl: './not-found.component.html',
  providers: [provideSpiderlyScope()],
  imports: [RouterModule, ButtonModule, TranslocoDirective],
})
export class NotFoundComponent {
  companyName = this.config.companyName;
  logoPath: string = this.config.logoPath;

  constructor(private config: ConfigServiceBase) {}
}
