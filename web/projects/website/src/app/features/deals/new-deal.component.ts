import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { AppHeaderComponent } from '../../core/components/app-header/app-header.component';
import { BottomNavComponent } from '../../core/components/bottom-nav/bottom-nav.component';
import { DealsService, Counterparty, CreateDealRequest, CreatedDeal, LookupItem } from '../../core/services/deals.service';
import { LanguageService } from '../../core/services/language.service';

const twoDecimalPlaces: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  if (control.value === null || control.value === '') return null;
  const amount = Number(control.value);
  if (!Number.isFinite(amount)) return { amountPrecision: true };
  return Math.abs(amount * 100 - Math.round(amount * 100)) < 1e-8 ? null : { amountPrecision: true };
};

@Component({
  selector: 'app-new-deal',
  standalone: true,
  imports: [DecimalPipe, ReactiveFormsModule, AppHeaderComponent, BottomNavComponent],
  templateUrl: './new-deal.component.html',
  styleUrl: './new-deal.component.scss',
})
export class NewDealComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly dealsApi = inject(DealsService);
  readonly lang = inject(LanguageService);

  readonly isSubmitting = signal(false);
  readonly isLoadingCompanies = signal(false);
  readonly companiesError = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly createdDeal = signal<CreatedDeal | null>(null);
  readonly shippingCompanies = signal<LookupItem[]>([]);
  readonly buyerPreview = signal<Counterparty | null>(null);
  readonly isLookingUpBuyer = signal(false);
  readonly buyerLookupError = signal(false);
  readonly selectedImage = signal<File | null>(null);
  readonly imagePreviewUrl = signal<string | null>(null);
  readonly imageUploadError = signal(false);

  lookupBuyer(): void {
    const q = (this.dealForm.controls.buyerPhoneOrCode.value ?? '').trim();
    if (!q || this.isLookingUpBuyer()) return;
    this.isLookingUpBuyer.set(true);
    this.buyerLookupError.set(false);
    this.buyerPreview.set(null);
    this.dealsApi.lookupBuyer(q).subscribe({
      next: (response) => {
        this.isLookingUpBuyer.set(false);
        if (!response.success || !response.data) {
          this.buyerLookupError.set(true);
          return;
        }
        this.buyerPreview.set(response.data);
      },
      error: () => {
        this.isLookingUpBuyer.set(false);
        this.buyerLookupError.set(true);
      },
    });
  }

  readonly dealForm = this.fb.group({
    buyerPhoneOrCode: ['', [Validators.required, Validators.maxLength(50)]],
    title: ['', [Validators.required, Validators.maxLength(150)]],
    description: ['', [Validators.required, Validators.maxLength(200)]],
    amount: [null as number | null, [Validators.required, Validators.min(0.01), Validators.max(10_000_000), twoDecimalPlaces]],
    durationValue: [1, [Validators.required, Validators.min(1), Validators.max(365)]],
    durationUnit: ['day' as 'hour' | 'day', Validators.required],
    needShipping: [false],
    shippingCompanyId: [null as number | null],
    acceptTerms: [false, Validators.requiredTrue],
  });

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login']);
      return;
    }
    this.loadShippingCompanies();
  }

  setShipping(needed: boolean): void {
    const company = this.dealForm.controls.shippingCompanyId;
    this.dealForm.controls.needShipping.setValue(needed);
    if (needed) {
      company.addValidators(Validators.required);
    } else {
      company.clearValidators();
      company.setValue(null);
    }
    company.updateValueAndValidity();
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0] ?? null;
    this.imageUploadError.set(false);
    if (!file) {
      this.selectedImage.set(null);
      this.imagePreviewUrl.set(null);
      return;
    }
    if (!file.type.startsWith('image/')) {
      this.selectedImage.set(null);
      this.imagePreviewUrl.set(null);
      this.imageUploadError.set(true);
      return;
    }
    this.selectedImage.set(file);
    this.imagePreviewUrl.set(URL.createObjectURL(file));
  }

  clearImage(): void {
    this.selectedImage.set(null);
    this.imagePreviewUrl.set(null);
    this.imageUploadError.set(false);
  }

  onSubmit(): void {
    if (this.isSubmitting() || this.createdDeal()) return;
    if (this.dealForm.invalid) {
      this.dealForm.markAllAsTouched();
      return;
    }

    const value = this.dealForm.getRawValue();
    if (value.amount === null) return;

    const request: CreateDealRequest = {
      title: (value.title ?? '').trim(),
      buyerPhoneOrCode: (value.buyerPhoneOrCode ?? '').trim(),
      description: (value.description ?? '').trim(),
      amount: Number(value.amount),
      durationValue: Number(value.durationValue ?? 1),
      durationUnit: value.durationUnit === 'hour' ? 'hour' : 'day',
      needShipping: value.needShipping === true,
      shippingCompanyId: value.needShipping === true && value.shippingCompanyId ? Number(value.shippingCompanyId) : null,
      acceptTerms: value.acceptTerms === true,
      imagePublicId: null as string | null,
    };

    this.isSubmitting.set(true);
    this.submitError.set(null);
    this.imageUploadError.set(false);

    const image = this.selectedImage();
    if (!image) {
      this.submitDeal(request);
      return;
    }

    // The image is uploaded first; only its public id travels with the deal,
    // so the deal is never created with a dangling or foreign image reference.
    this.dealsApi.uploadDealImage(image).subscribe({
      next: (response) => {
        if (!response.success || !response.data?.fileId) {
          this.isSubmitting.set(false);
          this.imageUploadError.set(true);
          return;
        }
        this.submitDeal({ ...request, imagePublicId: response.data.fileId });
      },
      error: () => {
        this.isSubmitting.set(false);
        this.imageUploadError.set(true);
      },
    });
  }

  private submitDeal(request: CreateDealRequest): void {
    this.dealsApi.createDeal(request).subscribe({
      next: (response) => {
        this.isSubmitting.set(false);
        if (!response.success || !response.data) {
          this.submitError.set(this.lang.t('deal_error_generic'));
          return;
        }
        this.createdDeal.set(response.data);
      },
      error: (error: unknown) => {
        this.isSubmitting.set(false);
        this.submitError.set(this.errorMessage(error));
      },
    });
  }

  goHome(): void {
    void this.router.navigate(['/home']);
  }

  private loadShippingCompanies(): void {
    this.isLoadingCompanies.set(true);
    this.dealsApi.getShippingCompanies().subscribe({
      next: (response) => {
        this.isLoadingCompanies.set(false);
        this.shippingCompanies.set(response.success ? response.data ?? [] : []);
      },
      error: () => {
        this.isLoadingCompanies.set(false);
        this.companiesError.set(true);
      },
    });
  }

  private errorMessage(error: unknown): string {
    const envelope = error as {
      error?: { errors?: Array<{ code?: string; field?: string }> };
      errors?: Array<{ code?: string; field?: string }>;
    };
    const issue = envelope?.error?.errors?.[0] ?? envelope?.errors?.[0];
    if (issue?.field === 'shippingCompanyId') {
      return this.lang.t('deal_error_shipping');
    }
    if (issue?.field === 'durationValue' || issue?.field === 'durationUnit') {
      return this.lang.t('deal_error_duration');
    }

    switch (issue?.code) {
      case 'BUYER_NOT_FOUND':
        return this.lang.t('deal_error_buyer_not_found');
      case 'DEAL_SAME_PARTIES':
        return this.lang.t('deal_error_same_party');
      case 'DEAL_AMOUNT_INVALID':
        return this.lang.t('deal_error_amount');
      case 'TERMS_NOT_ACCEPTED':
        return this.lang.t('deal_error_terms');
      default:
        return this.lang.t('deal_error_generic');
    }
  }
}
