import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import {
  Admin,
  AuditLog,
  Dispute,
  Penalty,
  Permission,
  Role,
  Withdrawal,
} from '../models/admin.models';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);

  // ── Admins ──────────────────────────────────────────────────────────────────

  listAdmins(params: {
    status?: string;
    page?: number;
    pageSize?: number;
  }): Observable<ApiResponse<Admin[]>> {
    const q = this.toParams(params);
    return this.http.get<ApiResponse<Admin[]>>(`/admin/api/admins${q}`);
  }

  createAdmin(body: {
    name: string;
    email: string;
    password: string;
    roleIds?: number[];
  }): Observable<ApiResponse<Admin>> {
    return this.http.post<ApiResponse<Admin>>('/admin/api/admins', body);
  }

  getAdmin(id: number): Observable<ApiResponse<Admin>> {
    return this.http.get<ApiResponse<Admin>>(`/admin/api/admins/${id}`);
  }

  updateAdmin(id: number, body: { name?: string; email?: string }): Observable<ApiResponse<Admin>> {
    return this.http.patch<ApiResponse<Admin>>(`/admin/api/admins/${id}`, body);
  }

  disableAdmin(id: number): Observable<ApiResponse<Admin>> {
    return this.http.post<ApiResponse<Admin>>(`/admin/api/admins/${id}/disable`, {});
  }

  enableAdmin(id: number): Observable<ApiResponse<Admin>> {
    return this.http.post<ApiResponse<Admin>>(`/admin/api/admins/${id}/enable`, {});
  }

  reset2faAdmin(id: number): Observable<ApiResponse<Admin>> {
    return this.http.post<ApiResponse<Admin>>(`/admin/api/admins/${id}/reset-2fa`, {});
  }

  setAdminRoles(id: number, roleIds: number[]): Observable<ApiResponse<Admin>> {
    return this.http.post<ApiResponse<Admin>>(`/admin/api/admins/${id}/roles`, { roleIds });
  }

  // ── Roles ───────────────────────────────────────────────────────────────────

  listRoles(): Observable<ApiResponse<Role[]>> {
    return this.http.get<ApiResponse<Role[]>>('/admin/api/roles');
  }

  createRole(body: { name: string; description?: string }): Observable<ApiResponse<Role>> {
    return this.http.post<ApiResponse<Role>>('/admin/api/roles', body);
  }

  getRole(id: number): Observable<ApiResponse<Role>> {
    return this.http.get<ApiResponse<Role>>(`/admin/api/roles/${id}`);
  }

  updateRole(id: number, body: { name?: string; description?: string }): Observable<ApiResponse<Role>> {
    return this.http.patch<ApiResponse<Role>>(`/admin/api/roles/${id}`, body);
  }

  deleteRole(id: number): Observable<ApiResponse<null>> {
    return this.http.delete<ApiResponse<null>>(`/admin/api/roles/${id}`);
  }

  setRolePermissions(id: number, permissions: string[]): Observable<ApiResponse<Role>> {
    return this.http.post<ApiResponse<Role>>(`/admin/api/roles/${id}/permissions`, { permissions });
  }

  // ── Permissions ─────────────────────────────────────────────────────────────

  listPermissions(): Observable<ApiResponse<Permission[]>> {
    return this.http.get<ApiResponse<Permission[]>>('/admin/api/permissions');
  }

  // ── Audit Log ───────────────────────────────────────────────────────────────

  listAuditLogs(params: {
    action?: string;
    entityType?: string;
    actorId?: number;
    page?: number;
    pageSize?: number;
  }): Observable<ApiResponse<AuditLog[]>> {
    const q = this.toParams(params);
    return this.http.get<ApiResponse<AuditLog[]>>(`/admin/api/audit-logs${q}`);
  }

  // ── Withdrawals (operator — /api/v1/admin paths, frozen mobile contract) ────

  listWithdrawals(params: {
    status?: string;
    page?: number;
    pageSize?: number;
  }): Observable<ApiResponse<Withdrawal[]>> {
    const q = this.toParams(params);
    return this.http.get<ApiResponse<Withdrawal[]>>(`/api/v1/admin/withdrawals${q}`);
  }

  approveWithdrawal(id: number): Observable<ApiResponse<Withdrawal>> {
    return this.http.post<ApiResponse<Withdrawal>>(`/api/v1/admin/withdrawals/${id}/approve`, {});
  }

  rejectWithdrawal(id: number, reason: string): Observable<ApiResponse<Withdrawal>> {
    return this.http.post<ApiResponse<Withdrawal>>(`/api/v1/admin/withdrawals/${id}/reject`, {
      reason,
    });
  }

  completeWithdrawal(id: number, bankReference: string): Observable<ApiResponse<Withdrawal>> {
    return this.http.post<ApiResponse<Withdrawal>>(`/api/v1/admin/withdrawals/${id}/complete`, {
      bankReference,
    });
  }

  // ── Penalties (operator — /api/v1/admin paths) ───────────────────────────────

  listPenalties(params?: {
    page?: number;
    pageSize?: number;
  }): Observable<ApiResponse<Penalty[]>> {
    const q = this.toParams(params ?? {});
    return this.http.get<ApiResponse<Penalty[]>>(`/api/v1/admin/company-deals/penalties${q}`);
  }

  waivePenalty(penaltyId: number, reason: string): Observable<ApiResponse<Penalty>> {
    return this.http.post<ApiResponse<Penalty>>(
      `/api/v1/admin/company-deals/penalties/${penaltyId}/waive`,
      { reason },
    );
  }

  // ── Disputes (operator — /api/v1/admin paths) ────────────────────────────────

  listDisputes(params?: {
    page?: number;
    pageSize?: number;
  }): Observable<ApiResponse<Dispute[]>> {
    const q = this.toParams(params ?? {});
    return this.http.get<ApiResponse<Dispute[]>>(`/api/v1/admin/disputes${q}`);
  }

  requestDisputeInfo(
    disputeId: number,
    body: { note?: string; deadlineDays: number },
  ): Observable<ApiResponse<Dispute>> {
    return this.http.post<ApiResponse<Dispute>>(
      `/api/v1/admin/disputes/${disputeId}/request-info`,
      body,
    );
  }

  decideDispute(
    disputeId: number,
    body: {
      resolution: 'ReleaseSeller' | 'RefundBuyer' | 'Partial';
      sellerPercent?: number;
      note: string;
    },
  ): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(
      `/api/v1/admin/disputes/${disputeId}/decide`,
      body,
    );
  }

  // ── Utility ─────────────────────────────────────────────────────────────────

  private toParams(obj: Record<string, unknown>): string {
    const entries = Object.entries(obj).filter(
      ([, v]) => v !== undefined && v !== null && v !== '',
    );
    if (!entries.length) return '';
    const qs = entries.map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
    return '?' + qs.join('&');
  }
}
