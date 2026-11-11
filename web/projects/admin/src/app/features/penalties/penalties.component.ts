import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { ApiMeta } from '../../core/models/api-response.model';
import { Penalty } from '../../core/models/admin.models';

@Component({
  selector: 'app-penalties',
  imports: [FormsModule],
  templateUrl: './penalties.component.html',
  styleUrl: './penalties.component.scss',
})
export class PenaltiesComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected items        = signal<Penalty[]>([]);
  protected meta         = signal<ApiMeta | null>(null);
  protected loading      = signal(true);
  protected error        = signal('');
  protected page         = signal(1);

  protected showModal    = signal(false);
  protected selectedItem = signal<Penalty | null>(null);
  protected waiveReason  = signal('');
  protected modalLoading = signal(false);
  protected modalError   = signal('');

  protected readonly canView  = this.auth.hasPermission('admin.penalties.view');
  protected readonly canWaive = this.auth.hasPermission('admin.penalties.waive');

  ngOnInit(): void {
    if (this.canView) this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listPenalties({ page: this.page(), pageSize: 20 }).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) { this.items.set(res.data ?? []); this.meta.set(res.meta ?? null); }
        else this.error.set(res.message);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? this.lang.t('error_generic'));
      },
    });
  }

  protected goPage(p: number): void { this.page.set(p); this.load(); }

  protected openWaive(item: Penalty): void {
    this.selectedItem.set(item);
    this.showModal.set(true);
    this.waiveReason.set('');
    this.modalError.set('');
  }

  protected closeModal(): void {
    this.showModal.set(false);
    this.selectedItem.set(null);
    this.modalLoading.set(false);
    this.modalError.set('');
  }

  protected confirmWaive(): void {
    const item = this.selectedItem();
    if (!item || this.modalLoading()) return;
    if (!this.waiveReason().trim()) { this.modalError.set(this.lang.t('error_generic')); return; }
    this.modalLoading.set(true);
    this.api.waivePenalty(item.id, this.waiveReason().trim()).subscribe({
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
