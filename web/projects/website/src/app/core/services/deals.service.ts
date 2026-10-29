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
