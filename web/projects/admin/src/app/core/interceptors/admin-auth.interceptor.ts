import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError, map } from 'rxjs';
import { AdminAuthService } from '../services/admin-auth.service';
import { AdminLanguageService } from '../services/admin-language.service';

/**
 * Admin auth interceptor — dual-mode:
 *
 * Current branch (cycle9_Admin @ b6268e8):
 *   API errors arrive as a real HTTP 4xx status.
 *   On real HTTP 401 from a non-auth endpoint → refresh once and retry.
 *
 * Forward-compat (after rebase onto main @ b74e02a):
 *   API errors arrive as HTTP 200 with { success: false } in the body.
 *   Convert those to HttpErrorResponse(status 200) so components still read
 *   err.error?.errors / err.error?.message.
 *
 * Dead-session codes: TOKEN_EXPIRED | TOKEN_INVALID | UNAUTHORIZED.
 * Auth endpoints (/admin/api/auth/*) are NEVER retried.
 */
export const adminAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AdminAuthService);
  const langService = inject(AdminLanguageService);
  const router = inject(Router);

  const token = authService.getAccessToken();

  let headers = req.headers.set('Accept-Language', langService.currentLang());
  if (token && !req.headers.has('Authorization')) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  const cloned = req.clone({ headers });

  const isAuthEndpoint = (url: string) => url.includes('/admin/api/auth/');

  const DEAD_SESSION_CODES = new Set(['TOKEN_EXPIRED', 'TOKEN_INVALID', 'UNAUTHORIZED']);

  const isDeadSession = (body: unknown): boolean => {
    if (!body || typeof body !== 'object') return false;
    const b = body as Record<string, unknown>;
    if (Array.isArray(b['errors'])) {
      const errs = b['errors'] as Array<{ code?: string }>;
      return errs.some((e) => e.code && DEAD_SESSION_CODES.has(e.code));
    }
    return false;
  };

  const handleDeadSession = (err: unknown) => {
    authService.clearAuth();
    router.navigate(['/login']);
    return throwError(() => err);
  };

  return next(cloned).pipe(
    // ── Forward-compat: convert 200 error envelopes ──────────────────────────
    map((event) => {
      if (!(event instanceof HttpResponse)) return event;
      const body = event.body as Record<string, unknown> | null;
      if (body && body['success'] === false) {
        throw new HttpErrorResponse({
          error: body,
          status: event.status,
          statusText: event.statusText,
          url: event.url ?? undefined,
        });
      }
      return event;
    }),
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse)) return throwError(() => err);

      // ── 200-envelope dead-session (after rebase) ─────────────────────────
      if (err.status === 200 && isDeadSession(err.error)) {
        if (isAuthEndpoint(req.url)) return throwError(() => err);
        if (!authService.getRefreshToken()) return handleDeadSession(err);
        return authService.refreshAccessToken().pipe(
          switchMap((ok) => {
            if (!ok) return handleDeadSession(err);
            const newToken = authService.getAccessToken();
            const retry = cloned.clone({
              headers: cloned.headers.set('Authorization', `Bearer ${newToken ?? ''}`),
            });
            return next(retry);
          }),
        );
      }

      // ── Real HTTP 401 (current branch behaviour) ─────────────────────────
      if (err.status === 401) {
        if (isAuthEndpoint(req.url)) return throwError(() => err);
        if (!authService.getRefreshToken()) return handleDeadSession(err);
        return authService.refreshAccessToken().pipe(
          switchMap((ok) => {
            if (!ok) return handleDeadSession(err);
            const newToken = authService.getAccessToken();
            const retry = cloned.clone({
              headers: cloned.headers.set('Authorization', `Bearer ${newToken ?? ''}`),
            });
            return next(retry);
          }),
        );
      }

      // ── Everything else — rethrow ────────────────────────────────────────
      return throwError(() => err);
    }),
  );
};
