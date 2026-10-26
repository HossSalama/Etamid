import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { LanguageService } from '../services/language.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const langService = inject(LanguageService);
  const token = authService.getAccessToken();

  let headers = req.headers.set('Accept-Language', langService.currentLang());

  if (token && !req.headers.has('Authorization')) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  const cloned = req.clone({ headers });
  return next(cloned);
};
