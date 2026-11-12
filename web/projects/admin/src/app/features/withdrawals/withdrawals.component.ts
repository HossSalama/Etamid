import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { ApiMeta } from '../../core/models/api-response.model';
import { Withdrawal } from '../../core/models/admin.models';

@Component({
  selector: 'app-withdrawals',
  imports: [FormsModule],
  templateUrl: './withdrawals.component.html',
  styleUrl: './withdrawals.component.scss',
})
export class WithdrawalsComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected items        = signal<Withdrawal[]>([]);
  protected meta         = signal<ApiMeta | null>(null);
  protected loading      = signal(true);
  protected error        = signal('');
  protected statusFilter = signal('');
  protected page         = signal(1);

  protected activeModal  = signal<'approve' | 'reject' | 'complete' | null>(null);
  protected selectedItem = signal<Withdrawal | null>(null);
  protected modalInput   = signal('');
  protected modalLoading = signal(false);
  protected modalError   = signal('');

  protected readonly canApprove  = this.auth.hasPermission('admin.withdrawals.approve');
  protected readonly canComplete = this.auth.hasPermission('admin.withdrawals.complete');
  protected readonly canView     = this.auth.hasPermission('admin.withdrawals.view');

  ngOnInit(): void {
    if (this.canView) this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listWithdrawals({ status: this.statusFilter() || undefined, page: this.page(), pageSize: 20 }).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) { this.items.set(res.data ?? []); this.meta.set(res.meta ?? null); }
        else this.error.set(res.message);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        const code = err.error?.errors?.[0]?.code;
        this.error.set(code === 'ADMIN_FORBIDDEN' ? this.lang.t('forbidden') : (err.error?.message ?? this.lang.t('error_generic')));
      },
    });
  }

  protected applyFilter(): void { this.page.set(1); this.load(); }
  protected goPage(p: number): void { this.page.set(p); this.load(); }

  protected openModal(type: 'approve' | 'reject' | 'complete', item: Withdrawal): void {
    this.selectedItem.set(item);
    this.activeModal.set(type);
    this.modalInput.set('');
    this.modalError.set('');
  }

  protected closeModal(): void {
    this.activeModal.set(null);
    this.selectedItem.set(null);
    this.modalLoading.set(false);
    this.modalError.set('');
  }

  protected confirmModal(): void {
    const type = this.activeModal();
    const item = this.selectedItem();
    if (!type || !item || this.modalLoading()) return;
    if ((type === 'reject' || type === 'complete') && !this.modalInput().trim()) {
      this.modalError.set(this.lang.t('error_generic')); return;
    }
    this.modalLoading.set(true);
    this.modalError.set('');
    const obs = type === 'approve'
      ? this.api.approveWithdrawal(item.id)
      : type === 'reject'
        ? this.api.rejectWithdrawal(item.id, this.modalInput().trim())
        : this.api.completeWithdrawal(item.id, this.modalInput().trim());
    obs.subscribe({
      next: (res) => {
        this.modalLoading.set(false);
        if (res.success) { this.closeModal(); this.load(); }
        else this.modalError.set(res.message ?? this.lang.t('error_generic'));
      },
      error: (err: HttpErrorResponse) => {
        this.modalLoading.set(false);
        this.modalError.set(err.error?.message ?? this.lang.t('error_generic'));
      },
    });
  }

  protected statusBadge(status: string): string {
    switch (status) {
      case 'pending':   return 'badge badge-warning';
      case 'approved':  return 'badge badge-info';
      case 'completed': return 'badge badge-success';
      case 'rejected':  return 'badge badge-danger';
      default:          return 'badge badge-neutral';
    }
  }

  protected statusLabel(status: string): string {
    switch (status) {
      case 'pending':   return this.lang.t('wd_status_pending');
      case 'approved':  return this.lang.t('wd_status_approved');
      case 'completed': return this.lang.t('wd_status_completed');
      case 'rejected':  return this.lang.t('wd_status_rejected');
      default: return status;
    }
  }

  protected formatSAR(amount: number): string {
    return amount.toLocaleString('ar-SA', { style: 'currency', currency: 'SAR', maximumFractionDigits: 2 });
  }

  protected formatDate(dt: string): string {
    return new Date(dt).toLocaleString(this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' });
  }

  protected pages(): number[] {
    const m = this.meta();
    if (!m) return [];
    return Array.from({ length: m.totalPages }, (_, i) => i + 1);
  }
}
