import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { AuthService } from './auth.service';

const PUBLIC_PATHS = ['/api/auth/login', '/api/auth/register', '/api/auth/refresh', '/api/auth/logout'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/') || PUBLIC_PATHS.some((path) => req.url.startsWith(path))) {
    return next(req);
  }

  const auth = inject(AuthService);
  const send = (token: string | null) =>
    next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);

  return from(auth.getAccessToken()).pipe(
    switchMap(send),
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) return throwError(() => error);

      // One retry with a fresh token. A second 401 is final, because the retry sits outside this catchError.
      return from(auth.refresh()).pipe(
        switchMap((refreshed) => {
          if (refreshed) return from(auth.getAccessToken()).pipe(switchMap(send));
          auth.endSession();
          return throwError(() => error);
        }),
      );
    }),
  );
};