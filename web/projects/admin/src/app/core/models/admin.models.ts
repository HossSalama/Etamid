/** Admin user returned by GET /admin/api/auth/me and embedded in AuthResult */
export interface Admin {
  id: number;
  name: string;
  email: string;
  status: 'active' | 'disabled';
  twoFactorEnabled: boolean;
  lastLoginAt: string | null;
  roles: string[];
  permissions: string[];
}

/** Returned by /admin/api/auth/login (when 2FA not required) and /admin/api/auth/2fa/verify */
export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
  admin: Admin;
}

/** Returned by POST /admin/api/auth/login */
export interface LoginResult {
  twoFactorRequired: boolean;
  challenge: { challengeToken: string; expiresInSeconds: number } | null;
  auth: AuthResult | null;
}

/** Role from /admin/api/roles */
export interface Role {
  id: number;
  name: string;
  description: string | null;
  permissions: string[];
}

/** Permission code from GET /admin/api/permissions */
export interface Permission {
  id: number;
  code: string;
  description: string;
}

/** Audit log entry from GET /admin/api/audit-logs */
export interface AuditLog {
  id: number;
  actorId: number | null;
  actorName: string | null;
  action: string;
  entityType: string | null;
  entityId: number | null;
  before: unknown;
  after: unknown;
  ip: string | null;
  createdAt: string;
}

/** Withdrawal request from GET /api/v1/admin/withdrawals */
export interface Withdrawal {
  id: number;
  userId: number;
  userName: string;
  amount: number;
  status: 'pending' | 'approved' | 'rejected' | 'completed';
  bankName: string | null;
  iban: string | null;
  reason: string | null;
  bankReference: string | null;
  approvedByAdminId: number | null;
  createdAt: string;
  updatedAt: string;
}

/** Penalty from GET /api/v1/admin/company-deals/penalties */
export interface Penalty {
  id: number;
  companyDealId: number;
  installmentId: number | null;
  amount: number;
  reason: string | null;
  waivedByAdminId: number | null;
  waivedAt: string | null;
  waivedReason: string | null;
  createdAt: string;
}

/** User counts from GET /admin/api/users/counts */
export interface UserCounts {
  sellers: number;
  buyers: number;
  companies: number;
  individuals: number;
}

/** Deal item from GET /admin/api/deals */
export interface Deal {
  id: number;
  number: string;
  title: string;
  amount: number;
  currency: string;
  status: string;
  buyerId: number;
  buyerName: string;
  sellerId: number;
  sellerName: string;
  createdAt: string;
}

/** Dispute from GET /api/v1/admin/disputes */
export interface Dispute {
  id: number;
  dealId: number;
  openedByUserId: number;
  againstUserId: number;
  status: 'under_review' | 'awaiting_info' | 'resolved_buyer' | 'resolved_seller' | 'closed';
  resolution: 'RefundBuyer' | 'ReleaseSeller' | 'Partial' | null;
  sellerPercent: number | null;
  reason: string | null;
  details: string | null;
  note: string | null;
  infoDeadline: string | null;
  resolvedByAdminId: number | null;
  createdAt: string;
  updatedAt: string;
}
