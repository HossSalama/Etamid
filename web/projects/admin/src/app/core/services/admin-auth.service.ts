import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { Admin, AuthResult, LoginResult } from '../models/admin.models';

const ACCESS_TOKEN_KEY  = 'etamid_admin_access_token';
const REFRESH_TOKEN_KEY = 'etamid_admin_refresh_token';
const USER_KEY          = 'etamid_admin_user';

@Injectable({ providedIn: 'root' })
export class AdminAuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/admin/api/auth';

  private readonly _currentAdmin = signal<Admin | null>(this.loadStoredAdmin());
  readonly currentAdmin = this._currentAdmin.asReadonly();
  readonly isAuthenticated = computed(() => !!this._currentAdmin());

  /** POST /admin/api/auth/login */
  login(email: string, password: string): Observable<ApiResponse<LoginResult>> {
    return this.http
      .post<ApiResponse<LoginResult>>(`${this.baseUrl}/login`, { email, password })
      .pipe(
        tap((res) => {
          if (res.success && res.data && !res.data.twoFactorRequired && res.data.auth) {
            this.handleAuthSuccess(res.data.auth);
          }
        }),
      );
  }

  /** POST /admin/api/auth/2fa/verify */
  verify2fa(challengeToken: string, code: string): Observable<ApiResponse<AuthResult>> {
    return this.http
      .post<ApiResponse<AuthResult>>(`${this.baseUrl}/2fa/verify`, { challengeToken, code })
      .pipe(
        tap((res) => {
          if (res.success && res.data) {
            this.handleAuthSuccess(res.data);
          }
        }),
      );
  }

  /** POST /admin/api/auth/2fa/setup */
  setup2fa(): Observable<ApiResponse<{ secret: string; otpAuthUri: string }>> {
    return this.http.post<ApiResponse<{ secret: string; otpAuthUri: string }>>(
      `${this.baseUrl}/2fa/setup`,
      {},
    );
  }

  /** POST /admin/api/auth/2fa/enable */
  enable2fa(code: string): Observable<ApiResponse<Admin>> {
    return this.http.post<ApiResponse<Admin>>(`${this.baseUrl}/2fa/enable`, { code }).pipe(
      tap((res) => {
        if (res.success && res.data) {
          this.saveCurrentAdmin(res.data);
        }
      }),
    );
  }

  /** POST /admin/api/auth/2fa/disable */
  disable2fa(password: string, code: string): Observable<ApiResponse<Admin>> {
    return this.http.post<ApiResponse<Admin>>(`${this.baseUrl}/2fa/disable`, { password, code }).pipe(
      tap((res) => {
        if (res.success && res.data) {
          this.saveCurrentAdmin(res.data);
        }
      }),
    );
  }

  /** GET /admin/api/auth/me */
  me(): Observable<ApiResponse<Admin>> {
    return this.http.get<ApiResponse<Admin>>(`${this.baseUrl}/me`).pipe(
      tap((res) => {
        if (res.success && res.data) {
          this.saveCurrentAdmin(res.data);
        }
      }),
    );
  }

  /** POST /admin/api/auth/logout (best-effort) */
  logout(): void {
    this.http.post(`${this.baseUrl}/logout`, {}).subscribe({ error: () => {} });
    this.clearAuth();
  }

  /** POST /admin/api/auth/logout-all then clear local session */
  logoutAll(): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/logout-all`, {}).pipe(
      finalize(() => this.clearAuth()),
    );
  }

  /**
   * POST /admin/api/auth/refresh — concurrent callers share one in-flight
   * request via shareReplay(1). Returns true when a new access token was stored.
   */
  private refreshInFlight: Observable<boolean> | null = null;

  refreshAccessToken(): Observable<boolean> {
    if (this.refreshInFlight) return this.refreshInFlight;
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) return of(false);

    const request$ = this.http
      .post<ApiResponse<AuthResult>>(`${this.baseUrl}/refresh`, { refreshToken })
      .pipe(
        tap((res) => {
          if (res.success && res.data) {
            if (res.data.accessToken) {
              localStorage.setItem(ACCESS_TOKEN_KEY, res.data.accessToken);
            }
            if (res.data.refreshToken) {
              localStorage.setItem(REFRESH_TOKEN_KEY, res.data.refreshToken);
            }
            if (res.data.admin) {
              this.saveCurrentAdmin(res.data.admin);
            }
          }
        }),
        map((res) => !!(res.success && res.data?.accessToken)),
        catchError(() => of(false)),
        finalize(() => {
          this.refreshInFlight = null;
        }),
        shareReplay(1),
      );

    this.refreshInFlight = request$;
    return request$;
  }

  hasPermission(permission: string): boolean {
    return this._currentAdmin()?.permissions?.includes(permission) ?? false;
  }

  isSuperAdmin(): boolean {
    return this._currentAdmin()?.roles?.includes('super-admin') ?? false;
  }

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  private handleAuthSuccess(auth: AuthResult): void {
    if (auth.accessToken)  localStorage.setItem(ACCESS_TOKEN_KEY, auth.accessToken);
    if (auth.refreshToken) localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken);
    if (auth.admin)        this.saveCurrentAdmin(auth.admin);
  }

  private saveCurrentAdmin(admin: Admin): void {
    localStorage.setItem(USER_KEY, JSON.stringify(admin));
    this._currentAdmin.set(admin);
  }

  clearAuth(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._currentAdmin.set(null);
  }

  private loadStoredAdmin(): Admin | null {
    try {
      const stored = localStorage.getItem(USER_KEY);
      return stored ? (JSON.parse(stored) as Admin) : null;
    } catch {
      return null;
    }
  }
}
