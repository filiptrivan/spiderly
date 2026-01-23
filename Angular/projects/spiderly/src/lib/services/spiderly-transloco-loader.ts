import { ConfigServiceBase } from './config.service.base';
import { Injectable } from '@angular/core';
import { TranslocoLoader } from '@jsverse/transloco';
import { HttpClient } from '@angular/common/http';

@Injectable({
  providedIn: 'root',
})
export class SpiderlyTranslocoLoader implements TranslocoLoader {
  constructor(
    private http: HttpClient,
    private config: ConfigServiceBase,
  ) {}

  getTranslation(lang: string) {
    return this.http.get(`${this.config.frontendUrl}/assets/i18n/${lang}.json`);
  }
}
