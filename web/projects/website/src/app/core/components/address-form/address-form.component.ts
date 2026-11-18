import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import * as L from 'leaflet';
import { AddressInput, DealsService, UserAddress } from '../../services/deals.service';
import { LanguageService } from '../../services/language.service';

/**
 * Shared add/edit address form, used in two places with the exact same
 * fields, validation and map pin:
 *  - the My Addresses page (/addresses and the profile section)
 *  - the accept-deal page, so the buyer can add an address right there
 *    instead of going to the addresses page first.
 *
 * The form includes a Leaflet + OpenStreetMap picker: the user drops a pin
 * on the map (or uses their current location), the pin's coordinates are
 * reverse-geocoded via Nominatim to fill city/district/street, and the user
 * then types the building/apartment details. The coordinates are saved with
 * the address (the backend already accepts latitude/longitude).
 *
 * The parent renders this component only while the form is open (via @if),
 * so a fresh instance is created for every add/edit. On a successful save
 * it emits `saved` with the saved address; on cancel it emits `cancelled`.
 * The parent decides what to do next (reload the list, select the new
 * address, show a message).
 */
@Component({
  selector: 'app-address-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form class="address-form" [formGroup]="addressForm" (ngSubmit)="save()" novalidate>
      <h3>{{ address ? lang.t('addresses_edit_title') : lang.t('addresses_new_title') }}</h3>
      @if (formError()) {
        <p class="profile-message profile-error" role="alert">{{ lang.t(formError()!) }}</p>
      }
      @if (showMap) {
        <div class="address-map-block">
          <span class="address-map-title">{{ lang.t('addresses_map_title') }}</span>
          <p class="addresses-map-hint">{{ lang.t('addresses_map_hint') }}</p>
          <div #mapEl [id]="mapId" class="address-map"></div>
          <div class="address-map-row">
            <button type="button" class="btn btn-outline-coral" (click)="useMyLocation()" [disabled]="isLocating()">
              {{ isLocating() ? lang.t('addresses_locating') : lang.t('addresses_my_location') }}
            </button>
            @if (pickedLat() !== null && pickedLng() !== null) {
              <button type="button" class="btn btn-outline-coral" (click)="clearPin()">
                {{ lang.t('addresses_clear_pin') }}
              </button>
            }
            @if (mapStatus()) {
              <span class="addresses-map-status">{{ lang.t(mapStatus()!) }}</span>
            }
          </div>
        </div>
      } @else if (pickedLat() !== null || mapStatus()) {
        <!-- No map of its own: the parent page's map is the picker. Keep only
             the status line (geocoding / failed) and the remove-pin button. -->
        <div class="address-map-row">
          @if (pickedLat() !== null && pickedLng() !== null) {
            <button type="button" class="btn btn-outline-coral" (click)="clearPin()">
              {{ lang.t('addresses_clear_pin') }}
            </button>
          }
          @if (mapStatus()) {
            <span class="addresses-map-status">{{ lang.t(mapStatus()!) }}</span>
          }
        </div>
      }
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
        <button type="button" class="btn btn-outline-coral" [disabled]="isSaving()" (click)="cancel()">
          {{ lang.t('addresses_cancel') }}
        </button>
      </div>
    </form>
  `,
  styles: [`
    .address-form { margin-top: 1rem; }
    .address-form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0.9rem; margin: 0.75rem 0 1rem; }
    .address-map-block { margin: 0.75rem 0 1rem; }
    .address-map-title { font-weight: 600; }
    .addresses-map-hint { opacity: 0.75; margin: 0.25rem 0 0.5rem; }
    .address-map { height: 260px; border: 1px solid #e5e7eb; border-radius: 14px; overflow: hidden; background: #f3f4f6; z-index: 0; }
    .address-map-row { display: flex; flex-wrap: wrap; align-items: center; gap: 0.75rem; margin-top: 0.5rem; }
    .addresses-map-status { opacity: 0.75; font-size: 0.9rem; }
  `],
})
export class AddressFormComponent implements OnInit, OnChanges, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly dealsApi = inject(DealsService);
  readonly lang = inject(LanguageService);

  /** The address to edit, or null to add a new one. Set once, before ngOnInit. */
  @Input() address: UserAddress | null = null;

  /**
   * Optional starting pin for a NEW address (address must be null). The
   * accept-deal page sets this when the buyer taps its top map (or "my
   * location"), so the form opens with the pin already there and the
   * city/district/street filled in from the reverse-geocode.
   */
  @Input() initialLocation: { latitude: number; longitude: number } | null = null;

  /**
   * Whether this form shows its own map. The My Addresses page uses the
   * map (true); the accept-deal page has its own live map on top, so it
   * passes false and the top map becomes this form's picker instead.
   */
  @Input() showMap = true;

  /** Emitted with the saved address (from the API response) after a successful save. */
  @Output() saved = new EventEmitter<UserAddress>();
  /** Emitted when the user cancels. */
  @Output() cancelled = new EventEmitter<void>();
  /** Emitted when the user removes the pin (the parent clears its own map pin too). */
  @Output() pinCleared = new EventEmitter<void>();

  @ViewChild('mapEl') mapEl?: ElementRef<HTMLDivElement>;

  // Unique map element id per instance, so two forms on the same page can
  // never collide on the hardcoded id.
  readonly mapId = 'address-map-' + Math.random().toString(36).slice(2, 9);

  readonly isSaving = signal(false);
  readonly formError = signal<string | null>(null);

  // Map picker state: the pin's coordinates (null until the user drops a pin).
  readonly pickedLat = signal<number | null>(null);
  readonly pickedLng = signal<number | null>(null);
  readonly mapStatus = signal<string | null>(null);
  readonly isLocating = signal(false);

  private map: L.Map | null = null;
  private marker: L.Marker | null = null;
  private geocodeTimer: ReturnType<typeof setTimeout> | null = null;
  private openMapTimer: ReturnType<typeof setTimeout> | null = null;
  private destroyed = false;
  // Sequence number for reverse-geocode requests: a response is applied only
  // if it is still the latest one, so an older (slower) response can never
  // overwrite the address of a newer pin position.
  private geocodeSeq = 0;

  // The database stores coordinates as decimal(9,6) — 6 decimal places
  // (~10 cm). Round the pin to 6 decimals before saving so we never send a
  // 15-digit double that the database would round anyway.
  private static readonly coordScale = 1e6;

  // Default map center (Jeddah) when there is no saved pin yet.
  private static readonly defaultCenter: L.LatLngExpression = [21.4858, 39.1925];

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
    // With no map of its own (accept page) there is nothing to open — the
    // parent page's map is the picker.
    if (this.showMap) this.openMap();
  }

  /**
   * Runs on creation and whenever the parent switches the address being
   * edited (e.g. the user taps Edit on another address while the form is
   * open). Re-initialises the form and the pin from the new input; if the
   * map already exists it is re-centred on the new pin instead of being
   * recreated.
   */
  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['address'] && !changes['initialLocation']) return;
    this.initFromAddress();
  }

  private initFromAddress(): void {
    const a = this.address;
    if (a) {
      this.addressForm.reset({
        label: a.label ?? '',
        recipientName: a.recipientName,
        recipientPhone: a.recipientPhone,
        city: a.city,
        district: a.district ?? '',
        street: a.street ?? '',
        buildingNo: a.buildingNo ?? '',
        postalCode: a.postalCode ?? '',
        additionalNo: a.additionalNo ?? '',
      });
      this.pickedLat.set(a.latitude ?? null);
      this.pickedLng.set(a.longitude ?? null);
    } else {
      this.addressForm.reset();
      // New address: start from the parent's tapped location, if any.
      this.pickedLat.set(this.initialLocation?.latitude ?? null);
      this.pickedLng.set(this.initialLocation?.longitude ?? null);
    }
    this.mapStatus.set(null);
    this.formError.set(null);
    this.syncMapToPin();
    // Without a map of its own (accept page) the pin comes from the parent's
    // top map — still reverse-geocode it so the fields fill in.
    if (!this.showMap && this.address === null && this.pickedLat() !== null && this.pickedLng() !== null) {
      this.reverseGeocode(this.pickedLat()!, this.pickedLng()!);
    }
  }

  /** Re-centre the (already existing) map on the current pin, or clear it. */
  private syncMapToPin(): void {
    const map = this.map;
    if (!map) return;
    const lat = this.pickedLat();
    const lng = this.pickedLng();
    if (lat !== null && lng !== null) {
      map.setView([lat, lng], 16);
      this.placePin(lat, lng, { reverse: false, center: false });
    } else {
      if (this.marker) {
        map.removeLayer(this.marker);
        this.marker = null;
      }
      map.setView(AddressFormComponent.defaultCenter, 12);
    }
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.destroyMap();
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
      latitude: this.pickedLat(),
      longitude: this.pickedLng(),
    };
    this.isSaving.set(true);
    this.formError.set(null);
    const id = this.address?.id ?? null;
    const call = id === null ? this.dealsApi.addAddress(input) : this.dealsApi.updateAddress(id, input);
    call.subscribe({
      next: (response) => {
        this.isSaving.set(false);
        if (!response.success || !response.data) {
          this.formError.set(this.errorKey(response));
          return;
        }
        this.destroyMap();
        this.saved.emit(response.data);
      },
      error: (error: unknown) => {
        this.isSaving.set(false);
        this.formError.set(this.errorKey(error));
      },
    });
  }

  cancel(): void {
    if (this.isSaving()) return;
    this.destroyMap();
    this.cancelled.emit();
  }

  /**
   * Set (or move) the pin from the parent — used by the accept-deal page
   * when the buyer taps its top map while this form is already open.
   * Reverse-geocodes the new position, so city/district/street follow the pin.
   */
  setLocation(lat: number, lng: number): void {
    this.placePin(lat, lng, { center: true });
  }

  /** Center the map on the browser's current location and drop the pin there. */
  useMyLocation(): void {
    if (!('geolocation' in navigator)) {
      this.mapStatus.set('addresses_location_failed');
      return;
    }
    this.isLocating.set(true);
    this.mapStatus.set('addresses_locating');
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        this.isLocating.set(false);
        this.placePin(pos.coords.latitude, pos.coords.longitude, { center: true });
      },
      () => {
        this.isLocating.set(false);
        this.mapStatus.set('addresses_location_failed');
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  }

  /** Remove the pin (the address stays text-only; the backend accepts null lat/lng). */
  clearPin(): void {
    this.pickedLat.set(null);
    this.pickedLng.set(null);
    if (this.marker && this.map) {
      this.map.removeLayer(this.marker);
      this.marker = null;
    }
    if (this.geocodeTimer !== null) {
      globalThis.clearTimeout(this.geocodeTimer);
      this.geocodeTimer = null;
    }
    this.geocodeSeq++;
    this.mapStatus.set(null);
    this.pinCleared.emit();
  }

  /**
   * The map container renders with the form, so wait a tick for Angular to
   * render it before creating the Leaflet map. The timer is tracked so it
   * can be cancelled if the form closes first.
   */
  private openMap(): void {
    if (this.openMapTimer !== null) globalThis.clearTimeout(this.openMapTimer);
    this.openMapTimer = globalThis.setTimeout(() => {
      this.openMapTimer = null;
      this.initMap();
    }, 0);
  }

  private initMap(): void {
    // The form may have been closed (cancel / save / navigate away) before
    // the timer fired — never create a map for a closed form.
    if (this.destroyed) return;
    const el = this.mapEl?.nativeElement ?? globalThis.document.getElementById(this.mapId);
    if (!el || this.map) return;
    const lat = this.pickedLat();
    const lng = this.pickedLng();
    const hasPin = lat !== null && lng !== null;
    const map = L.map(el).setView(
      hasPin ? [lat, lng] : AddressFormComponent.defaultCenter,
      hasPin ? 16 : 12,
    );
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '© OpenStreetMap contributors',
    }).addTo(map);
    map.on('click', (e: L.LeafletMouseEvent) => this.placePin(e.latlng.lat, e.latlng.lng));
    this.map = map;
    if (hasPin) {
      // A pin from initialLocation (new address) gets reverse-geocoded so
      // the fields fill in; a pin from an edited address never does, so the
      // saved city/district/street are never overwritten by a guess.
      const fromInitial = this.address === null && this.initialLocation !== null;
      this.placePin(lat, lng, { reverse: fromInitial, center: false });
    }
    map.invalidateSize();
  }

  private destroyMap(): void {
    if (this.openMapTimer !== null) {
      globalThis.clearTimeout(this.openMapTimer);
      this.openMapTimer = null;
    }
    if (this.geocodeTimer !== null) {
      globalThis.clearTimeout(this.geocodeTimer);
      this.geocodeTimer = null;
    }
    if (this.map) {
      this.map.remove();
      this.map = null;
    }
    this.marker = null;
  }

  private placePin(lat: number, lng: number, opts: { reverse?: boolean; center?: boolean } = {}): void {
    // Leaflet gives [lat, lng] (lat first) everywhere — e.latlng, getLatLng(),
    // setLatLng() and setView() all take latitude first, longitude second.
    // The backend stores them as separate named fields (Latitude/Longitude),
    // so the mapping is: lat -> latitude, lng -> longitude. Round to 6
    // decimals to match the database column decimal(9,6).
    const rLat = Math.round(lat * AddressFormComponent.coordScale) / AddressFormComponent.coordScale;
    const rLng = Math.round(lng * AddressFormComponent.coordScale) / AddressFormComponent.coordScale;
    this.pickedLat.set(rLat);
    this.pickedLng.set(rLng);
    // Reverse-geocode even without a map (accept page: the top map is the
    // picker, this form has none) — the fields still need the address.
    if (opts.reverse !== false) this.reverseGeocode(rLat, rLng);
    const map = this.map;
    if (!map) return;
    if (!this.marker) {
      this.marker = L.marker([rLat, rLng], { draggable: true, icon: this.pinIcon() }).addTo(map);
      this.marker.on('dragend', () => {
        const p = this.marker!.getLatLng();
        this.placePin(p.lat, p.lng);
      });
    } else {
      this.marker.setLatLng([rLat, rLng]);
    }
    if (opts.center) map.setView([rLat, rLng], 16);
  }

  /**
   * A CSS-only pin (no image file), so there is no marker-icon path to break
   * in the Angular build. Leaflet creates the marker element dynamically, so
   * the pin is styled inline instead of relying on component-scoped CSS.
   */
  private pinIcon(): L.DivIcon {
    return L.divIcon({
      className: 'etamid-pin',
      html: '<div style="width:28px;height:28px;background:#e11d48;border:3px solid #fff;border-radius:50% 50% 50% 0;transform:rotate(-45deg);box-shadow:0 2px 6px rgba(0,0,0,.35);"></div>',
      iconSize: [28, 28],
      iconAnchor: [14, 28],
    });
  }

  /**
   * Reverse-geocode the pin via Nominatim (OpenStreetMap) and fill
   * city/district/street/postcode. Only non-empty values are patched, so a
   * missing field never clears what the user typed. Nominatim asks for at most
   * 1 request/second, so pin moves are debounced. A response is applied only
   * if it is still the latest request and the form is still open, so a slow
   * older response can never overwrite a newer pin's address.
   *
   * Nominatim's address fields (verified live for Jeddah, Riyadh and a rural
   * point): city comes from city/town/village/municipality/county (the rural
   * point only has `town`, so that fallback is essential); district from
   * suburb/neighbourhood/quarter/district (Riyadh uses `suburb`, Jeddah uses
   * `neighbourhood`); street from `road`; postcode from `postcode`. `state`
   * is the region (e.g. Makkah Region) and is deliberately NOT used as a city.
   */
  private reverseGeocode(lat: number, lng: number): void {
    if (this.geocodeTimer !== null) globalThis.clearTimeout(this.geocodeTimer);
    this.mapStatus.set('addresses_geocoding');
    const seq = ++this.geocodeSeq;
    this.geocodeTimer = globalThis.setTimeout(() => {
      const lang = this.lang.isAr() ? 'ar' : 'en';
      const url =
        `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${lat}` +
        `&lon=${lng}&addressdetails=1&accept-language=${lang}`;
      globalThis
        .fetch(url, { headers: { Accept: 'application/json' } })
        .then((r) => (r.ok ? r.json() : Promise.reject(new Error('geocode failed'))))
        .then((data: { address?: Record<string, string | undefined> }) => {
          // Stale response (a newer pin move already fired) or the form was
          // closed meanwhile — ignore it. A form without its own map
          // (accept page) has no map to check, so only check it when it
          // is supposed to have one.
          if (seq !== this.geocodeSeq || this.destroyed || (this.showMap && !this.map)) return;
          const a = data.address ?? {};
          const patch: Record<string, string> = {};
          const city = a['city'] ?? a['town'] ?? a['village'] ?? a['municipality'] ?? a['county'] ?? '';
          const district = a['suburb'] ?? a['neighbourhood'] ?? a['quarter'] ?? a['district'] ?? '';
          const street = a['road'] ?? '';
          const postal = a['postcode'] ?? '';
          // Truncate to the form/backend maxlengths so a long geocoded value
          // can never leave the form invalid with a filled field.
          if (city) patch['city'] = city.slice(0, 80);
          if (district) patch['district'] = district.slice(0, 80);
          if (street) patch['street'] = street.slice(0, 150);
          if (postal) patch['postalCode'] = postal.slice(0, 10);
          if (Object.keys(patch).length) this.addressForm.patchValue(patch);
          this.mapStatus.set(null);
        })
        .catch(() => {
          if (seq !== this.geocodeSeq || this.destroyed || (this.showMap && !this.map)) return;
          this.mapStatus.set('addresses_geocode_failed');
        });
    }, 600);
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
