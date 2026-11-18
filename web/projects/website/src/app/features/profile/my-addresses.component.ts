import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { DealsService, UserAddress } from '../../core/services/deals.service';
import { LanguageService } from '../../core/services/language.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';
import { AddressFormComponent } from '../../core/components/address-form/address-form.component';

/**
 * Saved delivery addresses, used by the profile section and the standalone
 * /addresses route.
 *
 * The add/edit form itself lives in the shared AddressFormComponent (same
 * form, fields, validation and map pin as the accept-deal page uses): the
 * user drops a pin on the map (or uses their current location), the pin's
 * coordinates are reverse-geocoded via Nominatim to fill
 * city/district/street, and the user then types the building/apartment
 * details. The coordinates are saved with the address (the backend already
 * accepts latitude/longitude).
 */
@Component({
  selector: 'app-my-addresses',
  standalone: true,
  imports: [CommonModule, AppHeaderComponent, BottomNavComponent, AddressFormComponent],
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
          <app-address-form
            [address]="editingAddress()"
            (saved)="onAddressSaved($event)"
            (cancelled)="onAddressCancelled()"
          />
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
  `],
})
export class MyAddressesComponent implements OnInit {
  private readonly dealsApi = inject(DealsService);
  private readonly router = inject(Router);
  readonly lang = inject(LanguageService);

  readonly isStandalone = computed(() => this.router.url.split('?')[0] === '/addresses');

  readonly addresses = signal<UserAddress[]>([]);
  readonly isLoading = signal(false);
  readonly loadError = signal(false);
  readonly isEditing = signal(false);
  readonly editingAddress = signal<UserAddress | null>(null);
  readonly formError = signal<string | null>(null);
  readonly message = signal<string | null>(null);

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
    this.editingAddress.set(null);
    this.formError.set(null);
    this.message.set(null);
    this.isEditing.set(true);
  }

  startEdit(address: UserAddress): void {
    this.editingAddress.set(address);
    this.formError.set(null);
    this.message.set(null);
    this.isEditing.set(true);
  }

  onAddressSaved(_saved: UserAddress): void {
    this.isEditing.set(false);
    this.editingAddress.set(null);
    this.message.set('addresses_saved');
    this.load();
  }

  onAddressCancelled(): void {
    this.isEditing.set(false);
    this.editingAddress.set(null);
    this.formError.set(null);
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

  private errorKey(error: unknown): string {
    const envelope = error as { error?: { errors?: Array<{ code?: string }> }; errors?: Array<{ code?: string }> };
    const code = envelope?.error?.errors?.[0]?.code ?? envelope?.errors?.[0]?.code;
    return code === 'ADDRESS_LIMIT_REACHED' ? 'addresses_limit' : 'addresses_error';
  }
}
