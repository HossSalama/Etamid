import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { SupportTicketDetail } from '../../core/models/admin.models';

@Component({
  selector: 'app-support-ticket-detail',
  imports: [FormsModule],
  templateUrl: './support-ticket-detail.component.html',
  styleUrl: './support-ticket-detail.component.scss',
})
export class SupportTicketDetailComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);
  private readonly route  = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected ticket       = signal<SupportTicketDetail | null>(null);
  protected loading      = signal(true);
  protected error        = signal('');

  // Reply to the user (max 1000 chars; the API refuses closed tickets).
  protected msgText      = signal('');
  protected msgLoading   = signal(false);
  protected msgError     = signal('');

  // Close the ticket — admin only. The user has no close action anywhere:
  // no button on the website and no user close endpoint.
  protected showCloseConfirm = signal(false);
  protected closeLoading = signal(false);
  protected closeError   = signal('');

  // admin.support.manage gates both operator actions (reply + close).
  protected readonly canOperate = this.auth.hasPermission('admin.support.manage');

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.router.navigate(['/support/tickets']); return; }
    this.load(id);
  }

  protected load(id?: number): void {
    const targetId = id ?? (this.ticket()?.id ?? 0);
    this.loading.set(true);
    this.api.getSupportTicket(targetId).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success) {
          if (res.data) this.ticket.set(res.data);
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

  protected submitMessage(): void {
    if (this.msgLoading() || !this.msgText().trim()) return;
    this.msgLoading.set(true); this.msgError.set('');
    this.api.postSupportTicketMessage(this.ticket()!.id, { message: this.msgText().trim() }).subscribe({
      next: (res) => {
        this.msgLoading.set(false);
        if (res.success) { this.msgText.set(''); this.load(); }
        else this.msgError.set(res.message ?? this.lang.t('error_generic'));
      },
      error: (err: HttpErrorResponse) => { this.msgLoading.set(false); this.msgError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected openCloseConfirm(): void { this.showCloseConfirm.set(true); this.closeError.set(''); }
  protected cancelClose(): void { this.showCloseConfirm.set(false); this.closeLoading.set(false); }

  protected submitClose(): void {
    if (this.closeLoading()) return;
    this.closeLoading.set(true); this.closeError.set('');
    this.api.closeSupportTicket(this.ticket()!.id).subscribe({
      next: (res) => {
        this.closeLoading.set(false);
        if (res.success) { if (res.data) this.ticket.set(res.data); this.cancelClose(); }
        else this.closeError.set(res.message ?? this.lang.t('error_generic'));
      },
      error: (err: HttpErrorResponse) => { this.closeLoading.set(false); this.closeError.set(err.error?.message ?? this.lang.t('error_generic')); },
    });
  }

  protected back(): void { this.router.navigate(['/support/tickets']); }

  protected isClosed(): boolean {
    return this.ticket()?.status === 'closed';
  }

  protected statusBadge(status: string): string {
    switch (status) {
      case 'open':      return 'badge badge-warning';
      case 'in_review': return 'badge badge-info';
      case 'closed':    return 'badge badge-neutral';
      default: return 'badge badge-neutral';
    }
  }

  protected statusLabel(status: string): string {
    switch (status) {
      case 'open':      return this.lang.t('sup_status_open');
      case 'in_review': return this.lang.t('sup_status_in_review');
      case 'closed':    return this.lang.t('sup_status_closed');
      default: return status;
    }
  }

  protected formatDate(dt: string | null | undefined): string {
    if (!dt) return '—';
    return new Date(dt).toLocaleString(this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' });
  }
}
