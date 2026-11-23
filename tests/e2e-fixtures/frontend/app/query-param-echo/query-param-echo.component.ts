import { Component, OnInit, signal } from '@angular/core';
import { ApiService } from 'src/app/business/services/api/api.service';

// Sends one instant through a generated API-service method's DateTime query param and shows what the
// backend bound from it. query-param-echo.spec.ts holds the same instant as a literal, so a change here
// changes the spec too. It also calls the method of RouteEchoController, for route-echo.spec.ts.
@Component({
    selector: 'query-param-echo',
    template: `
        <p data-testid="echoed-date-time">{{ echoedDateTime() }}</p>
        <p data-testid="echoed-controller-route">{{ echoedControllerRoute() }}</p>
    `,
})
export class QueryParamEchoComponent implements OnInit {
    echoedDateTime = signal('');
    echoedControllerRoute = signal('');

    constructor(
        private apiService: ApiService,
    ) { }

    ngOnInit() {
        this.apiService.echoDateTimeQueryParam(new Date('2026-03-29T01:30:15.250Z'))
            .subscribe(echoed => this.echoedDateTime.set(echoed));
        this.apiService.echoControllerRoute()
            .subscribe(echoed => this.echoedControllerRoute.set(echoed));
    }
}
