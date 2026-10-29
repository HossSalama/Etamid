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
  email: string;
  verificationMethod: string;
  verificationRequired: boolean;
}

export interface VerifyOtpRequest {
  phone: string;
  code: string;
}

export interface ResendOtpRequest {
  phone: string;
}

export interface ForgotPasswordRequest {
  phone: string;
}

export interface VerifyResetCodeRequest {
  phone: string;
  code: string;
}

export interface ResetPasswordRequest {
  resetToken: string;
  newPassword: string;
  confirmPassword: string;
}

export interface ResetTokenResponse {
  resetToken: string;
  expiresInSeconds: number;
}

export interface LoginRequest {
  phone: string;
  password: string;
}

export interface UpdateProfileRequest {
  username: string;
  email: string;
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
  emailVerified?: boolean;
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
