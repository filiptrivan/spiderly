import { ConfigBaseService } from './config-base.service';
import { Injectable } from '@angular/core';
import { TranslocoLoader } from '@jsverse/transloco';
import { HttpClient } from '@angular/common/http';

@Injectable({ 
  providedIn: 'root' 
})
export class SpiderlyTranslocoLoader implements TranslocoLoader {
  constructor(
    private http: HttpClient,
    private config: ConfigBaseService,
  ) {}

  getTranslation(lang: string) {
    return this.http.get(`${this.config.frontendUrl}/assets/i18n/${lang}.json`);
  }

}