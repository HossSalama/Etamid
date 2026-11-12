import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { Dispute } from '../../core/models/admin.models';

@Component({
  selector: 'app-dispute-detail',
  imports: [FormsModule],
  templateUrl: './dispute-detail.component.html',
  styleUrl: './dispute-detail.component.scss',
})
export class DisputeDetailComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);
  private readonly route  = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected dispute      = signal<Dispute | null>(null);
  protected loading      = signal(true);
  protected error        = signal('');

  protected showReqInfo  = signal(false);
  protected reqNote      = signal('');
  protected reqDeadline  = signal(7);
  protected reqLoading   = signal(false);
  protected reqError     = signal('');

  protected showDecide    = signal(false);
  protected decideRes     = signal<'ReleaseSeller' | 'RefundBuyer' | 'Partial'>('ReleaseSeller');
  protected decidePercent = signal(50);
  protected decideNote    = signal('');
  protected decideLoading = signal(false);
  protected decideError   = signal('');

  protected readonly canDecide = this.auth.hasPermission('admin.disputes.decide');

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.router.navigate(['/disputes']); return; }
    this.load(id);
  }

  protected load(id?: number): void {
    const targetId = id ?? (this.dispute()?.id ?? 0);
    this.loading.set(true);
    // No single-dispute GET — load full page and find by id
    this.api.listDisputes({ pageSize: 100 }).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) {
          const found = (res.data ?? []).find(d => d.id === targetId);
          if (found) this.dispute.set(found);
          else this.error.set(this.lang.t('not_found'));
        } else {
          this.error.set(res.message);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? this.lang.t('error_generic'));
      },
    });
  }

  protected openReqInfo(): void { this.showReqInfo.set(true); this.reqNote.set(''); this.reqDeadline.set(7); this.reqError.set(''); }
  protected closeReqInfo(): void { this.showReqInfo.set(false); this.reqLoading.set(false); }

  protected submitReqInfo(): void {
    if (this.reqLoading() || !this.reqDeadline()) return;
    this.reqLoading.set(true); this.reqError.set('');
    this.api.requestDisputeInfo(this.dispute()!.id, { note: this.reqNote().trim() || undefined, deadlineDays: this.reqDeadline() }).subscribe({
      next: (res) => {
        this.reqLoading.set(false);
        if (res.success) { this.dispute.set(res.data as Dispute); this.closeReqInfo(); }
        else this.reqError.set(res.message ?? this.lang.t('error_generic'));
      },
      error: (err: HttpErrorResponse) => { this.reqLoading.set(false); this.reqError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected openDecide(): void { this.showDecide.set(true); this.decideRes.set('ReleaseSeller'); this.decidePercent.set(50); this.decideNote.set(''); this.decideError.set(''); }
  protected closeDecide(): void { this.showDecide.set(false); this.decideLoading.set(false); }

  protected submitDecide(): void {
    if (this.decideLoading() || !this.decideNote().trim()) return;
    this.decideLoading.set(true); this.decideError.set('');
    this.api.decideDispute(this.dispute()!.id, {
      resolution: this.decideRes(),
      sellerPercent: this.decideRes() === 'Partial' ? this.decidePercent() : undefined,
      note: this.decideNote().trim(),
    }).subscribe({
      next: (res: { success: boolean; message?: string }) => {
        this.decideLoading.set(false);
        if (res.success) { this.closeDecide(); this.load(); }
        else this.decideError.set(res.message ?? this.lang.t('error_generic'));
      },
      error: (err: HttpErrorResponse) => { this.decideLoading.set(false); this.decideError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected back(): void { this.router.navigate(['/disputes']); }

  protected statusBadge(status: string): string {
    switch (status) {
      case 'under_review':    return 'badge badge-warning';
      case 'awaiting_info':   return 'badge badge-info';
      case 'resolved_buyer':  return 'badge badge-success';
      case 'resolved_seller': return 'badge badge-purple';
      case 'closed':          return 'badge badge-neutral';
      default: return 'badge badge-neutral';
    }
  }

  protected statusLabel(status: string): string {
    const map: Record<string, string> = {
      under_review:    this.lang.t('dis_status_under_review'),
      awaiting_info:   this.lang.t('dis_status_awaiting_info'),
      resolved_buyer:  this.lang.t('dis_status_resolved_buyer'),
      resolved_seller: this.lang.t('dis_status_resolved_seller'),
      closed:          this.lang.t('dis_status_closed'),
    };
    return map[status] ?? status;
  }

  protected formatDate(dt: string | null): string {
    if (!dt) return '—';
    return new Date(dt).toLocaleString(this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' });
  }

  protected isResolved(): boolean {
    const s = this.dispute()?.status ?? '';
    return s === 'resolved_buyer' || s === 'resolved_seller' || s === 'closed';
  }
}
