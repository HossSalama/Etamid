import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';

export interface CreateDealRequest {
  title: string;
  sellerPhoneOrCode: string;
  description: string;
  amount: number;
  durationValue: number;
  durationUnit: 'hour' | 'day';
  needShipping: boolean;
  shippingCompanyId: number | null;
  shippingAddressId: number | null;
  acceptTerms: boolean;
}

export interface CreatedDeal {
  id: number;
  number: number;
  title: string;
  description: string;
  amount: number;
  currency: string;
  status: string;
  role: string;
  counterpartyName: string;
  counterpartyCode: string;
  durationValue: number;
  durationUnit: string;
  needShipping: boolean;
  shippingCompanyId: number | null;
  shippingAddressId: number | null;
  createdAt: string;
  respondDeadlineAt: string;
}

export interface DealSummary {
  id: number;
  number: number;
  title: string;
  amount: number;
  currency: string;
  status: string;
  role: string;
  counterpartyName: string;
  counterpartyCode: string;
  createdAt: string;
  availableActions: string[];
}

export interface FundSession {
  paymentId: string;
  dealId: number;
  status: string;
  amount: number;
  currency: string;
  provider: string;
  providerPaymentId: string;
  redirectUrl: string;
}

export interface LookupItem {
  id: number;
  code: string;
  name: string;
  iconUrl: string | null;
}

export interface DealEdit {
  id: number;
  dealId: number;
  proposedBy: string;
  status: string;
  prevAmount: number;
  prevDurationValue: number;
  prevDurationUnit: string;
  prevDescription: string;
  newAmount: number | null;
  newDurationValue: number | null;
  newDurationUnit: string | null;
  newDescription: string | null;
  createdAt: string;
  respondedAt: string | null;
}

export interface DealDetail {
  id: number;
  status: string;
  amount: number;
  durationValue: number;
  durationUnit: string;
  description: string;
  availableActions: string[];
  pendingEdit: DealEdit | null;
}

export interface Counterparty {
  name: string;
  code: string;
  storeName: string | null;
  avatarUrl: string | null;
  emailVerified: boolean;
  nafathVerified: boolean;
  rating: number;
  successfulDeals: number;
}

export interface ProposeEditRequest {
  amount?: number | null;
  durationValue?: number | null;
  durationUnit?: 'hour' | 'day' | null;
  description?: string | null;
}

export interface UserAddress {
  id: number;
  label: string | null;
  recipientName: string;
  recipientPhone: string;
  city: string;
  district: string | null;
  street: string | null;
  fullText: string;
  isDefault: boolean;
}

@Injectable({ providedIn: 'root' })
export class DealsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  createDeal(request: CreateDealRequest): Observable<ApiResponse<CreatedDeal>> {
    return this.http.post<ApiResponse<CreatedDeal>>(`${this.baseUrl}/deals`, request);
  }

  getMyDeals(page = 1, pageSize = 50): Observable<ApiResponse<DealSummary[]>> {
    return this.http.get<ApiResponse<DealSummary[]>>(`${this.baseUrl}/deals`, {
      params: { page, pageSize },
    });
  }

  acceptDeal(id: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/accept`, { acceptTerms: true });
  }

  rejectDeal(id: number, reasonId: number, note?: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/reject`, {
      reasonId,
      ...(note ? { note } : {}),
    });
  }

  cancelDeal(id: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/cancel`, null);
  }

  proposeEdit(id: number, request: ProposeEditRequest): Observable<ApiResponse<DealEdit>> {
    return this.http.post<ApiResponse<DealEdit>>(`${this.baseUrl}/deals/${id}/edits`, request);
  }

  approveEdit(id: number, editId: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/edits/${editId}/approve`, null);
  }

  rejectEdit(id: number, editId: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/edits/${editId}/reject`, null);
  }

  cancelEdit(id: number, editId: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/edits/${editId}/cancel`, null);
  }

  openDispute(id: number, reasonId: number, details: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/dispute`, { reasonId, details });
  }

  getDealDetail(id: number): Observable<ApiResponse<DealDetail>> {
    return this.http.get<ApiResponse<DealDetail>>(`${this.baseUrl}/deals/${id}`);
  }

  lookupSeller(q: string): Observable<ApiResponse<Counterparty>> {
    return this.http.get<ApiResponse<Counterparty>>(`${this.baseUrl}/users/lookup`, { params: { q } });
  }

  getLookups(type: string): Observable<ApiResponse<LookupItem[]>> {
    return this.http.get<ApiResponse<LookupItem[]>>(`${this.baseUrl}/lookups/${type}`);
  }

  fundDeal(id: number): Observable<ApiResponse<FundSession>> {
    return this.http.post<ApiResponse<FundSession>>(`${this.baseUrl}/deals/${id}/fund`, null, {
      headers: { 'X-Idempotency-Key': this.createIdempotencyKey() },
    });
  }

  completeFakePayment(id: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/payments/dev/deals/${id}/complete`, null);
  }

  deliverDeal(id: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/deliver`, null);
  }

  releaseEscrow(id: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/release`, null, {
      headers: { 'X-Idempotency-Key': this.createIdempotencyKey() },
    });
  }

  getShippingCompanies(): Observable<ApiResponse<LookupItem[]>> {
    return this.http.get<ApiResponse<LookupItem[]>>(`${this.baseUrl}/lookups/shipping-companies`);
  }

  getMyAddresses(): Observable<ApiResponse<UserAddress[]>> {
    return this.http.get<ApiResponse<UserAddress[]>>(`${this.baseUrl}/me/addresses`);
  }

  private createIdempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.()
      ?? `${Date.now()}-${Math.random().toString(16).slice(2)}-${Math.random().toString(16).slice(2)}`;
  }
}
