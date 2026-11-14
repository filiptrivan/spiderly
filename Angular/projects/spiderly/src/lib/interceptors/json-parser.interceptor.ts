import {
  HttpEvent,
  HttpResponse,
  HttpInterceptorFn,
  HttpRequest,
  HttpParams,
} from '@angular/common/http';
import { map } from 'rxjs';

export const jsonHttpInterceptor: HttpInterceptorFn = (req, next) => {
  const cleanedBody = getCleanRequestBody(req.body);
  const clonedRequest: HttpRequest<any> = req.clone({
    body: cleanedBody,
  });

  // Only a JSON body. A C# string action answers text, and its generated method promises a string.
  return next(clonedRequest).pipe(
    map((event: HttpEvent<any>) =>
      event instanceof HttpResponse && req.responseType === 'json'
        ? event.clone({ body: convertToDate(event.body) })
        : event,
    ),
  );
};

const getCleanRequestBody = (obj: unknown): unknown => {
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
    return obj.map((item) => getCleanRequestBody(item));
  }

  if (obj instanceof Date) {
    return obj.toISOString();
  }

  if (typeof obj === 'object') {
    const newObj: Record<string, unknown> = {};
    for (const key of Object.keys(obj)) {
      newObj[key] = getCleanRequestBody((obj as any)[key]);
    }
    return newObj;
  }

  return obj;
};

const dateRegex = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;

/** A copy of `value` with each ISO date string in it turned into a `Date`. */
const convertToDate = (value: unknown): unknown => {
  if (typeof value === 'string') {
    return dateRegex.test(value) ? new Date(value) : value;
  }

  if (Array.isArray(value)) {
    return value.map((item) => convertToDate(item));
  }

  if (value !== null && typeof value === 'object') {
    const newObj: Record<string, unknown> = {};
    for (const key of Object.keys(value)) {
      newObj[key] = convertToDate((value as any)[key]);
    }
    return newObj;
  }

  return value;
};
