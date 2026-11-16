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

  // Execute the parties' agreement (not a decision: the admin never decides).
  protected showExecute    = signal(false);
  protected executeRes     = signal<'ReleaseSeller' | 'RefundBuyer' | 'Partial'>('ReleaseSeller');
  protected executePercent = signal(50);
  protected executeNote    = signal('');
  protected executeLoading = signal(false);
  protected executeError   = signal('');

  // Send the dispute documents to the buyer for court.
  protected showSendDocs    = signal(false);
  protected sendDocsNote    = signal('');
  protected sendDocsLoading = signal(false);
  protected sendDocsError   = signal('');

  // Message the parties (visible to both; internal note = admins only).
  protected msgText      = signal('');
  protected msgInternal  = signal(false);
  protected msgLoading   = signal(false);
  protected msgError     = signal('');

  // Same permission code as before (admin.disputes.decide): it now gates the
  // operator actions above instead of a decision. The code itself is seeded
  // in the database, so it is kept unchanged.
  protected readonly canOperate = this.auth.hasPermission('admin.disputes.decide');

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.router.navigate(['/disputes']); return; }
    this.load(id);
  }

  protected load(id?: number): void {
    const targetId = id ?? (this.dispute()?.id ?? 0);
    this.loading.set(true);
    this.api.getDispute(targetId).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) {
          if (res.data) this.dispute.set(res.data);
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

  protected openExecute(): void { this.showExecute.set(true); this.executeRes.set('ReleaseSeller'); this.executePercent.set(50); this.executeNote.set(''); this.executeError.set(''); }
  protected closeExecute(): void { this.showExecute.set(false); this.executeLoading.set(false); }

  protected submitExecute(): void {
    if (this.executeLoading() || !this.executeNote().trim()) return;
    this.executeLoading.set(true); this.executeError.set('');
    this.api.executeAgreement(this.dispute()!.id, {
      resolution: this.executeRes(),
      sellerPercent: this.executeRes() === 'Partial' ? this.executePercent() : undefined,
      note: this.executeNote().trim(),
    }).subscribe({
      next: (res: { success: boolean; message?: string }) => {
        this.executeLoading.set(false);
        if (res.success) { this.closeExecute(); this.load(); }
        else this.executeError.set(res.message ?? this.lang.t('error_generic'));
      },
      error: (err: HttpErrorResponse) => { this.executeLoading.set(false); this.executeError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected openSendDocs(): void { this.showSendDocs.set(true); this.sendDocsNote.set(''); this.sendDocsError.set(''); }
  protected closeSendDocs(): void { this.showSendDocs.set(false); this.sendDocsLoading.set(false); }

  protected submitSendDocs(): void {
    if (this.sendDocsLoading()) return;
    this.sendDocsLoading.set(true); this.sendDocsError.set('');
    this.api.sendDisputeDocuments(this.dispute()!.id, { note: this.sendDocsNote().trim() || undefined }).subscribe({
      next: (res) => {
        this.sendDocsLoading.set(false);
        if (res.success) { if (res.data) this.dispute.set(res.data as Dispute); this.closeSendDocs(); }
        else this.sendDocsError.set(res.message ?? this.lang.t('error_generic'));
      },
      error: (err: HttpErrorResponse) => { this.sendDocsLoading.set(false); this.sendDocsError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected submitMessage(): void {
    if (this.msgLoading() || !this.msgText().trim()) return;
    this.msgLoading.set(true); this.msgError.set('');
    this.api.postDisputeMessage(this.dispute()!.id, {
      message: this.msgText().trim(),
      isInternalNote: this.msgInternal(),
    }).subscribe({
      next: (res) => {
        this.msgLoading.set(false);
        if (res.success) { this.msgText.set(''); this.msgInternal.set(false); this.load(); }
        else this.msgError.set(res.message ?? this.lang.t('error_generic'));
      },
      error: (err: HttpErrorResponse) => { this.msgLoading.set(false); this.msgError.set(err.error?.message ?? this.lang.t('error_generic')); },
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

  protected formatDate(dt: string | null | undefined): string {
    if (!dt) return '—';
    return new Date(dt).toLocaleString(this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' });
  }

  /** Reason in the UI language, falling back to the other language, then '—'. */
  protected reasonLabel(): string {
    const d = this.dispute();
    if (!d) return '—';
    const label = this.lang.currentLang() === 'ar'
      ? (d.reasonNameAr ?? d.reasonNameEn)
      : (d.reasonNameEn ?? d.reasonNameAr);
    return label ?? '—';
  }

  protected isResolved(): boolean {
    const s = this.dispute()?.status ?? '';
    return s === 'resolved_buyer' || s === 'resolved_seller' || s === 'closed';
  }
}
