import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import {
  AuthTokens,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  RegisterCompanyRequest,
  RegisterIndividualRequest,
  RegisterResponse,
  ResetPasswordRequest,
  ResetTokenResponse,
  UpdateProfileRequest,
  VerifyResetCodeRequest,
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

  resendOtp(req: ResendOtpRequest): Observable<ApiResponse<null>> {
    return this.http.post<ApiResponse<null>>(`${this.baseUrl}/otp/resend`, req);
  }

  forgotPassword(req: ForgotPasswordRequest): Observable<ApiResponse<null>> {
    return this.http.post<ApiResponse<null>>(`${this.baseUrl}/password/forgot`, req);
  }

  verifyResetCode(req: VerifyResetCodeRequest): Observable<ApiResponse<ResetTokenResponse>> {
    return this.http.post<ApiResponse<ResetTokenResponse>>(`${this.baseUrl}/password/verify-otp`, req);
  }

  resetPassword(req: ResetPasswordRequest): Observable<ApiResponse<null>> {
    return this.http.post<ApiResponse<null>>(`${this.baseUrl}/password/reset`, req);
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

  refreshProfile(): Observable<ApiResponse<UserProfile>> {
    return this.http.get<ApiResponse<UserProfile>>('/api/v1/me').pipe(
      tap((res) => {
        if (res.success && res.data) this.saveCurrentUser(res.data);
      })
    );
  }

  updateProfile(request: UpdateProfileRequest): Observable<ApiResponse<null>> {
    return this.http.put<ApiResponse<null>>('/api/v1/me', request).pipe(
      tap((res) => {
        const user = this._currentUser();
        if (res.success && user) {
          this.saveCurrentUser({
            ...user,
            name: request.username.trim(),
            email: request.email.trim().toLowerCase(),
          });
        }
      })
    );
  }

  uploadAvatar(file: File): Observable<ApiResponse<{ avatarUrl: string }>> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<ApiResponse<{ avatarUrl: string }>>('/api/v1/me/avatar', formData).pipe(
      tap((res) => {
        const user = this._currentUser();
        if (res.success && res.data?.avatarUrl && user) {
          this.saveCurrentUser({ ...user, avatarUrl: res.data.avatarUrl });
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

  private saveCurrentUser(user: UserProfile): void {
    localStorage.setItem('etamid_user', JSON.stringify(user));
    this._currentUser.set(user);
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
