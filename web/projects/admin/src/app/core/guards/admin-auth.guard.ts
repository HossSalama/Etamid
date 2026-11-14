import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { catchError, map, Observable, of, switchMap } from 'rxjs';
import { AdminAuthService } from '../services/admin-auth.service';

/**
 * Route guard — requires a valid admin session.
 * If a token exists in localStorage, calls GET /admin/api/auth/me to validate it.
 *
 * Two fixes over the first version:
 * 1. Returns a UrlTree instead of calling router.navigate() imperatively.
 *    An imperative navigate issued from inside a guard — while a navigation
 *    is already in progress — can be dropped by the router, which leaves the
 *    outlet empty: a blank white page at the same URL. A returned UrlTree is
 *    handled by the router itself, so the redirect always lands on /login.
 * 2. Retries /me once after a token refresh. The admin access token lives
 *    15 minutes, and the interceptor never refreshes for /me (it is an auth
 *    endpoint), so without this every hard refresh after 15 minutes logged
 *    the admin out. A failed refresh still ends at /login.
 */
export const adminAuthGuard: CanActivateFn = (): Observable<boolean | UrlTree> => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);
  const loginTree = (): UrlTree => router.createUrlTree(['/login']);

  if (!authService.getAccessToken()) return of(loginTree());

  // Validate the token against the server and load fresh permissions.
  const validate = (): Observable<boolean | UrlTree> =>
    authService.me().pipe(map((res) => (res.success ? true : loginTree())));

  return validate().pipe(
    catchError(() =>
      authService.refreshAccessToken().pipe(
        switchMap((ok) => {
          if (!ok) {
            authService.clearAuth();
            return of(loginTree());
          }
          return validate();
        }),
        catchError(() => {
          authService.clearAuth();
          return of(loginTree());
        }),
      ),
    ),
  );
};
