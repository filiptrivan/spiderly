import { HttpEvent, HttpResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { tap } from 'rxjs';

export const jsonHttpInterceptor: HttpInterceptorFn = (req, next) => {
  const updatedBody = convertToISOString(req.body);
  const clonedRequest: HttpRequest<any> = req.clone({ body: updatedBody });

  return next(clonedRequest)
    .pipe(
      tap((event: HttpEvent<any>) => {
        if (event instanceof HttpResponse) {
          convertToDate(event.body);
        }
      }
  ));
}

const convertToISOString = (obj: unknown): unknown => {
  if (
    obj === null || 
    obj === undefined ||
    obj instanceof FormData || 
    obj instanceof Blob || 
    obj instanceof File ||
    obj instanceof ArrayBuffer
  ) {
    return obj;
  }

  if (Array.isArray(obj)) {
    return obj.map(item => convertToISOString(item));
  }

  if (obj instanceof Date) {
    return obj.toISOString();
  }

  if (typeof obj === 'object') {
    const newObj: Record<string, unknown> = {};
    for (const key of Object.keys(obj)) {
      newObj[key] = convertToISOString((obj as any)[key]);
    }
    return newObj;
  }

  return obj;
};

/**
 * @see https://stackoverflow.com/a/54733846/1306679
 */
const convertToDate = (
  object: unknown,
  parent?: Record<string, unknown> | unknown[],
  key?: number | string,
) => {
  if (object === null) return;
  
  const dateRegex = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;

  if (typeof object === 'string') {
    if (dateRegex.test(object)) {
      const date = new Date(object);
      parent[key] = date;
    }
  } else if (Array.isArray(object)) {
    for (let i = 0; i < object.length; i++)
      convertToDate(object[i], object, i);
  } else {
    for (const key of Object.keys(object as Record<string, unknown>)) {
      convertToDate(
        (object as Record<string, unknown>)[key],
        object as Record<string, unknown>,
        key,
      );
    }
  }
}