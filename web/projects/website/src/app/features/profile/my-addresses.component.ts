import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AddressInput, DealsService, UserAddress } from '../../core/services/deals.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';

/**
 * Saved delivery addresses, used by the profile section and the standalone
 * /addresses route.
 */
@Component({
  selector: 'app-my-addresses',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AppHeaderComponent, BottomNavComponent],
  template: `
    @if (isStandalone()) {
      <div class="app-screen">
        <app-header [title]="lang.t('addresses_title')" backUrl="/home" />
        <main class="app-page-shell" style="max-width: 820px;">
          <ng-container *ngTemplateOutlet="addressContent" />
        </main>
        <app-bottom-nav activeNav="profile" />
      </div>
    } @else {
      <ng-container *ngTemplateOutlet="addressContent" />
    }

    <ng-template #addressContent>
      <section class="profile-card addresses-section">
        <div class="section-title-row">
          <h2 class="section-title">{{ lang.t('addresses_title') }}</h2>
          @if (!isEditing() && addresses().length < 10) {
            <button type="button" class="btn btn-outline-coral" (click)="startAdd()">
              {{ lang.t('addresses_add') }}
            </button>
          }
        </div>
        <p class="addresses-subtitle">{{ lang.t('addresses_subtitle') }}</p>

        @if (message()) {
          <p class="profile-message profile-success" role="status">{{ lang.t(message()!) }}</p>
        }
        @if (formError()) {
          <p class="profile-message profile-error" role="alert">{{ lang.t(formError()!) }}</p>
        }

        @if (isLoading()) {
          <p>{{ lang.t('addresses_loading') }}</p>
        } @else if (loadError()) {
          <p class="profile-message profile-error" role="alert">{{ lang.t('addresses_error') }}</p>
        } @else if (!addresses().length && !isEditing()) {
          <p>{{ lang.t('addresses_empty') }}</p>
        }

        @if (addresses().length >= 10) {
          <p class="addresses-limit">{{ lang.t('addresses_limit') }}</p>
        }

        <div class="addresses-list">
          @for (address of addresses(); track address.id) {
            <article class="address-card">
              <div class="address-card-main">
                <strong>{{ address.label || address.city }}</strong>
                <span>{{ address.fullText }}</span>
                <small>{{ address.recipientName }} · <span dir="ltr">{{ address.recipientPhone }}</span></small>
                @if (address.isDefault) {
                  <span class="badge badge-success">{{ lang.t('addresses_default') }}</span>
                }
              </div>
              <div class="address-card-actions">
                @if (!address.isDefault) {
                  <button type="button" class="btn btn-sm btn-outline-coral" (click)="setDefault(address.id)">
                    {{ lang.t('addresses_set_default') }}
                  </button>
                }
                <button type="button" class="btn btn-sm btn-outline-coral" (click)="startEdit(address)">
                  {{ lang.t('addresses_edit_title') }}
                </button>
                <button type="button" class="btn btn-sm btn-outline-coral" style="color: #b91c1c !important; border-color: #fecaca;" (click)="remove(address.id)">
                  {{ lang.t('addresses_delete') }}
                </button>
              </div>
            </article>
          }
        </div>

        @if (isEditing()) {
          <form class="address-form" [formGroup]="addressForm" (ngSubmit)="save()" novalidate>
            <h3>{{ editingId() ? lang.t('addresses_edit_title') : lang.t('addresses_new_title') }}</h3>
            <div class="address-form-grid">
              <label class="profile-form-field">
                <span>{{ lang.t('addresses_label') }}</span>
                <input type="text" formControlName="label" maxlength="50" [placeholder]="lang.t('addresses_label_placeholder')" />
              </label>
              <label class="profile-form-field">
                <span>{{ lang.t('addresses_recipient_name') }} *</span>
                <input type="text" formControlName="recipientName" maxlength="150" />
                @if (addressForm.controls.recipientName.touched && addressForm.controls.recipientName.invalid) {
                  <small class="profile-field-error">{{ lang.t('addresses_required') }}</small>
                }
              </label>
              <label class="profile-form-field">
                <span>{{ lang.t('addresses_recipient_phone') }} *</span>
                <input type="tel" formControlName="recipientPhone" dir="ltr" maxlength="10" placeholder="05XXXXXXXX" />
                @if (addressForm.controls.recipientPhone.touched && addressForm.controls.recipientPhone.invalid) {
                  <small class="profile-field-error">{{ lang.t('addresses_phone_error') }}</small>
                }
              </label>
              <label class="profile-form-field">
                <span>{{ lang.t('addresses_city') }} *</span>
                <input type="text" formControlName="city" maxlength="80" />
                @if (addressForm.controls.city.touched && addressForm.controls.city.invalid) {
                  <small class="profile-field-error">{{ lang.t('addresses_required') }}</small>
                }
              </label>
              <label class="profile-form-field">
                <span>{{ lang.t('addresses_district') }}</span>
                <input type="text" formControlName="district" maxlength="80" />
              </label>
              <label class="profile-form-field">
                <span>{{ lang.t('addresses_street') }}</span>
                <input type="text" formControlName="street" maxlength="150" />
              </label>
              <label class="profile-form-field">
                <span>{{ lang.t('addresses_building') }}</span>
                <input type="text" formControlName="buildingNo" maxlength="20" />
              </label>
              <label class="profile-form-field">
                <span>{{ lang.t('addresses_postal') }}</span>
                <input type="text" formControlName="postalCode" maxlength="10" />
              </label>
              <label class="profile-form-field">
                <span>{{ lang.t('addresses_additional') }}</span>
                <input type="text" formControlName="additionalNo" maxlength="10" />
              </label>
            </div>
            <div class="profile-button-row">
              <button type="submit" class="btn btn-black" [disabled]="isSaving()">
                {{ isSaving() ? lang.t('addresses_saving') : lang.t('addresses_save') }}
              </button>
              <button type="button" class="btn btn-outline-coral" [disabled]="isSaving()" (click)="cancelEdit()">
                {{ lang.t('addresses_cancel') }}
              </button>
            </div>
          </form>
        }
      </section>
    </ng-template>
  `,
  styles: [`
    .addresses-section { margin-top: 1.5rem; }
    .addresses-subtitle { opacity: 0.75; margin: 0.25rem 0 1rem; }
    .addresses-limit { font-weight: 600; color: #b45309; }
    .addresses-list { display: grid; gap: 0.75rem; margin-bottom: 1rem; }
    .address-card { display: flex; flex-wrap: wrap; gap: 0.75rem; justify-content: space-between; align-items: center; border: 1px solid #e5e7eb; border-radius: 14px; padding: 1rem; background: #fff; }
    .address-card-main { display: flex; flex-direction: column; gap: 0.25rem; }
    .address-card-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .address-form { margin-top: 1rem; }
    .address-form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0.9rem; margin: 0.75rem 0 1rem; }
  `],
})
export class MyAddressesComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dealsApi = inject(DealsService);
  private readonly router = inject(Router);
  readonly lang = inject(LanguageService);

  readonly isStandalone = computed(() => this.router.url.split('?')[0] === '/addresses');

  readonly addresses = signal<UserAddress[]>([]);
  readonly isLoading = signal(false);
  readonly loadError = signal(false);
  readonly isEditing = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly isSaving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly message = signal<string | null>(null);

  readonly addressForm = this.fb.group({
    label: ['', Validators.maxLength(50)],
    recipientName: ['', [Validators.required, Validators.maxLength(150)]],
    recipientPhone: ['', [Validators.required, Validators.pattern(/^05\d{8}$/)]],
    city: ['', [Validators.required, Validators.maxLength(80)]],
    district: ['', Validators.maxLength(80)],
    street: ['', Validators.maxLength(150)],
    buildingNo: ['', Validators.maxLength(20)],
    postalCode: ['', Validators.maxLength(10)],
    additionalNo: ['', Validators.maxLength(10)],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.loadError.set(false);
    this.dealsApi.getMyAddresses().subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (!response.success || !response.data) {
          this.loadError.set(true);
          return;
        }
        this.addresses.set(response.data);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadError.set(true);
      },
    });
  }

  startAdd(): void {
    if (this.addresses().length >= 10) return;
    this.editingId.set(null);
    this.addressForm.reset();
    this.formError.set(null);
    this.message.set(null);
    this.isEditing.set(true);
  }

  startEdit(address: UserAddress): void {
    this.editingId.set(address.id);
    this.addressForm.reset({
      label: address.label ?? '',
      recipientName: address.recipientName,
      recipientPhone: address.recipientPhone,
      city: address.city,
      district: address.district ?? '',
      street: address.street ?? '',
      buildingNo: address.buildingNo ?? '',
      postalCode: address.postalCode ?? '',
      additionalNo: address.additionalNo ?? '',
    });
    this.formError.set(null);
    this.message.set(null);
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    if (this.isSaving()) return;
    this.isEditing.set(false);
    this.editingId.set(null);
    this.formError.set(null);
  }

  save(): void {
    if (this.isSaving()) return;
    if (this.addressForm.invalid) {
      this.addressForm.markAllAsTouched();
      return;
    }
    const v = this.addressForm.getRawValue();
    const input: AddressInput = {
      label: this.orNull(v.label),
      recipientName: v.recipientName!.trim(),
      recipientPhone: v.recipientPhone!.trim(),
      city: v.city!.trim(),
      district: this.orNull(v.district),
      street: this.orNull(v.street),
      buildingNo: this.orNull(v.buildingNo),
      postalCode: this.orNull(v.postalCode),
      additionalNo: this.orNull(v.additionalNo),
    };
    this.isSaving.set(true);
    this.formError.set(null);
    this.message.set(null);
    const id = this.editingId();
    const call = id === null ? this.dealsApi.addAddress(input) : this.dealsApi.updateAddress(id, input);
    call.subscribe({
      next: (response) => {
        this.isSaving.set(false);
        if (!response.success) {
          this.formError.set(this.errorKey(response));
          return;
        }
        this.isEditing.set(false);
        this.editingId.set(null);
        this.message.set('addresses_saved');
        this.load();
      },
      error: (error: unknown) => {
        this.isSaving.set(false);
        this.formError.set(this.errorKey(error));
      },
    });
  }

  setDefault(id: number): void {
    this.message.set(null);
    this.formError.set(null);
    this.dealsApi.setDefaultAddress(id).subscribe({
      next: (response) => {
        if (!response.success) {
          this.formError.set(this.errorKey(response));
          return;
        }
        this.message.set('addresses_default_saved');
        this.load();
      },
      error: (error: unknown) => this.formError.set(this.errorKey(error)),
    });
  }

  remove(id: number): void {
    if (!globalThis.confirm(this.lang.t('addresses_delete_confirm'))) return;
    this.message.set(null);
    this.formError.set(null);
    this.dealsApi.deleteAddress(id).subscribe({
      next: (response) => {
        if (!response.success) {
          this.formError.set(this.errorKey(response));
          return;
        }
        this.message.set('addresses_deleted');
        this.load();
      },
      error: (error: unknown) => this.formError.set(this.errorKey(error)),
    });
  }

  private orNull(value: string | null | undefined): string | null {
    const trimmed = (value ?? '').trim();
    return trimmed ? trimmed : null;
  }

  private errorKey(error: unknown): string {
    const envelope = error as { error?: { errors?: Array<{ code?: string }> }; errors?: Array<{ code?: string }> };
    const code = envelope?.error?.errors?.[0]?.code ?? envelope?.errors?.[0]?.code;
    return code === 'ADDRESS_LIMIT_REACHED' ? 'addresses_limit' : 'addresses_error';
  }
}
