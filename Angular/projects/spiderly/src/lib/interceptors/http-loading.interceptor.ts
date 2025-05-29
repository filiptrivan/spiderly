import { HttpEvent, HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { NgxSpinnerService } from "ngx-spinner";
import { Observable, finalize } from "rxjs";

export const httpLoadingInterceptor: HttpInterceptorFn = (req, next) => {
  const spinner = inject(NgxSpinnerService);
  
  const shouldSkipSpinner = req.params.has('X-Skip-Spinner'); // Using this for multiautocomplete, autocomplete, dropdown, table etc...

  if (!shouldSkipSpinner)
    spinner.show();

  return next(req).pipe(
    finalize(() => {
      if (!shouldSkipSpinner){
        spinner.hide();
      }
    })
  ) as Observable<HttpEvent<any>>;
}