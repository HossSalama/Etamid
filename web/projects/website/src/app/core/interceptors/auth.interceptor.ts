import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, of, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { LanguageService } from '../services/language.service';

/**
 * Attaches the access token and recovers from an expired session.
 *
 * Since 2026-11-09 every API error returns HTTP 200 with the error in the
 * envelope ({ success: false, errors: [{ code, message }] }), so a failed
 * call no longer arrives as an HttpErrorResponse. This interceptor converts
 * a 200 response with success === false back into a thrown HttpErrorResponse
 * (error = the envelope body), which is exactly what every component's
 * `error:` handler already reads (err.error?.errors / err.error?.message) —
 * so no component changes when the API changes its transport status.
 *
 * A body error whose code means "the session is dead" (TOKEN_EXPIRED from
 * an expired token, TOKEN_INVALID, or UNAUTHORIZED from a rejected token)
 * on a non-auth endpoint rotates the session via the stored refresh token
 * and retries the original request once with the new token, exactly like
 * the old 401 handling. If the refresh fails (or there is no refresh
 * token), the session is cleared and the user is sent to the login page.
 * Auth endpoints are never retried, so the refresh call itself cannot loop.
 * A real HttpErrorResponse 401 is still handled the same way as a fallback
 * (e.g. if a proxy ever returns one).
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
    switchMap((event: HttpEvent<unknown>) =>
      convertResponse(event, req, cloned, next, authService, router, true)
    ),
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
          return next(retry).pipe(
            switchMap((event: HttpEvent<unknown>) =>
              convertResponse(event, req, cloned, next, authService, router, false)
            )
          );
        })
      );
    })
  );
};

/**
 * Passes a response through, except a 200 error envelope, which becomes a
 * thrown HttpErrorResponse — unless it is a dead-session code on a
 * retryable call, in which case the session is refreshed once and the
 * request is retried (the retried response is converted too, but with
 * refresh disabled so a second dead-session error just throws).
 */
function convertResponse(
  event: HttpEvent<unknown>,
  req: HttpRequest<unknown>,
  cloned: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router,
  allowRefresh: boolean
): Observable<HttpEvent<unknown>> {
  if (!(event instanceof HttpResponse) || !isEnvelopeError(event.body)) {
    return of(event);
  }
  const code = errorCode(event.body);
  if (code && isSessionDeadCode(code) && allowRefresh && !req.url.includes('/api/v1/auth/')) {
    return refreshAndRetry(req, cloned, next, authService, router, event.body);
  }
  return throwError(() => toHttpError(event.body, req.url));
}

function refreshAndRetry(
  req: HttpRequest<unknown>,
  cloned: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router,
  body: unknown
): Observable<HttpEvent<unknown>> {
  if (!authService.getRefreshToken()) {
    authService.logout();
    router.navigate(['/auth/login']);
    return throwError(() => toHttpError(body, req.url));
  }
  return authService.refreshAccessToken().pipe(
    switchMap((ok) => {
      if (!ok) {
        authService.logout();
        router.navigate(['/auth/login']);
        return throwError(() => toHttpError(body, req.url));
      }
      const newToken = authService.getAccessToken();
      const retry = cloned.clone({
        headers: cloned.headers.set('Authorization', `Bearer ${newToken ?? ''}`),
      });
      return next(retry).pipe(
        switchMap((event: HttpEvent<unknown>) =>
          convertResponse(event, req, cloned, next, authService, router, false)
        )
      );
    })
  );
}

/** The API's error envelope: { success: false, message, errors: [{ code, ... }] }. */
function isEnvelopeError(body: unknown): body is { success: false; message: string; errors?: Array<{ code: string }> } {
  return !!body && typeof body === 'object' && (body as { success?: unknown }).success === false;
}

function errorCode(body: { errors?: Array<{ code: string }> }): string | undefined {
  return body.errors?.[0]?.code;
}

/**
 * Codes that mean the access token is dead: TOKEN_EXPIRED (expired token,
 * from the API's status-code-pages mapping), UNAUTHORIZED (a token the
 * JwtBearer challenge rejected), and TOKEN_INVALID (a token the backend
 * itself rejected). Any of them on a non-auth call means "try the refresh
 * token once", exactly like the old HTTP 401.
 */
function isSessionDeadCode(code: string): boolean {
  return code === 'TOKEN_EXPIRED' || code === 'TOKEN_INVALID' || code === 'UNAUTHORIZED';
}

/** Wrap a 200 error envelope as the HttpErrorResponse the components already handle. */
function toHttpError(body: unknown, url: string): HttpErrorResponse {
  return new HttpErrorResponse({ error: body, status: 200, statusText: 'OK', url });
}
