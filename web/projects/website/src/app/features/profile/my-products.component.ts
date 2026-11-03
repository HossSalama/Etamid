import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DealsService, ImportRowError, Product, ProductInput } from '../../core/services/deals.service';
import { LanguageService } from '../../core/services/language.service';

/**
 * The seller's own product catalog, rendered inside the store editor section
 * in the profile. Products are display-only in v1: they show on the public
 * store page, but they do not start deals. The sheet import is all-or-nothing:
 * one bad row rejects the whole file and the API reports every bad row.
 */
@Component({
  selector: 'app-my-products',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="my-products">
      <div class="section-title-row">
        <h3 class="section-title">{{ lang.t('product_title') }}</h3>
        <div class="profile-button-row my-products-actions">
          <button type="button" class="btn btn-outline-coral" (click)="downloadTemplate()">
            {{ lang.t('product_template') }}
          </button>
          <input
            #importInput
            class="visually-hidden"
            type="file"
            accept=".xlsx,.xlsm"
            (change)="onImportSelected($event)"
          />
          <button type="button" class="btn btn-outline-coral" [disabled]="isImporting()" (click)="importInput.click()">
            {{ isImporting() ? lang.t('product_importing') : lang.t('product_import') }}
          </button>
          @if (!isEditing()) {
            <button type="button" class="btn btn-black" (click)="startAdd()">
              {{ lang.t('product_add') }}
            </button>
          }
        </div>
      </div>
      <p class="my-products-hint">{{ lang.t('product_import_hint') }}</p>

      @if (message()) {
        <p class="profile-message profile-success" role="status">{{ lang.t(message()!) }}</p>
      }
      @if (formError()) {
        <p class="profile-message profile-error" role="alert">{{ lang.t(formError()!) }}</p>
      }
      @if (importFailedMessage()) {
        <p class="profile-message profile-error" role="alert">{{ importFailedMessage() }}</p>
      }
      @if (importErrors().length) {
        <ul class="my-products-import-errors">
          @for (error of importErrors(); track error.row + '-' + error.field) {
            <li>
              {{ lang.t('product_row') }} {{ error.row }}:
              {{ lang.t('product_field_' + error.field) }} —
              {{ lang.t('product_err_' + error.code) }}
            </li>
          }
        </ul>
      }

      @if (isLoading()) {
        <p>{{ lang.t('product_loading') }}</p>
      } @else if (!products().length && !isEditing()) {
        <p>{{ lang.t('product_empty') }}</p>
      }

      <div class="my-products-list">
        @for (product of products(); track product.id) {
          <article class="my-product-card">
            @if (product.imageUrl) {
              <img class="my-product-thumb" [src]="product.imageUrl" [alt]="product.name" />
            } @else {
              <div class="my-product-thumb-empty">{{ product.name.charAt(0) }}</div>
            }
            <div class="my-product-main">
              <strong>{{ product.name }}</strong>
              @if (product.description) {
                <span>{{ product.description }}</span>
              }
              <small>
                {{ product.price }} {{ lang.t('product_currency') }} ·
                {{ product.quantity > 0 ? lang.t('product_available') : lang.t('product_out_of_stock') }}
                ({{ product.quantity }})
              </small>
            </div>
            <div class="my-product-actions">
              <button type="button" class="btn btn-outline-coral" (click)="startEdit(product)">
                {{ lang.t('product_edit') }}
              </button>
              <button type="button" class="btn btn-outline-coral" (click)="remove(product.id)">
                {{ lang.t('addresses_delete') }}
              </button>
            </div>
          </article>
        }
      </div>
      @if (hasMore()) {
        <button type="button" class="btn btn-outline-coral" (click)="loadMore()">
          {{ lang.t('store_load_more') }}
        </button>
      }

      @if (isEditing()) {
        <form class="my-product-form" [formGroup]="productForm" (ngSubmit)="save()" novalidate>
          <h4>{{ editingId() ? lang.t('product_edit') : lang.t('product_add') }}</h4>
          <div class="my-product-form-grid">
            <label class="profile-form-field">
              <span>{{ lang.t('product_name') }} *</span>
              <input type="text" formControlName="name" maxlength="150" />
              @if (productForm.controls.name.touched && productForm.controls.name.invalid) {
                <small class="profile-field-error">{{ lang.t('store_name_required') }}</small>
              }
            </label>
            <label class="profile-form-field">
              <span>{{ lang.t('product_price') }} *</span>
              <input type="number" formControlName="price" min="0.01" step="0.01" />
            </label>
            <label class="profile-form-field">
              <span>{{ lang.t('product_quantity') }} *</span>
              <input type="number" formControlName="quantity" min="0" step="1" />
            </label>
            <label class="profile-form-field my-product-desc">
              <span>{{ lang.t('product_description') }}</span>
              <textarea formControlName="description" maxlength="1000" rows="3"></textarea>
            </label>
            <div class="profile-form-field">
              <span>{{ lang.t('product_image') }}</span>
              @if (imagePreview() || editingImageUrl()) {
                <img class="my-product-form-image" [src]="imagePreview() ?? editingImageUrl()!" [alt]="lang.t('product_image')" />
              }
              <p><small>{{ lang.t('product_image_hint') }}</small></p>
              <input
                #imageInput
                class="visually-hidden"
                type="file"
                accept="image/png,image/jpeg,image/webp"
                (change)="onImageSelected($event)"
              />
              <button type="button" class="btn btn-outline-coral" (click)="imageInput.click()">
                {{ lang.t('product_image_change') }}
              </button>
            </div>
          </div>
          <div class="profile-button-row">
            <button type="submit" class="btn btn-black" [disabled]="isSaving()">
              {{ isSaving() ? lang.t('product_saving') : lang.t('product_save') }}
            </button>
            <button type="button" class="btn btn-outline-coral" [disabled]="isSaving()" (click)="cancelEdit()">
              {{ lang.t('addresses_cancel') }}
            </button>
          </div>
        </form>
      }
    </div>
  `,
  styles: [`
    .my-products { margin-top: 1.5rem; padding-top: 1.25rem; border-top: 1px solid #f1f5f9; }
    .my-products-actions { margin-top: 0; }
    .my-products-hint { opacity: 0.75; margin: 0.25rem 0 1rem; font-size: 0.85rem; }
    .my-products-import-errors { color: #b91c1c; font-weight: 600; font-size: 0.85rem; }
    .my-products-list { display: grid; gap: 0.75rem; margin-bottom: 1rem; }
    .my-product-card { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: center; border: 1px solid #e5e7eb; border-radius: 14px; padding: 0.75rem 1rem; }
    .my-product-thumb, .my-product-thumb-empty { width: 64px; height: 64px; flex: 0 0 64px; border-radius: 10px; object-fit: cover; }
    .my-product-thumb-empty { display: flex; align-items: center; justify-content: center; background: #fff1f2; color: #eb5757; font-size: 1.4rem; font-weight: 800; }
    .my-product-main { display: flex; flex-direction: column; gap: 0.2rem; min-width: 0; flex: 1; }
    .my-product-main span { opacity: 0.75; font-size: 0.85rem; overflow-wrap: anywhere; }
    .my-product-main small { opacity: 0.75; }
    .my-product-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .my-product-form { margin-top: 1rem; }
    .my-product-form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0.9rem; margin: 0.75rem 0 1rem; }
    .my-product-desc { grid-column: 1 / -1; }
    .my-product-form-image { width: 100%; max-height: 180px; object-fit: cover; border-radius: 12px; }
  `],
})
export class MyProductsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dealsApi = inject(DealsService);
  readonly lang = inject(LanguageService);

  readonly products = signal<Product[]>([]);
  readonly isLoading = signal(false);
  readonly isEditing = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly editingImageUrl = signal<string | null>(null);
  readonly editingImageFileId = signal<string | null>(null);
  readonly isSaving = signal(false);
  readonly isImporting = signal(false);
  readonly formError = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly importFailedMessage = signal<string | null>(null);
  readonly importErrors = signal<ImportRowError[]>([]);
  readonly hasMore = signal(false);
  readonly selectedImage = signal<File | null>(null);
  readonly imagePreview = signal<string | null>(null);
  private page = 1;

  readonly productForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    description: ['', Validators.maxLength(1000)],
    price: [null as number | null, [Validators.required, Validators.min(0.01)]],
    quantity: [null as number | null, [Validators.required, Validators.min(0), Validators.pattern(/^\d+$/)]],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.dealsApi.getMyProducts(this.page).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (!response.success || !response.data) return;
        this.products.update((current) => [...current, ...response.data!]);
        const total = response.meta?.total ?? this.products().length;
        this.hasMore.set(this.products().length < total);
      },
      error: () => this.isLoading.set(false),
    });
  }

  loadMore(): void {
    this.page += 1;
    this.load();
  }

  startAdd(): void {
    this.editingId.set(null);
    this.editingImageUrl.set(null);
    this.editingImageFileId.set(null);
    this.productForm.reset({ name: '', description: '', price: null, quantity: null });
    this.formError.set(null);
    this.message.set(null);
    this.isEditing.set(true);
  }

  startEdit(product: Product): void {
    this.editingId.set(product.id);
    this.editingImageUrl.set(product.imageUrl);
    this.editingImageFileId.set(product.imageFileId);
    this.selectedImage.set(null);
    this.imagePreview.set(null);
    this.productForm.reset({
      name: product.name,
      description: product.description ?? '',
      price: product.price,
      quantity: product.quantity,
    });
    this.formError.set(null);
    this.message.set(null);
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    if (this.isSaving()) return;
    this.isEditing.set(false);
    this.editingId.set(null);
    this.selectedImage.set(null);
    this.imagePreview.set(null);
    this.formError.set(null);
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/') || file.size > 10 * 1024 * 1024) {
      this.selectedImage.set(null);
      this.imagePreview.set(null);
      this.formError.set('product_upload_error');
      return;
    }
    this.formError.set(null);
    this.selectedImage.set(file);
    this.imagePreview.set(URL.createObjectURL(file));
  }

  save(): void {
    if (this.isSaving()) return;
    if (this.productForm.invalid) {
      this.productForm.markAllAsTouched();
      return;
    }
    const v = this.productForm.getRawValue();
    this.isSaving.set(true);
    this.formError.set(null);
    this.message.set(null);
    const file = this.selectedImage();
    const finish = (imagePublicId: string | null) => {
      const input: ProductInput = {
        name: v.name!.trim(),
        description: (v.description ?? '').trim() || null,
        price: v.price!,
        quantity: v.quantity!,
        imagePublicId,
      };
      const id = this.editingId();
      const call = id === null ? this.dealsApi.addProduct(input) : this.dealsApi.updateProduct(id, input);
      call.subscribe({
        next: (response) => {
          this.isSaving.set(false);
          if (!response.success) {
            this.formError.set('product_error');
            return;
          }
          this.isEditing.set(false);
          this.editingId.set(null);
          this.selectedImage.set(null);
          this.imagePreview.set(null);
          this.message.set('product_saved');
          this.page = 1;
          this.products.set([]);
          this.load();
        },
        error: () => {
          this.isSaving.set(false);
          this.formError.set('product_error');
        },
      });
    };
    if (file) {
      this.dealsApi.uploadProductImage(file).subscribe({
        next: (response) => {
          if (!response.success || !response.data?.fileId) {
            this.isSaving.set(false);
            this.formError.set('product_upload_error');
            return;
          }
          finish(response.data.fileId);
        },
        error: () => {
          this.isSaving.set(false);
          this.formError.set('product_upload_error');
        },
      });
    } else {
      finish(this.editingImageFileId());
    }
  }

  remove(id: number): void {
    if (!globalThis.confirm(this.lang.t('product_delete_confirm'))) return;
    this.message.set(null);
    this.formError.set(null);
    this.dealsApi.deleteProduct(id).subscribe({
      next: (response) => {
        if (!response.success) {
          this.formError.set('product_error');
          return;
        }
        this.message.set('product_deleted');
        this.products.update((current) => current.filter((p) => p.id !== id));
      },
      error: () => this.formError.set('product_error'),
    });
  }

  onImportSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.isImporting()) return;
    this.isImporting.set(true);
    this.importFailedMessage.set(null);
    this.importErrors.set([]);
    this.message.set(null);
    this.dealsApi.importProducts(file).subscribe({
      next: (response) => {
        this.isImporting.set(false);
        if (!response.success || !response.data) {
          this.importFailedMessage.set(this.lang.t('product_import_failed'));
          return;
        }
        this.message.set('product_imported');
        this.page = 1;
        this.products.set([]);
        this.load();
      },
      error: (error: unknown) => {
        this.isImporting.set(false);
        const envelope = error as { error?: { message?: string; errors?: Array<{ errors?: ImportRowError[] }> } };
        this.importFailedMessage.set(envelope?.error?.message ?? this.lang.t('product_import_failed'));
        this.importErrors.set(envelope?.error?.errors?.[0]?.errors ?? []);
      },
    });
  }

  downloadTemplate(): void {
    this.dealsApi.downloadTemplate().subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'etamid-products-template.xlsx';
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => {},
    });
  }
}
