import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';
import { AuditLog, Dispute, UserCounts } from '../../core/models/admin.models';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  protected readonly api  = inject(AdminApiService);
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);

  protected pendingWithdrawals = signal<number | null>(null);
  protected openDisputes       = signal<number | null>(null);
  protected appliedPenalties   = signal<number | null>(null);
  protected recentAudit        = signal<AuditLog[]>([]);
  protected userCounts         = signal<UserCounts | null>(null);
  protected userCountsLoaded   = signal(false);
  protected totalDeals         = signal<number | null>(null);

  protected loadingWithdrawals = signal(false);
  protected loadingDisputes    = signal(false);
  protected loadingPenalties   = signal(false);
  protected loadingAudit       = signal(false);
  protected loadingUserCounts  = signal(false);
  protected loadingDeals       = signal(false);

  protected readonly canViewWithdrawals = this.auth.hasPermission('admin.withdrawals.view');
  protected readonly canViewDisputes    = this.auth.hasPermission('admin.disputes.view');
  protected readonly canViewPenalties   = this.auth.hasPermission('admin.penalties.view');
  protected readonly canViewAudit       = this.auth.hasPermission('admin.audit.view');
  protected readonly canViewUsers       = this.auth.hasPermission('admin.users.view');
  protected readonly canViewDeals       = this.auth.hasPermission('admin.deals.view');

  ngOnInit(): void {
    if (this.canViewWithdrawals) this.loadWithdrawals();
    if (this.canViewDisputes)    this.loadDisputes();
    if (this.canViewPenalties)   this.loadPenalties();
    if (this.canViewAudit)       this.loadAudit();
    if (this.canViewUsers)       this.loadUserCounts();
    if (this.canViewDeals)       this.loadDealsCount();
  }

  private loadWithdrawals(): void {
    this.loadingWithdrawals.set(true);
    this.api.listWithdrawals({ status: 'pending', pageSize: 1 }).subscribe({
      next: (res) => {
        this.loadingWithdrawals.set(false);
        if (res.success) this.pendingWithdrawals.set(res.meta?.total ?? (res.data?.length ?? 0));
      },
      error: (err: HttpErrorResponse) => {
        this.loadingWithdrawals.set(false);
        if (err.status !== 403) this.pendingWithdrawals.set(0);
      },
    });
  }

  private loadDisputes(): void {
    this.loadingDisputes.set(true);
    this.api.listDisputes({ pageSize: 100 }).subscribe({
      next: (res) => {
        this.loadingDisputes.set(false);
        if (res.success) {
          const open = (res.data as Dispute[] ?? []).filter(
            (d) => d.status === 'under_review' || d.status === 'awaiting_info',
          ).length;
          this.openDisputes.set(res.meta ? res.meta.total : open);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.loadingDisputes.set(false);
        if (err.status !== 403) this.openDisputes.set(0);
      },
    });
  }

  private loadPenalties(): void {
    this.loadingPenalties.set(true);
    this.api.listPenalties({ pageSize: 1 }).subscribe({
      next: (res) => {
        this.loadingPenalties.set(false);
        if (res.success) this.appliedPenalties.set(res.meta?.total ?? (res.data?.length ?? 0));
      },
      error: (err: HttpErrorResponse) => {
        this.loadingPenalties.set(false);
        if (err.status !== 403) this.appliedPenalties.set(0);
      },
    });
  }

  private loadAudit(): void {
    this.loadingAudit.set(true);
    this.api.listAuditLogs({ pageSize: 8 }).subscribe({
      next: (res) => {
        this.loadingAudit.set(false);
        if (res.success) this.recentAudit.set(res.data ?? []);
      },
      error: () => this.loadingAudit.set(false),
    });
  }

  private loadUserCounts(): void {
    this.loadingUserCounts.set(true);
    this.api.getUserCounts().subscribe({
      next: (res) => {
        this.loadingUserCounts.set(false);
        if (res.success) {
          this.userCounts.set(res.data ?? null);
          this.userCountsLoaded.set(true);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.loadingUserCounts.set(false);
        // 403 = hide cards; other errors = hide quietly
        if (err.status !== 403) this.userCountsLoaded.set(false);
      },
    });
  }

  private loadDealsCount(): void {
    this.loadingDeals.set(true);
    this.api.listDeals({ page: 1, pageSize: 1 }).subscribe({
      next: (res) => {
        this.loadingDeals.set(false);
        if (res.success) this.totalDeals.set(res.meta?.total ?? null);
      },
      error: (err: HttpErrorResponse) => {
        this.loadingDeals.set(false);
        if (err.status !== 403) this.totalDeals.set(0);
      },
    });
  }

  protected formatDate(dt: string): string {
    return new Date(dt).toLocaleString(
      this.lang.currentLang() === 'ar' ? 'ar-SA' : 'en-US',
      { dateStyle: 'short', timeStyle: 'short' },
    );
  }
}


