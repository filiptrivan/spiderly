import { Component, OnInit, signal } from '@angular/core';
import { ApiService } from 'src/app/business/services/api/api.service';

// Sends one instant through a generated API-service method's DateTime query param and shows what the
// backend bound from it. query-param-echo.spec.ts holds the same instant as a literal, so a change here
// changes the spec too.
@Component({
    selector: 'query-param-echo',
    template: `<p data-testid="echoed-date-time">{{ echoedDateTime() }}</p>`,
})
export class QueryParamEchoComponent implements OnInit {
    echoedDateTime = signal('');

    constructor(
        private apiService: ApiService,
    ) { }

    ngOnInit() {
        this.apiService.echoDateTimeQueryParam(new Date('2026-03-29T01:30:15.250Z'))
            .subscribe(echoed => this.echoedDateTime.set(echoed));
    }
}
