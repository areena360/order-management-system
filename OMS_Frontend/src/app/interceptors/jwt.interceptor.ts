import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';

// Built-in functional interceptor style (Angular 15+/20) — no class boilerplate needed
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(req);
};

// Register in app.config.ts:
// providers: [
//   provideHttpClient(withInterceptors([jwtInterceptor])),
// ]