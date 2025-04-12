import { TranslocoService } from '@jsverse/transloco';
import { ErrorHandler, Injectable } from '@angular/core';
import { SpiderlyMessageService } from '../services/spiderly-message.service';
import { HttpErrorResponse } from '@angular/common/http';
import { ConfigBaseService } from '../services/config-base.service';

@Injectable({
  providedIn: 'root'
})
export class SpiderlyErrorHandler implements ErrorHandler {
  constructor(
    private messageService: SpiderlyMessageService, 
    private translocoService: TranslocoService, 
    private config: ConfigBaseService,
  ) {

  }

  handleError(error: any): void {
    if(this.config.production == false){
      console.error(error);
    }

    if(error instanceof HttpErrorResponse == false){
      this.messageService.errorMessage(
        this.translocoService.translate('UnexpectedErrorDetails'),
        this.translocoService.translate('UnexpectedErrorTitle'),
      );
    }

  }
}
