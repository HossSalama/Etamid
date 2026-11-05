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
  imagePublicId: string | null;
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
  imageUrl: string | null;
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
  imageUrl: string | null;
  counterpartyId: number | null;
  needShipping: boolean;
  shippingCompanyNameAr: string | null;
  shippingCompanyNameEn: string | null;
  shippingAddressText: string | null;
  trackingNumber: string | null;
  shipmentStatus: string | null;
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
  sellerId: number;
}

export interface Wallet {
  activated: boolean;
  activatedAt: string | null;
  availableBalance: number | null;
  escrowHeld: number | null;
  hasBankAccount: boolean;
  bankAccountMasked: string | null;
}

export interface WalletTransaction {
  id: number;
  type: string;
  direction: string;
  amount: number;
  balanceAfter: number | null;
  dealId: number | null;
  description: string | null;
  createdAt: string;
}

export interface Withdrawal {
  id: number;
  amount: number;
  feeAmount: number;
  ibanSnapshotMasked: string;
  status: string;
  rejectionReason: string | null;
  bankReference: string | null;
  approvedAt: string | null;
  processedAt: string | null;
  createdAt: string;
}

export interface ShopProduct {
  id: number;
  name: string;
  description: string | null;
  price: number;
  quantity: number;
  imageUrl: string | null;
  createdAt: string;
  sellerId: number;
  sellerName: string;
  storeName: string | null;
  sellerCode: string;
  isCompany: boolean;
}

export interface ProposeEditRequest {
  amount?: number | null;
  durationValue?: number | null;
  durationUnit?: 'hour' | 'day' | null;
  description?: string | null;
}

export interface SellerStore {
  sellerId: number;
  sellerName: string;
  storeName: string | null;
  categoryId: number | null;
  category: string | null;
  bannerUrl: string | null;
  ratingAvg: number;
  ratingCount: number;
  successfulDeals: number;
}

export interface MyStore {
  id: number;
  name: string;
  categoryId: number | null;
  category: string | null;
  bannerUrl: string | null;
  rating: number;
  successfulDeals: number;
  bannerFileId: string | null;
}

export interface SaveStoreRequest {
  storeName: string;
  categoryId: number | null;
  bannerFileId: string | null;
}

export interface SellerReview {
  id: number;
  dealId: number;
  dealTitle: string;
  stars: number;
  comment: string | null;
  raterName: string;
  createdAt: string;
}

export interface Product {
  id: number;
  name: string;
  description: string | null;
  price: number;
  quantity: number;
  imageUrl: string | null;
  createdAt: string;
  imageFileId: string | null;
}

export interface ProductInput {
  name: string;
  description: string | null;
  price: number;
  quantity: number;
  imagePublicId: string | null;
}

export interface ImportRowError {
  row: number;
  field: string;
  code: string;
}

export interface ImportResult {
  imported: number;
  errors: ImportRowError[];
}

export interface UserAddress {
  id: number;
  label: string | null;
  recipientName: string;
  recipientPhone: string;
  city: string;
  district: string | null;
  street: string | null;
  buildingNo: string | null;
  postalCode: string | null;
  additionalNo: string | null;
  fullText: string;
  isDefault: boolean;
}

export interface AddressInput {
  label: string | null;
  recipientName: string;
  recipientPhone: string;
  city: string;
  district: string | null;
  street: string | null;
  buildingNo: string | null;
  postalCode: string | null;
  additionalNo: string | null;
}

export interface CompanyInstallmentInput {
  label: string | null;
  amount: number;
  dueDate: string;
}

export interface CreateCompanyDealRequest {
  counterpartyPhoneOrCode: string;
  title: string;
  details: string;
  totalAmount: number;
  startDate: string;
  contractFilePublicId: string | null;
  approvalSendDaysBefore: number | null;
  approvalResponseDays: number | null;
  approvalNotes: string | null;
  penaltyPercent: number | null;
  penaltyGraceDays: number | null;
  penaltyClauses: string | null;
  wizardStep: number;
  installments: CompanyInstallmentInput[];
}

export interface CompanyInstallment {
  id: number;
  order: number;
  label: string | null;
  amount: number;
  dueDate: string;
  status: string;
  approvalRequestedAt: string | null;
  approvalDeadlineAt: string | null;
  approvedAt: string | null;
  paidAt: string | null;
  isFunded: boolean;
  penaltyAmount: number | null;
  penaltyStatus: string | null;
  rejectionReason: string | null;
  availableActions: string[];
}

export interface CompanyPenalty {
  id: number;
  installmentId: number;
  penaltyPercent: number;
  baseAmount: number;
  penaltyAmount: number;
  daysLate: number;
  status: string;
  waiverReason: string | null;
  appliedAt: string;
}

