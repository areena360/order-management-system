import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { environment } from '../../environments/environment';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(`${environment.apiUrl}/`)) return next(req);
  const auth = inject(AuthService);
  const path = req.url.split('?')[0];
  if (['login', 'register', 'refresh', 'logout', 'forgot-password', 'reset-password']
      .some(action => path === `${environment.apiUrl}/auth/${action}`)) {
    return next(req.clone({ withCredentials: true }));
  }
  const send = (token: string | null) => next(token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
  return auth.getValidToken().pipe(switchMap(token => send(token).pipe(
    catchError(err => {
      if (err.status !== 401 || !token) return throwError(() => err);
      const current = auth.getToken();
      const retry = current && current !== token ? send(current)
        : auth.refreshSession().pipe(switchMap(res => send(res.token)));
      return retry.pipe(catchError(retryError => {
        if (retryError.status === 401 && auth.getToken()) auth.expireSession();
        return throwError(() => retryError);
      }));
    })
  )));
};
