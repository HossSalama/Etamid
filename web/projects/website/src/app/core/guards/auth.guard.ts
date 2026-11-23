import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Logged-in pages only (e.g. the registered Contact Us page). The user is
// restored from storage on reload, so isAuthenticated() is safe to read here.
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) return true;
  return inject(Router).createUrlTree(['/auth/login']);
};