export interface CompanyDealSummary {
  id: number;
  number: number;
  title: string;
  counterpartyName: string;
  counterpartyCode: string;
  totalAmount: number;
  currency: string;
  status: string;
  role: string;
  startDate: string;
  createdAt: string;
  availableActions: string[];
}

export interface CompanyDealDetail extends CompanyDealSummary {
  details: string;
  creatorName: string;
  hasContract: boolean;
  contractFileUrl: string | null;
  approvalSendDaysBefore: number | null;
  approvalResponseDays: number | null;
  approvalNotes: string | null;
  penaltyPercent: number | null;
  penaltyGraceDays: number;
  penaltyClauses: string | null;
  wizardStep: number;
  rejectReasonId: number | null;
  rejectNote: string | null;
  submittedAt: string | null;
  acceptedAt: string | null;
  completedAt: string | null;
  installments: CompanyInstallment[];
  penalties: CompanyPenalty[];
}

export interface InstallmentFundSession {
  paymentId: string;
  companyDealId: number;
  installmentId: number;
  status: string;
  amount: number;
  currency: string;
  provider: string;
  providerPaymentId: string;
  redirectUrl: string;
}

export interface DisputeAttachment {
  id: number;
  fileId: number;
  fileName: string;
  fileUrl: string | null;
  contentType: string;
  sizeBytes: number;
  uploadedByName: string | null;
  createdAt: string;
}

export interface DisputeMessage {
  id: number;
  senderType: string;
  senderName: string | null;
  message: string | null;
  isInternalNote: boolean;
  createdAt: string;
  attachments: DisputeAttachment[];
}

export interface SettlementProposal {
  id: number;
  proposedById: number;
  proposedByName: string | null;
  resolution: string;
  sellerPercent: number | null;
  status: string;
  createdAt: string;
  decidedAt: string | null;
}

export interface DisputeDetail {
  id: number;
  number: string;
  dealId: number;
  dealTitle: string;
  dealAmount: number;
  dealCurrency: string;
  status: string;
  resolution: string;
  resolutionNote: string | null;
  details: string;
  reasonId: number;
  reasonNameAr: string | null;
  reasonNameEn: string | null;
  openedById: number;
  openedByName: string | null;
  againstUserId: number;
  againstName: string | null;
  role: string;
  infoDeadlineAt: string | null;
  resolvedAt: string | null;
  createdAt: string;
  messages: DisputeMessage[];
  attachments: DisputeAttachment[];
  openProposal: SettlementProposal | null;
  availableActions: string[];
}

