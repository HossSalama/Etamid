import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DealsService, LookupItem, MyStore } from '../../core/services/deals.service';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * The seller's own store editor, embedded in the profile section. Editing the
 * store creates the profile when it does not exist yet. The banner round-trips
 * through its public file id: saving without a new banner keeps the old one
 * instead of clearing it.
 */
@Component({
  selector: 'app-store-editor',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="store-editor-section">
      <div class="section-title-row">
        <h2 class="section-title">{{ lang.t('store_edit_title') }}</h2>
        @if (auth.currentUser()?.id) {
          <a class="btn btn-outline-coral" [routerLink]="['/store', auth.currentUser()!.id]">
            {{ lang.t('store_view_public') }}
          </a>
        }
      </div>

      @if (saved()) {
        <p class="profile-message profile-success" role="status">{{ lang.t('store_saved') }}</p>
      }
      @if (saveError()) {
        <p class="profile-message profile-error" role="alert">{{ lang.t(saveError()!) }}</p>
      }

      @if (store(); as s) {
        <p class="store-editor-stats">
          {{ s.rating.toFixed(1) }} ★ · {{ s.successfulDeals }} {{ lang.t('store_successful_deals') }}
        </p>
      }

      <form class="store-editor-form" [formGroup]="storeForm" (ngSubmit)="save()" novalidate>
        <label class="address-field">
          <span>{{ lang.t('store_name') }} *</span>
          <input type="text" formControlName="storeName" maxlength="150" />
          @if (storeForm.controls.storeName.touched && storeForm.controls.storeName.invalid) {
            <small class="profile-field-error">{{ lang.t('store_name_required') }}</small>
          }
        </label>
        <label class="address-field">
          <span>{{ lang.t('store_category') }}</span>
          <select formControlName="categoryId">
            <option [ngValue]="null">{{ lang.t('store_category_placeholder') }}</option>
            @for (category of categories(); track category.id) {
              <option [ngValue]="category.id">{{ category.name }}</option>
            }
          </select>
        </label>
        <div class="address-field">
          <span>{{ lang.t('store_banner') }}</span>
          @if (bannerPreview() || store()?.bannerUrl) {
            <img class="store-editor-banner" [src]="bannerPreview() ?? store()!.bannerUrl!" [alt]="lang.t('store_banner')" />
          }
          <p><small>{{ lang.t('store_banner_hint') }}</small></p>
          <input
            #bannerInput
            class="visually-hidden"
            type="file"
            accept="image/png,image/jpeg,image/webp"
            (change)="onBannerSelected($event)"
          />
          <button type="button" class="btn btn-outline-coral" (click)="bannerInput.click()">
            {{ lang.t('store_banner_change') }}
          </button>
        </div>
        <div class="profile-button-row">
          <button type="submit" class="btn btn-black" [disabled]="isSaving()">
            {{ isSaving() ? lang.t('store_saving') : lang.t('store_save') }}
          </button>
        </div>
      </form>
    </section>
  `,
  styles: [`
    .store-editor-section { margin-top: 1.5rem; }
    .store-editor-stats { font-weight: 700; margin: 0.25rem 0 1rem; }
    .store-editor-form { display: grid; gap: 0.75rem; border: 1px solid #e5e5e5; border-radius: 12px; padding: 1rem; }
    .store-editor-banner { width: 100%; max-height: 180px; object-fit: cover; border-radius: 12px; }
  `],
})
export class StoreEditorComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dealsApi = inject(DealsService);
  readonly auth = inject(AuthService);
  readonly lang = inject(LanguageService);

  readonly store = signal<MyStore | null>(null);
  readonly categories = signal<LookupItem[]>([]);
  readonly isSaving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly saved = signal(false);
  readonly selectedBanner = signal<File | null>(null);
  readonly bannerPreview = signal<string | null>(null);

  readonly storeForm = this.fb.group({
    storeName: ['', [Validators.required, Validators.maxLength(150)]],
    categoryId: [null as number | null],
  });

  ngOnInit(): void {
    this.dealsApi.getMyStore().subscribe({
      next: (response) => {
        if (!response.success || !response.data) return;
        const s = response.data;
        this.store.set(s);
        this.storeForm.reset({ storeName: s.name, categoryId: s.categoryId });
      },
      error: () => {},
    });
    this.dealsApi.getLookups('store-categories').subscribe({
      next: (response) => {
        if (response.success && response.data) this.categories.set(response.data);
      },
      error: () => {},
    });
  }

  onBannerSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/') || file.size > 10 * 1024 * 1024) {
      this.selectedBanner.set(null);
      this.bannerPreview.set(null);
      this.saveError.set('store_upload_error');
      return;
    }
    this.saveError.set(null);
    this.selectedBanner.set(file);
    this.bannerPreview.set(URL.createObjectURL(file));
  }

  save(): void {
    if (this.isSaving()) return;
    if (this.storeForm.invalid) {
      this.storeForm.markAllAsTouched();
      return;
    }
    const values = this.storeForm.getRawValue();
    this.isSaving.set(true);
    this.saveError.set(null);
    this.saved.set(false);
    const file = this.selectedBanner();
    const finish = (bannerFileId: string | null) => {
      this.dealsApi.saveMyStore({
        storeName: values.storeName!.trim(),
        categoryId: values.categoryId,
        bannerFileId,
      }).subscribe({
        next: (response) => {
          this.isSaving.set(false);
          if (!response.success) {
            this.saveError.set('store_error');
            return;
          }
          this.saved.set(true);
          this.selectedBanner.set(null);
          this.bannerPreview.set(null);
          if (response.data) this.store.set(response.data);
        },
        error: () => {
          this.isSaving.set(false);
          this.saveError.set('store_error');
        },
      });
    };
    if (file) {
      this.dealsApi.uploadBanner(file).subscribe({
        next: (response) => {
          if (!response.success || !response.data?.fileId) {
            this.isSaving.set(false);
            this.saveError.set('store_upload_error');
            return;
          }
          finish(response.data.fileId);
        },
        error: () => {
          this.isSaving.set(false);
          this.saveError.set('store_upload_error');
        },
      });
    } else {
      finish(this.store()?.bannerFileId ?? null);
    }
  }
}
