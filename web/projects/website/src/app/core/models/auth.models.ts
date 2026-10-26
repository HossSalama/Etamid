export interface RegisterIndividualRequest {
  username: string;
  phone: string;
  email: string;
  password: string;
  acceptTerms: boolean;
}

export interface RegisterCompanyRequest {
  companyName: string;
  ownerPhone: string;
  commercialRegNo: string;
  taxNo?: string;
  email: string;
  password: string;
  acceptTerms: boolean;
}

export interface RegisterResponse {
  phone: string;
  resendAfterSeconds: number;
  expiresInSeconds: number;
}

export interface VerifyOtpRequest {
  phone?: string;
  email?: string;
  code: string;
}

export interface ResendOtpRequest {
  phone?: string;
  email?: string;
  purpose?: 'register' | 'password_reset' | 'login';
}

export interface LoginRequest {
  phone: string;
  password: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface UserProfile {
  id: number;
  code: string;
  name: string;
  phone: string;
  email: string;
  type: 'individual' | 'company';
  language: string;
  phoneVerified: boolean;
  nafathVerified: boolean;
  avatarUrl?: string;
}

export interface LoginResponse {
  tokens?: AuthTokens;
  accessToken?: string;
  refreshToken?: string;
  expiresInSeconds?: number;
  user: UserProfile;
}
