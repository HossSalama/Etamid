import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import {
  AuthTokens,
  LoginRequest,
  LoginResponse,
  RegisterCompanyRequest,
  RegisterIndividualRequest,
  RegisterResponse,
  UserProfile,
  VerifyOtpRequest,
  ResendOtpRequest,
} from '../models/auth.models';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/auth';

  private readonly _currentUser = signal<UserProfile | null>(this.loadStoredUser());
  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => !!this._currentUser());

  registerIndividual(req: RegisterIndividualRequest): Observable<ApiResponse<RegisterResponse>> {
    return this.http.post<ApiResponse<RegisterResponse>>(`${this.baseUrl}/register/individual`, req);
  }

  registerCompany(req: RegisterCompanyRequest): Observable<ApiResponse<RegisterResponse>> {
    return this.http.post<ApiResponse<RegisterResponse>>(`${this.baseUrl}/register/company`, req);
  }

  verifyOtp(req: VerifyOtpRequest): Observable<ApiResponse<LoginResponse>> {
    return this.http.post<ApiResponse<LoginResponse>>(`${this.baseUrl}/otp/verify`, req).pipe(
      tap((res) => {
        if (res.success && res.data) {
          this.handleAuthSuccess(res.data);
        }
      })
    );
  }

  resendOtp(req: ResendOtpRequest): Observable<ApiResponse<{ resendAfterSeconds: number }>> {
    return this.http.post<ApiResponse<{ resendAfterSeconds: number }>>(`${this.baseUrl}/otp/resend`, req);
  }

  login(req: LoginRequest): Observable<ApiResponse<LoginResponse>> {
    return this.http.post<ApiResponse<LoginResponse>>(`${this.baseUrl}/login`, req).pipe(
      tap((res) => {
        if (res.success && res.data) {
          this.handleAuthSuccess(res.data);
        }
      })
    );
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post(`${this.baseUrl}/logout`, { refreshToken }).subscribe({
        error: () => {},
      });
    }
    this.clearAuth();
  }

  getAccessToken(): string | null {
    return localStorage.getItem('etamid_access_token');
  }

  getRefreshToken(): string | null {
    return localStorage.getItem('etamid_refresh_token');
  }

  private handleAuthSuccess(response: any): void {
    const accessToken = response.tokens?.accessToken || response.accessToken;
    const refreshToken = response.tokens?.refreshToken || response.refreshToken;
    const user = response.user;

    if (accessToken) localStorage.setItem('etamid_access_token', accessToken);
    if (refreshToken) localStorage.setItem('etamid_refresh_token', refreshToken);
    if (user) {
      localStorage.setItem('etamid_user', JSON.stringify(user));
      this._currentUser.set(user);
    }
  }

  private clearAuth(): void {
    localStorage.removeItem('etamid_access_token');
    localStorage.removeItem('etamid_refresh_token');
    localStorage.removeItem('etamid_user');
    this._currentUser.set(null);
  }

  private loadStoredUser(): UserProfile | null {
    try {
      const stored = localStorage.getItem('etamid_user');
      return stored ? JSON.parse(stored) : null;
    } catch {
      return null;
    }
  }
}
