import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { LanguageService } from '../services/language.service';

/**
 * Attaches the access token and recovers from an expired session: on a 401
 * from a non-auth endpoint, it rotates the session via the stored refresh
 * token and retries the original request once with the new token. If the
 * refresh fails (or there is no refresh token), the session is cleared and
 * the user is sent to the login page instead of staying "authenticated"
 * with a dead token. Auth endpoints are never retried, so the refresh call
 * itself cannot loop.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const langService = inject(LanguageService);
  const router = inject(Router);
  const token = authService.getAccessToken();

  let headers = req.headers.set('Accept-Language', langService.currentLang());

  if (token && !req.headers.has('Authorization')) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  const cloned = req.clone({ headers });
  return next(cloned).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse) || err.status !== 401) {
        return throwError(() => err);
      }
      if (req.url.includes('/api/v1/auth/')) {
        return throwError(() => err);
      }
      if (!authService.getRefreshToken()) {
        authService.logout();
        router.navigate(['/auth/login']);
        return throwError(() => err);
      }
      return authService.refreshAccessToken().pipe(
        switchMap((ok) => {
          if (!ok) {
            authService.logout();
            router.navigate(['/auth/login']);
            return throwError(() => err);
          }
          const newToken = authService.getAccessToken();
          const retry = cloned.clone({
            headers: cloned.headers.set('Authorization', `Bearer ${newToken ?? ''}`),
          });
          return next(retry);
        })
      );
    })
  );
};
