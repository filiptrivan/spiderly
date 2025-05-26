import { HttpEvent, HttpResponse, HttpInterceptorFn } from '@angular/common/http';
import { tap } from 'rxjs';

export const jsonHttpInterceptor: HttpInterceptorFn = (req, next) => {
  const dateRegex = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;

  const convertDates = (
    object: unknown,
    parent?: Record<string, unknown> | unknown[],
    key?: number | string,
  ) => {
    if (object === null) return;

    if (typeof object === 'string') {
      if (dateRegex.test(object)) {
        /**
         * @see https://stackoverflow.com/a/54733846/1306679
         */

        parent[key] = new Date(object);
      }
    } else if (Array.isArray(object)) {
      for (let i = 0; i < object.length; i++)
        convertDates(object[i], object, i);
    } else {
      for (const key of Object.keys(object as Record<string, unknown>)) {
        convertDates(
          (object as Record<string, unknown>)[key],
          object as Record<string, unknown>,
          key,
        );
      }
    }
  }

  return next(req)
    .pipe(
      tap((event: HttpEvent<any>) => {
        if (event instanceof HttpResponse) {
          convertDates(event.body);
        }
      }
  ));
}