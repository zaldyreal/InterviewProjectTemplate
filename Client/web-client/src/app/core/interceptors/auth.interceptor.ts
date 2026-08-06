import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';

/**
 * Attaches the admin bearer token to API requests and signs the admin out if the API rejects it.
 *
 * Centralising this means no component has to remember to set the header, and an expired token
 * produces a redirect to the login page rather than an unexplained empty admin table.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const token = authService.getAccessToken();

  const authorised = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorised).pipe(
    catchError((error: unknown) => {
      const isRejectedAuth =
        error instanceof HttpErrorResponse && (error.status === 401 || error.status === 403);

      // Only react when a token was actually sent: a 401 from the login endpoint itself is a wrong
      // password, which the login form reports on its own.
      if (isRejectedAuth && token) {
        authService.logout();
        void router.navigate(['/admin/login'], { queryParams: { expired: true } });
      }

      return throwError(() => error);
    })
  );
};
