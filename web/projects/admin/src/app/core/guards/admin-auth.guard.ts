import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AdminAuthService } from '../services/admin-auth.service';

/**
 * Route guard — requires a valid admin session.
 * If a token exists in localStorage, calls GET /admin/api/auth/me to validate it.
 * On failure (or no token) navigates to /login and denies access.
 */
export const adminAuthGuard: CanActivateFn = () => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);

  if (!authService.getAccessToken()) {
    router.navigate(['/login']);
    return false;
  }

  // Validate the token against the server and load fresh permissions
  return authService.me().pipe(
    map((res) => {
      if (res.success) return true;
      router.navigate(['/login']);
      return false;
    }),
    catchError(() => {
      authService.clearAuth();
      router.navigate(['/login']);
      return of(false);
    }),
  );
};