@Injectable({ providedIn: 'root' })
export class DealsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  createDeal(request: CreateDealRequest): Observable<ApiResponse<CreatedDeal>> {
    return this.http.post<ApiResponse<CreatedDeal>>(`${this.baseUrl}/deals`, request);
  }

  uploadDealImage(file: File): Observable<ApiResponse<{ fileId: string; url: string }>> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('purpose', 'deal_image');
    return this.http.post<ApiResponse<{ fileId: string; url: string }>>(`${this.baseUrl}/uploads`, formData);
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

  // ---------- Disputes ----------

  getDispute(dealId: number): Observable<ApiResponse<DisputeDetail>> {
    return this.http.get<ApiResponse<DisputeDetail>>(`${this.baseUrl}/deals/${dealId}/dispute`);
  }

  uploadDisputeFile(file: File): Observable<ApiResponse<{ fileId: string; url: string }>> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('purpose', 'dispute');
    return this.http.post<ApiResponse<{ fileId: string; url: string }>>(`${this.baseUrl}/uploads`, formData);
  }

  sendDisputeMessage(dealId: number, message: string, filePublicId?: string): Observable<ApiResponse<DisputeMessage>> {
    return this.http.post<ApiResponse<DisputeMessage>>(`${this.baseUrl}/deals/${dealId}/dispute/messages`, {
      message,
      ...(filePublicId ? { filePublicId } : {}),
    });
  }

  addDisputeAttachment(dealId: number, filePublicId: string): Observable<ApiResponse<DisputeAttachment>> {
    return this.http.post<ApiResponse<DisputeAttachment>>(
      `${this.baseUrl}/deals/${dealId}/dispute/attachments`,
      { filePublicId },
    );
  }

  proposeSettlement(
    dealId: number,
    resolution: string,
    sellerPercent?: number,
  ): Observable<ApiResponse<SettlementProposal>> {
    return this.http.post<ApiResponse<SettlementProposal>>(
      `${this.baseUrl}/deals/${dealId}/dispute/settlement/propose`,
      { resolution, ...(sellerPercent !== undefined ? { sellerPercent } : {}) },
    );
  }

  acceptSettlement(dealId: number, proposalId: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(
      `${this.baseUrl}/deals/${dealId}/dispute/settlement/${proposalId}/accept`,
      null,
    );
  }

  rejectSettlement(dealId: number, proposalId: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(
      `${this.baseUrl}/deals/${dealId}/dispute/settlement/${proposalId}/reject`,
      null,
    );
  }

  rateDeal(id: number, stars: number, comment?: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/rating`, {
      stars,
      ...(comment ? { comment } : {}),
    });
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

  deliverDeal(id: number, trackingNumber?: string): Observable<ApiResponse<unknown>> {
    const tracking = (trackingNumber ?? '').trim();
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/deliver`, {
      trackingNumber: tracking ? tracking : null,
    });
  }

  releaseEscrow(id: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/deals/${id}/release`, null, {
      headers: { 'X-Idempotency-Key': this.createIdempotencyKey() },
    });
  }

  getShippingCompanies(): Observable<ApiResponse<LookupItem[]>> {
    return this.http.get<ApiResponse<LookupItem[]>>(`${this.baseUrl}/lookups/shipping-companies`);
  }

  getSellerStore(sellerId: number): Observable<ApiResponse<SellerStore>> {
    return this.http.get<ApiResponse<SellerStore>>(`${this.baseUrl}/sellers/${sellerId}/store`);
  }

  getSellerReviews(sellerId: number, page = 1, pageSize = 20): Observable<ApiResponse<SellerReview[]>> {
    return this.http.get<ApiResponse<SellerReview[]>>(`${this.baseUrl}/sellers/${sellerId}/reviews`, {
      params: { page, pageSize },
    });
  }

  getWallet(): Observable<ApiResponse<Wallet>> {
    return this.http.get<ApiResponse<Wallet>>(`${this.baseUrl}/me/wallet`);
  }

  activateWallet(): Observable<ApiResponse<Wallet>> {
    return this.http.post<ApiResponse<Wallet>>(`${this.baseUrl}/me/wallet/activate`, {});
  }

  getWalletTransactions(page = 1, pageSize = 20): Observable<ApiResponse<WalletTransaction[]>> {
    return this.http.get<ApiResponse<WalletTransaction[]>>(`${this.baseUrl}/me/wallet/transactions`, {
      params: { page, pageSize },
    });
  }

  getMyWithdrawals(page = 1, pageSize = 20): Observable<ApiResponse<Withdrawal[]>> {
    return this.http.get<ApiResponse<Withdrawal[]>>(`${this.baseUrl}/me/wallet/withdrawals`, {
      params: { page, pageSize },
    });
  }

  requestWithdrawal(amount: number, idempotencyKey: string): Observable<ApiResponse<Withdrawal>> {
    return this.http.post<ApiResponse<Withdrawal>>(`${this.baseUrl}/me/wallet/withdrawals`, {
      amount,
      idempotencyKey,
    });
  }

  getMyStore(): Observable<ApiResponse<MyStore | null>> {
    return this.http.get<ApiResponse<MyStore | null>>(`${this.baseUrl}/me/store`);
  }

  saveMyStore(request: SaveStoreRequest): Observable<ApiResponse<MyStore | null>> {
    return this.http.put<ApiResponse<MyStore | null>>(`${this.baseUrl}/me/store`, request);
  }

  uploadBanner(file: File): Observable<ApiResponse<{ fileId: string; url: string }>> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('purpose', 'store_banner');
    return this.http.post<ApiResponse<{ fileId: string; url: string }>>(`${this.baseUrl}/uploads`, formData);
  }

  getShopProducts(page = 1, pageSize = 20): Observable<ApiResponse<ShopProduct[]>> {
    return this.http.get<ApiResponse<ShopProduct[]>>(`${this.baseUrl}/products`, {
      params: { page, pageSize },
    });
  }

  getSellerProducts(sellerId: number, page = 1, pageSize = 20): Observable<ApiResponse<Product[]>> {
    return this.http.get<ApiResponse<Product[]>>(`${this.baseUrl}/sellers/${sellerId}/products`, {
      params: { page, pageSize },
    });
  }

  getMyProducts(page = 1, pageSize = 20): Observable<ApiResponse<Product[]>> {
    return this.http.get<ApiResponse<Product[]>>(`${this.baseUrl}/me/products`, {
      params: { page, pageSize },
    });
  }

  addProduct(input: ProductInput): Observable<ApiResponse<Product>> {
    return this.http.post<ApiResponse<Product>>(`${this.baseUrl}/me/products`, input);
  }

  updateProduct(id: number, input: ProductInput): Observable<ApiResponse<Product>> {
    return this.http.put<ApiResponse<Product>>(`${this.baseUrl}/me/products/${id}`, input);
  }

  deleteProduct(id: number): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.baseUrl}/me/products/${id}`);
  }

  uploadProductImage(file: File): Observable<ApiResponse<{ fileId: string; url: string }>> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('purpose', 'product_image');
    return this.http.post<ApiResponse<{ fileId: string; url: string }>>(`${this.baseUrl}/uploads`, formData);
  }

  importProducts(file: File): Observable<ApiResponse<ImportResult>> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<ApiResponse<ImportResult>>(`${this.baseUrl}/me/products/import`, formData);
  }

  downloadTemplate(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/me/products/template`, { responseType: 'blob' });
  }

  getMyAddresses(): Observable<ApiResponse<UserAddress[]>> {
    return this.http.get<ApiResponse<UserAddress[]>>(`${this.baseUrl}/me/addresses`);
  }

  addAddress(input: AddressInput): Observable<ApiResponse<UserAddress>> {
    return this.http.post<ApiResponse<UserAddress>>(`${this.baseUrl}/me/addresses`, input);
  }

  updateAddress(id: number, input: AddressInput): Observable<ApiResponse<UserAddress>> {
    return this.http.put<ApiResponse<UserAddress>>(`${this.baseUrl}/me/addresses/${id}`, input);
  }

  deleteAddress(id: number): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.baseUrl}/me/addresses/${id}`);
  }

  setDefaultAddress(id: number): Observable<ApiResponse<unknown>> {
    return this.http.put<ApiResponse<unknown>>(`${this.baseUrl}/me/addresses/${id}/default`, {});
  }

  // ---------- Company deals ----------
  getCompanyDeals(status?: string, page = 1, pageSize = 20): Observable<ApiResponse<CompanyDealSummary[]>> {
    return this.http.get<ApiResponse<CompanyDealSummary[]>>(`${this.baseUrl}/company-deals`, {
      params: { ...(status ? { status } : {}), page, pageSize },
    });
  }

  getCompanyDeal(id: number): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.get<ApiResponse<CompanyDealDetail>>(`${this.baseUrl}/company-deals/${id}`);
  }

  createCompanyDeal(request: CreateCompanyDealRequest): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.post<ApiResponse<CompanyDealDetail>>(`${this.baseUrl}/company-deals`, request);
  }

  updateCompanyDeal(id: number, request: CreateCompanyDealRequest): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.put<ApiResponse<CompanyDealDetail>>(`${this.baseUrl}/company-deals/${id}`, request);
  }

  submitCompanyDeal(id: number): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.post<ApiResponse<CompanyDealDetail>>(`${this.baseUrl}/company-deals/${id}/submit`, {});
  }

  acceptCompanyDeal(id: number): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.post<ApiResponse<CompanyDealDetail>>(`${this.baseUrl}/company-deals/${id}/accept`, { acceptTerms: true });
  }

  rejectCompanyDeal(id: number, reasonId: number, note?: string): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.post<ApiResponse<CompanyDealDetail>>(`${this.baseUrl}/company-deals/${id}/reject`, {
      reasonId,
      ...(note ? { note } : {}),
    });
  }

  cancelCompanyDeal(id: number): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.post<ApiResponse<CompanyDealDetail>>(`${this.baseUrl}/company-deals/${id}/cancel`, {});
  }

  requestInstallmentApproval(dealId: number, installmentId: number): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.post<ApiResponse<CompanyDealDetail>>(
      `${this.baseUrl}/company-deals/${dealId}/installments/${installmentId}/request-approval`, {});
  }

  approveInstallment(dealId: number, installmentId: number): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.post<ApiResponse<CompanyDealDetail>>(
      `${this.baseUrl}/company-deals/${dealId}/installments/${installmentId}/approve`, {});
  }

  rejectInstallment(dealId: number, installmentId: number, reason: string): Observable<ApiResponse<CompanyDealDetail>> {
    return this.http.post<ApiResponse<CompanyDealDetail>>(
      `${this.baseUrl}/company-deals/${dealId}/installments/${installmentId}/reject`, { reason });
  }

  fundInstallment(dealId: number, installmentId: number): Observable<ApiResponse<InstallmentFundSession>> {
    return this.http.post<ApiResponse<InstallmentFundSession>>(
      `${this.baseUrl}/company-deals/${dealId}/installments/${installmentId}/fund`, null, {
        headers: { 'X-Idempotency-Key': this.createIdempotencyKey() },
      });
  }

  uploadContractFile(file: File): Observable<ApiResponse<{ fileId: string; url: string }>> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('purpose', 'contract');
    return this.http.post<ApiResponse<{ fileId: string; url: string }>>(`${this.baseUrl}/uploads`, formData);
  }

  private createIdempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.()
      ?? `${Date.now()}-${Math.random().toString(16).slice(2)}-${Math.random().toString(16).slice(2)}`;
  }
}
