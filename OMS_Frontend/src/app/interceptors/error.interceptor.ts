import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { catchError, throwError } from 'rxjs';
import { ApiErrorResponse } from '../models/api-error.model';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toastr = inject(ToastrService);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const error: ApiErrorResponse | null = err.error ?? null;

      const title = error?.title || 'Something went wrong. Please try again.';
      const errorCode = error?.errorCode;

      switch (err.status) {
        case 0:
          toastr.error('Cannot reach server. Check your connection.', 'Network Error');
          break;

        case 400:
        case 422:
          if (error?.errors) {
            const messages = Object.values(error.errors).flat();
            messages.forEach(msg => toastr.warning(msg, 'Validation Error'));
          } else {
            toastr.warning(title, 'Validation Error');
          }
          break;

        case 401:
          toastr.error(title, 'Unauthorized');
          // Optional: redirect to login here via Router if needed
          break;

        case 403:
          toastr.error(title, 'Access Denied');
          break;

        case 404:
          toastr.info(title, 'Not Found');
          break;

        case 409:
          toastr.warning(title, 'Conflict');
          break;

        case 500:
        default:
          toastr.error(title, 'Server Error');
          break;
      }

      if (errorCode) {
        console.error(`[${errorCode}] TraceId: ${error?.traceId}`, err);
      }

      return throwError(() => err);
    })
  );
};