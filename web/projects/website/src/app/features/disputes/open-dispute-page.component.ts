import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  DealsService,
  DisputeDetail,
  LookupItem,
} from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

/**
 * Open a dispute, exactly like the design (dispute.png / dispute-1.png /
 * dispute success.png): pick one of the 6 reasons, write the details
 * (0/200), attach up to 5 files, confirm in a bottom sheet, then a success
 * screen with the support number. Only the buyer opens a dispute.
 */
@Component({
  selector: 'app-open-dispute-page',
  standalone: true,
  imports: [FormsModule, RouterLink, AppHeaderComponent, BottomNavComponent],
  template: `
    <div class="app-screen open-dispute-page">
      <app-header
        [title]="lang.t('open_dispute_title')"
        backUrl="/home?tab=deals"
      />

      <main class="app-page-shell" style="max-width: 840px;">
        @if (isLoading()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('dispute_loading') }}</p>
          </div>
        } @else if (!canOpen()) {
          <div class="app-card text-center" style="padding: 2.5rem; text-align: center;">
            <p>{{ lang.t('open_dispute_not_allowed') }}</p>
            <a routerLink="/home" class="btn btn-outline-coral" style="margin-top: 1rem;">{{ lang.t('back') }}</a>
          </div>
        } @else if (opened()) {
          <!-- Success screen (dispute success.png) -->
          <section class="success-block">
            <span class="success-badge" aria-hidden="true">✓</span>
            <h2 class="success-title">{{ lang.t('open_dispute_success_title') }}</h2>
            <p class="success-text">{{ lang.t('open_dispute_success_text') }}</p>
          </section>

          <section class="app-card deal-card">
            <div class="deal-card-top">
              <span class="badge badge-neutral">#{{ opened()!.dealId }}</span>
              <div class="deal-card-info">
                <strong class="deal-title">{{ opened()!.dealTitle }}</strong>
                <p class="deal-amount">{{ opened()!.dealAmount }} {{ opened()!.dealCurrency }}</p>
              </div>
            </div>
            <div class="deal-seller-row">
              <span class="meta-label">{{ lang.t('open_dispute_seller') }}</span>
              <strong>{{ opened()!.againstName ?? '—' }}</strong>
            </div>
          </section>

          <section class="app-card">
            <h3 class="card-title">{{ lang.t('open_dispute_support_details') }}</h3>
            <div class="support-row">
              <span class="meta-label">{{ lang.t('open_dispute_support_number') }}</span>
              <strong>{{ opened()!.number }}</strong>
            </div>
            <div class="support-row">
              <span class="meta-label">{{ lang.t('open_dispute_opened_at') }}</span>
              <strong>{{ formatDate(opened()!.createdAt) }}</strong>
            </div>
            <div class="support-row">
              <span class="meta-label">{{ lang.t('open_dispute_status') }}</span>
              <span class="badge badge-warning">{{ lang.t('dispute_status_under_review') }}</span>
            </div>
          </section>

          <a routerLink="/home" class="btn btn-outline-coral btn-block">
            {{ lang.t('open_dispute_back_home') }}
          </a>
        } @else {
          <!-- Open form (dispute.png) -->
          <h2 class="form-question">{{ lang.t('open_dispute_reason_q') }}</h2>
          <section class="app-card reasons-card">
            @for (reason of reasons(); track reason.id) {
              <label class="reason-row">
                <input
                  type="radio"
                  name="disputeReason"
                  [value]="reason.id"
                  [(ngModel)]="selectedReasonId"
                />
                <span>{{ reason.name }}</span>
              </label>
            }
          </section>

          <h2 class="form-question">{{ lang.t('open_dispute_details') }}</h2>
          <section class="app-card">
            <textarea
              [(ngModel)]="details"
              rows="4"
              maxlength="200"
              class="details-textarea"
              [placeholder]="lang.t('open_dispute_details_ph')"
            ></textarea>
            <p class="char-counter">({{ details.length }}/200)</p>
          </section>

          <h2 class="form-question">{{ lang.t('open_dispute_attach') }}</h2>
          <p class="attach-hint">{{ lang.t('open_dispute_attach_hint') }}</p>
          <section class="app-card">
            <label class="upload-box">
              <span class="upload-icon" aria-hidden="true">⬆</span>
              <input
                type="file"
                class="visually-hidden"
                multiple
                (change)="onFiles($event)"
              />
            </label>
            @if (files().length) {
              <div class="files-list">
                @for (file of files(); track file.name) {
                  <div class="file-chip">
                    <span>📎 {{ file.name }}</span>
                    <button type="button" class="chip-remove" (click)="removeFile(file)" aria-label="remove">✕</button>
                  </div>
                }
              </div>
            }
            @if (isUploading()) {
              <p class="uploading-text">{{ lang.t('dispute_loading') }}</p>
            }
          </section>

          <button
            type="button"
            class="btn btn-black btn-block"
            (click)="openConfirm()"
            [disabled]="isSubmitting() || isUploading()"
          >
            {{ lang.t('open_dispute_submit') }}
          </button>

          @if (error()) {
            <div class="badge badge-danger" style="width: 100%; padding: 0.6rem 0.8rem; margin-top: 1rem;">
              ⚠️ {{ lang.t(error()!) }}
            </div>
          }
        }
      </main>

      <app-bottom-nav activeNav="deals" />
    </div>

    <!-- Confirm bottom sheet (dispute-1.png) -->
    @if (showConfirm()) {
      <div class="sheet-backdrop" (click)="closeConfirm()">
        <div class="sheet-box" (click)="$event.stopPropagation()">
          <span class="sheet-icon" aria-hidden="true">✋⚠️</span>
          <h3 class="sheet-title">{{ lang.t('open_dispute_confirm_title') }}</h3>
          <p class="sheet-text">{{ lang.t('open_dispute_confirm_text') }}</p>
          <label class="confirm-check">
            <input type="checkbox" [(ngModel)]="confirmed" />
            <span>{{ lang.t('open_dispute_confirm_check') }}</span>
          </label>
          <button
            type="button"
            class="btn btn-black btn-block"
            (click)="submit()"
            [disabled]="!confirmed || isSubmitting()"
          >
            {{ lang.t('open_dispute_submit') }}
          </button>
        </div>
      </div>
    }
  `,
  styles: [`
    .form-question {
      font-size: 1.1rem;
      font-weight: 850;
      color: #111827;
      margin: 1.25rem 0 0.6rem;
    }
    .card-title {
      font-size: 1.15rem;
      font-weight: 850;
      color: #111827;
      margin: 0 0 0.75rem;
    }
    .meta-label {
      font-size: 0.82rem;
      color: #6b7280;
      font-weight: 600;
    }
    .reasons-card {
      display: flex;
      flex-direction: column;
      padding: 0.25rem 1rem;
    }
    .reason-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.9rem 0;
      border-bottom: 1px solid #f1f5f9;
      cursor: pointer;
      font-size: 0.95rem;
      color: #374151;
    }
    .reason-row:last-child { border-bottom: none; }
    .reason-row input { accent-color: #eb5757; width: 1.1rem; height: 1.1rem; }
    .details-textarea {
      width: 100%;
      border: none;
      outline: none;
      resize: vertical;
      font-family: inherit;
      font-size: 0.95rem;
      line-height: 1.5;
    }
    .char-counter {
      color: #9ca3af;
      font-size: 0.82rem;
      margin: 0.35rem 0 0;
    }
    .attach-hint {
      color: #6b7280;
      font-size: 0.88rem;
      margin: 0 0 0.6rem;
    }
    .upload-box {
      display: flex;
      align-items: center;
      justify-content: center;
      border: 1px solid #e5e7eb;
      border-radius: 12px;
      padding: 1rem;
      cursor: pointer;
    }
    .upload-icon { font-size: 1.4rem; color: #eb5757; }
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      border: 0;
    }
    .files-list {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      margin-top: 0.75rem;
    }
    .file-chip {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      padding: 0.4rem 0.7rem;
      font-size: 0.85rem;
    }
    .chip-remove {
      border: none;
      background: none;
      cursor: pointer;
      color: #b91c1c;
      font-size: 0.9rem;
    }
    .uploading-text { color: #6b7280; font-size: 0.85rem; margin: 0.5rem 0 0; }
    .btn-block { width: 100%; margin-top: 1.25rem; }
    .success-block { text-align: center; padding: 1.5rem 0 0.5rem; }
    .success-badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 4.5rem;
      height: 4.5rem;
      border-radius: 50%;
      background: #22c55e;
      color: #fff;
      font-size: 2rem;
      font-weight: 800;
    }
    .success-title {
      font-size: 1.35rem;
      font-weight: 850;
      color: #111827;
      margin: 1rem 0 0.35rem;
    }
    .success-text { color: #6b7280; font-size: 0.95rem; margin: 0; }
    .deal-card { margin-top: 1.25rem; }
    .deal-card-top {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .deal-card-info { display: flex; flex-direction: column; gap: 0.15rem; }
    .deal-title { font-size: 1rem; color: #111827; }
    .deal-amount { color: #eb5757; font-weight: 800; margin: 0; }
    .deal-seller-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      border-top: 1px solid #f1f5f9;
      margin-top: 0.9rem;
      padding-top: 0.9rem;
    }
    .support-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      padding: 0.6rem 0;
      border-bottom: 1px solid #f1f5f9;
    }
    .support-row:last-child { border-bottom: none; }
    .sheet-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(17, 24, 39, 0.45);
      display: flex;
      align-items: flex-end;
      justify-content: center;
      /* Must sit above the bottom nav (z-index 900 in styles.scss) or the
         sheet's confirm button ends up hidden behind it. */
      z-index: 1000;
    }
    .sheet-box {
      background: #fff;
      border-radius: 24px 24px 0 0;
      padding: 1.5rem 1.5rem 2rem;
      width: 100%;
      max-width: 560px;
      text-align: center;
    }
    .sheet-icon { font-size: 2.2rem; }
    .sheet-title {
      font-size: 1.25rem;
      font-weight: 850;
      color: #111827;
      margin: 0.75rem 0 0.5rem;
    }
    .sheet-text { color: #6b7280; font-size: 0.92rem; line-height: 1.6; margin: 0; }
    .confirm-check {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      margin: 1rem 0 0.25rem;
      font-weight: 700;
      color: #111827;
      cursor: pointer;
    }
    .confirm-check input { accent-color: #22c55e; width: 1.1rem; height: 1.1rem; }
  `],
})
export class OpenDisputePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly dealsApi = inject(DealsService);
  readonly lang = inject(LanguageService);

  readonly isLoading = signal(true);
  readonly canOpen = signal(false);
  readonly reasons = signal<LookupItem[]>([]);
  readonly files = signal<File[]>([]);
  readonly isUploading = signal(false);
  readonly isSubmitting = signal(false);
  readonly showConfirm = signal(false);
  readonly opened = signal<DisputeDetail | null>(null);
  readonly error = signal<string | null>(null);

  selectedReasonId: number | null = null;
  details = '';
  confirmed = false;

  private dealId = 0;
  private filePublicIds: string[] = [];

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isFinite(id) || id <= 0) {
      void this.router.navigate(['/home']);
      return;
    }
    this.dealId = id;

    // A dispute that already exists opens its detail page instead.
    this.dealsApi.getDispute(this.dealId).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          void this.router.navigate(['/deals', this.dealId, 'dispute']);
        }
      },
      error: () => {},
    });

    this.dealsApi.getDealDetail(this.dealId).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        // Only the buyer, while the deal is in inspection, may open a dispute.
        this.canOpen.set(
          !!response.success
            && !!response.data?.availableActions.includes('dispute'),
        );
      },
      error: () => {
        this.isLoading.set(false);
        this.canOpen.set(false);
      },
    });

    this.dealsApi.getLookups('dispute-reasons').subscribe({
      next: (response) => this.reasons.set(response.success ? response.data ?? [] : []),
      error: () => {},
    });
  }

  onFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    const picked = Array.from(input.files ?? []);
    input.value = '';
    if (!picked.length) return;
    if (this.files().length + picked.length > 5) {
      this.error.set('open_dispute_max_files');
      return;
    }
    this.error.set(null);
    this.isUploading.set(true);
    let remaining = picked.length;
    for (const file of picked) {
      this.dealsApi.uploadDisputeFile(file).subscribe({
        next: (uploadRes) => {
          const fileId = uploadRes.data?.fileId;
          if (uploadRes.success && fileId) {
            this.files.update((current) => [...current, file]);
            this.filePublicIds.push(fileId);
          } else {
            this.error.set('open_dispute_error');
          }
          if (--remaining === 0) this.isUploading.set(false);
        },
        error: () => {
          this.error.set('open_dispute_error');
          if (--remaining === 0) this.isUploading.set(false);
        },
      });
    }
  }

  removeFile(file: File): void {
    const index = this.files().indexOf(file);
    if (index >= 0) {
      this.files.update((current) => current.filter((_, i) => i !== index));
      this.filePublicIds.splice(index, 1);
    }
  }

  openConfirm(): void {
    if (this.selectedReasonId === null || !this.details.trim()) {
      this.error.set('open_dispute_error');
      return;
    }
    this.error.set(null);
    this.confirmed = false;
    this.showConfirm.set(true);
  }

  closeConfirm(): void {
    this.showConfirm.set(false);
  }

  submit(): void {
    if (this.selectedReasonId === null || !this.confirmed || this.isSubmitting()) return;
    this.isSubmitting.set(true);
    this.error.set(null);
    this.dealsApi
      .openDispute(this.dealId, this.selectedReasonId, this.details.trim(), this.filePublicIds)
      .subscribe({
        next: (response) => {
          this.isSubmitting.set(false);
          if (!response.success) {
            this.error.set('open_dispute_error');
            this.showConfirm.set(false);
            return;
          }
          this.showConfirm.set(false);
          this.dealsApi.getDispute(this.dealId).subscribe({
            next: (detailRes) => {
              if (detailRes.success && detailRes.data) this.opened.set(detailRes.data);
            },
          });
        },
        error: () => {
          this.isSubmitting.set(false);
          this.error.set('open_dispute_error');
          this.showConfirm.set(false);
        },
      });
  }

  formatDate(isoString: string | null | undefined): string {
    if (!isoString) return '';
    try {
      const date = new Date(isoString);
      return date.toLocaleDateString(this.lang.isAr() ? 'ar-SA' : 'en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return isoString;
    }
  }
}
