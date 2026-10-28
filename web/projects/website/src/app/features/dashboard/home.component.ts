import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/services/language.service';

export interface DealItem {
  id: string;
  code: string;
  counterParty: string;
  role: 'مشتري' | 'بائع';
  title: string;
  description: string;
  amount: number;
  date: string;
  status: 'pending' | 'in_progress' | 'completed' | 'declined';
  statusText: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule],
  template: `
    @if (authService.isAuthenticated()) {
    <div class="app-screen home-screen">
      <!-- Top Red/Coral Header Card -->
      <header class="home-header-card">
        <div class="header-top-row">
          <div class="header-greeting">
            @if (authService.isAuthenticated()) {
              <span class="welcome-text">{{ lang.t('home_welcome_back') }}</span>
              <h1 class="user-display-name">{{ authService.currentUser()?.name }}</h1>
              <div class="header-tags">
                <span class="merchant-code">{{ lang.t('home_code') }}: {{ authService.currentUser()?.code }}</span>
                @if (authService.currentUser()?.nafathVerified) {
                  <span class="nafath-badge">{{ lang.t('home_nafath_verified') }}</span>
                } @else {
                  <span class="unverified-badge">{{ lang.t('home_nafath_unverified') }}</span>
                }
              </div>
            } @else {
              <span class="welcome-text">{{ lang.t('home_welcome_back') }}</span>
              <h1 class="user-display-name">{{ lang.t('home_guest_title') }}</h1>
              <p class="guest-desc">{{ lang.t('home_guest_desc') }}</p>
            }
          </div>

          <div class="header-actions">
            @if (authService.isAuthenticated()) {
              <button class="icon-btn" title="الإشعارات">
                <span class="bell-icon">🔔</span>
                @if (unreadNotifications()) {
                  <span class="unread-dot"></span>
                }
              </button>
              <button (click)="lang.toggleLanguage()" class="icon-btn lang-btn" [title]="lang.otherLangLabel()">
                <span class="lang-code-text">{{ lang.currentLang() === 'ar' ? 'EN' : 'ع' }}</span>
              </button>
              <button (click)="logout()" class="icon-btn logout-btn" title="تسجيل الخروج">
                <span>🚪</span>
              </button>
            } @else {
              <div class="lang-pill" (click)="lang.toggleLanguage()" [title]="lang.otherLangLabel()">
                <span>{{ lang.langLabel() }} ˅</span>
              </div>
            }
          </div>
        </div>

        <!-- Create Deal Action Button -->
        <div class="header-cta-wrapper">
          <button (click)="onOpenNewDeal()" class="btn-create-deal">
            <span class="plus-icon">+</span>
            <span>{{ lang.t('home_create_deal') }}</span>
          </button>
        </div>
      </header>

      <!-- Main Body Content -->
      <main class="home-content-body" [class.guest-layout]="!authService.isAuthenticated()">
        @if (activeNav() === 'profile' && authService.isAuthenticated()) {
          <section class="profile-section">
            <div class="section-title-row">
              <h2 class="section-title">{{ lang.t('profile_title') }}</h2>
              <button
                type="button"
                class="btn btn-outline-coral profile-edit-toggle"
                [disabled]="isSavingProfile()"
                (click)="isEditingProfile() ? cancelProfileEdit() : startProfileEdit()"
              >
                {{ isEditingProfile() ? lang.t('profile_cancel') : lang.t('profile_edit') }}
              </button>
            </div>

            <div class="profile-card">
              <div class="profile-photo-block">
                <div class="profile-avatar-large">
                  @if (profilePhotoUrl()) {
                    <img [src]="profilePhotoUrl()" [alt]="lang.t('profile_photo')" />
                  } @else {
                    <span>{{ authService.currentUser()?.name?.charAt(0) || '👤' }}</span>
                  }
                </div>
                <div class="profile-photo-actions">
                  <h3>{{ lang.t('profile_photo') }}</h3>
                  <p>{{ lang.t('profile_photo_hint') }}</p>
                  <input
                    #avatarInput
                    class="visually-hidden"
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    (change)="onAvatarSelected($event)"
                  />
                  <div class="profile-button-row">
                    <button type="button" class="btn btn-outline-coral" (click)="avatarInput.click()">
                      {{ lang.t('profile_change_photo') }}
                    </button>
                    @if (selectedAvatar()) {
                      <button
                        type="button"
                        class="btn btn-black"
                        [disabled]="isUploadingAvatar()"
                        (click)="saveAvatar()"
                      >
                        {{ isUploadingAvatar() ? lang.t('profile_uploading') : lang.t('profile_save_photo') }}
                      </button>
                    }
                  </div>
                  @if (avatarError()) {
                    <p class="profile-message profile-error" role="alert">{{ lang.t(avatarError()!) }}</p>
                  }
                  @if (avatarSaved()) {
                    <p class="profile-message profile-success" role="status">{{ lang.t('profile_upload_success') }}</p>
                  }
                </div>
              </div>

              @if (isEditingProfile()) {
                <form class="profile-edit-form" [formGroup]="profileForm" (ngSubmit)="saveProfile()" novalidate>
                  <div class="profile-form-field">
                    <label for="profile-name">{{ lang.t('profile_name') }}</label>
                    <input
                      id="profile-name"
                      type="text"
                      formControlName="name"
                      autocomplete="name"
                      maxlength="150"
                      [attr.aria-invalid]="profileForm.controls.name.invalid && profileForm.controls.name.touched"
                    />
                    @if (profileForm.controls.name.touched && profileForm.controls.name.hasError('required')) {
                      <small class="profile-field-error">{{ lang.t('profile_name_required') }}</small>
                    } @else if (profileForm.controls.name.touched && profileForm.controls.name.hasError('maxlength')) {
                      <small class="profile-field-error">{{ lang.t('profile_name_max') }}</small>
                    }
                  </div>

                  <div class="profile-form-field">
                    <label for="profile-email">{{ lang.t('profile_email') }}</label>
                    <input
                      id="profile-email"
                      type="email"
                      formControlName="email"
                      autocomplete="email"
                      inputmode="email"
                      dir="ltr"
                      maxlength="254"
                      [attr.aria-invalid]="profileForm.controls.email.invalid && profileForm.controls.email.touched"
                    />
                    @if (profileForm.controls.email.touched && profileForm.controls.email.invalid) {
                      <small class="profile-field-error">{{ lang.t('profile_email_invalid') }}</small>
                    }
                  </div>

                  <div class="profile-detail-row profile-phone-locked">
                    <span>{{ lang.t('profile_phone') }}</span>
                    <div class="profile-phone-value">
                      <strong dir="ltr">{{ authService.currentUser()?.phone }}</strong>
                      <small>{{ lang.t('profile_phone_locked_hint') }}</small>
                    </div>
                  </div>

                  @if (profileError()) {
                    <p class="profile-message profile-error" role="alert">{{ lang.t(profileError()!) }}</p>
                  }

                  <div class="profile-button-row">
                    <button type="submit" class="btn btn-black" [disabled]="isSavingProfile()">
                      {{ isSavingProfile() ? lang.t('profile_saving') : lang.t('profile_save') }}
                    </button>
                    <button type="button" class="btn btn-outline-coral" [disabled]="isSavingProfile()" (click)="cancelProfileEdit()">
                      {{ lang.t('profile_cancel') }}
                    </button>
                  </div>
                </form>
              } @else {
                <div class="profile-details">
                  @if (profileSaved()) {
                    <p class="profile-message profile-success" role="status">{{ lang.t('profile_save_success') }}</p>
                  }
                  <div class="profile-detail-row">
                    <span>{{ lang.t('profile_name') }}</span>
                    <strong>{{ authService.currentUser()?.name }}</strong>
                  </div>
                  <div class="profile-detail-row profile-phone-locked">
                    <span>{{ lang.t('profile_phone') }}</span>
                    <div class="profile-phone-value">
                      <strong dir="ltr">{{ authService.currentUser()?.phone }}</strong>
                      <small>{{ lang.t('profile_phone_locked_hint') }}</small>
                    </div>
                  </div>
                  <div class="profile-detail-row">
                    <span>{{ lang.t('profile_email') }}</span>
                    <strong dir="ltr">{{ authService.currentUser()?.email }}</strong>
                  </div>
                  <div class="profile-detail-row">
                    <span>{{ lang.t('profile_code') }}</span>
                    <strong dir="ltr">{{ authService.currentUser()?.code }}</strong>
                  </div>
                </div>
              }
            </div>
          </section>
        } @else {
        @if (!authService.isAuthenticated()) {
          <section class="guest-welcome-panel">
            <div class="guest-welcome-copy">
              <span class="guest-eyebrow">ETAMID · ESCROW</span>
              <h2>{{ lang.t('home_guest_title') }}</h2>
              <p>{{ lang.t('home_guest_desc') }}</p>
              <div class="guest-welcome-actions">
                <a routerLink="/auth/register-individual" class="guest-primary-action">{{ lang.t('welcome_create_individual') }}</a>
                <a routerLink="/auth/login" class="guest-secondary-action">{{ lang.t('welcome_login') }}</a>
              </div>
            </div>
            <div class="guest-welcome-art" aria-hidden="true">
              <div class="guest-art-orbit"></div>
              <div class="guest-art-shield">✓</div>
              <div class="guest-art-card guest-art-card-top"><span>✓</span></div>
              <div class="guest-art-card guest-art-card-bottom"><span>•••</span></div>
            </div>
          </section>
        }
        <!-- WALLET SECTION -->
        <section class="wallet-section">
          <div class="section-title-row">
            <h2 class="section-title">{{ lang.t('home_wallet_title') }}</h2>
            @if (authService.isAuthenticated()) {
              <span class="status-badge active-badge">{{ lang.t('home_wallet_active') }}</span>
            } @else {
              <span class="status-badge inactive-badge">{{ lang.t('home_wallet_inactive') }}</span>
            }
          </div>

          @if (authService.isAuthenticated()) {
            <!-- Real Active Wallet Card -->
            <div class="wallet-card active-wallet">
              <div class="wallet-balance-row">
                <div class="balance-meta">
                  <span class="balance-label">{{ lang.t('home_available_balance') }}</span>
                  <div class="balance-value">
                    {{ availableBalance().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
                    <span class="currency">{{ lang.t('home_sar') }}</span>
                  </div>
                </div>
                <button
                  class="withdraw-pill"
                  [disabled]="availableBalance() <= 0"
                  (click)="onWithdrawClick()"
                >
                  <span>{{ lang.t('home_withdraw') }}</span>
                </button>
              </div>

              <div class="wallet-escrow-row">
                <span class="escrow-label">{{ lang.t('home_escrow_held') }}</span>
                <span class="escrow-value">
                  {{ escrowHeldBalance().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} {{ lang.t('home_sar') }}
                </span>
              </div>
            </div>
          } @else {
            <!-- Guest Inactive Wallet Card -->
            <div class="wallet-card inactive-wallet-card">
              <div class="inactive-icon-row">
                <div class="inactive-shield">💳</div>
                <div class="inactive-info">
                  <h3>{{ lang.t('home_inactive_wallet_title') }}</h3>
                  <p>{{ lang.t('home_inactive_wallet_desc') }}</p>
                </div>
              </div>
              <a routerLink="/auth/login" class="btn btn-black btn-sm-wallet">
                {{ lang.t('welcome_login') }}
              </a>
            </div>

            <!-- Support Card -->
            <div class="support-card">
              <div class="support-content">
                <span class="support-icon">💬</span>
                <div>
                  <h4>{{ lang.t('home_need_help') }}</h4>
                  <p>{{ lang.t('home_need_help_desc') }}</p>
                </div>
              </div>
              <span class="support-chevron">&gt;</span>
            </div>
          }
        </section>

        <!-- DEALS SECTION -->
        @if (authService.isAuthenticated()) {
          <section class="deals-section">
            <div class="section-title-row">
              <h2 class="section-title">{{ lang.t('home_deals_title') }}</h2>
            </div>

            <!-- Real Tabs Navigation with Real Dynamic Counts -->
            <div class="deals-tabs">
              <button
                class="tab-item"
                [class.active]="selectedTab() === 'new'"
                (click)="selectedTab.set('new')"
              >
                <span>{{ lang.t('home_tab_new') }}</span>
                <span class="tab-count">{{ newDealsCount() }}</span>
              </button>
              <button
                class="tab-item"
                [class.active]="selectedTab() === 'in_progress'"
                (click)="selectedTab.set('in_progress')"
              >
                <span>{{ lang.t('home_tab_in_progress') }}</span>
                <span class="tab-count">{{ inProgressDealsCount() }}</span>
              </button>
              <button
                class="tab-item"
                [class.active]="selectedTab() === 'completed'"
                (click)="selectedTab.set('completed')"
              >
                <span>{{ lang.t('home_tab_completed') }}</span>
                <span class="tab-count">{{ completedDealsCount() }}</span>
              </button>
            </div>

            <!-- Real Deals List -->
            <div class="deals-list">
              @for (deal of currentTabDeals(); track deal.id) {
                <div class="deal-card">
                  <div class="deal-card-header">
                    <div class="client-avatar">
                      {{ deal.counterParty.charAt(0) || '👤' }}
                    </div>
                    <div class="client-info">
                      <strong class="client-name">{{ deal.counterParty }}</strong>
                      <span class="deal-code">{{ lang.t('home_deal_code') }} #{{ deal.code }}</span>
                    </div>
                    <div class="deal-price-badge">
                      <span class="deal-amount">{{ deal.amount.toLocaleString() }}</span>
                      <span class="deal-cur">{{ lang.t('home_sar') }}</span>
                    </div>
                  </div>

                  <div class="deal-card-body">
                    <p class="deal-title">{{ deal.title }}</p>
                    @if (deal.description) {
                      <p class="deal-desc">{{ deal.description }}</p>
                    }
                    <div class="deal-date-row">
                      <span class="date-tag">📅 {{ deal.date }}</span>
                      <span
                        class="status-tag"
                        [class.pending-tag]="deal.status === 'pending'"
                        [class.progress-tag]="deal.status === 'in_progress'"
                        [class.success-tag]="deal.status === 'completed'"
                        [class.declined-tag]="deal.status === 'declined'"
                      >
                        {{ deal.statusText }}
                      </span>
                    </div>
                  </div>

                  <!-- Deal Actions: ONLY show for pending requests -->
                  @if (deal.status === 'pending') {
                    <div class="deal-actions-row">
                      <button (click)="acceptDeal(deal.id)" class="btn btn-black btn-deal-accept">
                        {{ lang.t('home_deal_accept') }}
                      </button>
                      <button (click)="declineDeal(deal.id)" class="btn btn-outline-coral btn-deal-decline">
                        {{ lang.t('home_deal_decline') }}
                      </button>
                    </div>
                  } @else if (deal.status === 'in_progress') {
                    <div class="deal-actions-row">
                      <button (click)="completeDeal(deal.id)" class="btn btn-black btn-deal-accept">
                        {{ lang.t('home_deal_complete') }}
                      </button>
                    </div>
                  }
                </div>
              }

              @if (currentTabDeals().length === 0) {
                <div class="empty-deals-box">
                  <span class="empty-icon">📂</span>
                  <h4>{{ lang.t('home_empty_deals_title') }}</h4>
                  <p>{{ lang.t('home_empty_deals_desc') }}</p>
                  <button (click)="onOpenNewDeal()" class="btn btn-black btn-empty-cta">
                    {{ lang.t('home_open_first_deal') }}
                  </button>
                </div>
              }
            </div>
          </section>
        }
        }
      </main>

      <!-- REAL NEW DEAL MODAL -->
      @if (showNewDealModal()) {
        <div class="modal-backdrop" (click)="closeModal()">
          <div class="new-deal-modal" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h3>{{ lang.t('modal_deal_title') }}</h3>
              <button (click)="closeModal()" class="modal-close-btn">✕</button>
            </div>

            <form [formGroup]="dealForm" (ngSubmit)="onCreateDealSubmit()" class="modal-form">
              <!-- Counterparty Phone -->
              <div class="form-group">
                <label for="partyPhone">{{ lang.t('modal_party_phone') }}</label>
                <input
                  id="partyPhone"
                  type="tel"
                  formControlName="partyPhone"
                  placeholder="05XXXXXXXX"
                  dir="ltr"
                  class="modal-input"
                />
                @if (dealForm.get('partyPhone')?.touched && dealForm.get('partyPhone')?.invalid) {
                  <small class="error-text">{{ lang.t('modal_party_phone_error') }}</small>
                }
              </div>

              <!-- Deal Title -->
              <div class="form-group">
                <label for="title">{{ lang.t('modal_title_label') }}</label>
                <input
                  id="title"
                  type="text"
                  formControlName="title"
                  [placeholder]="lang.t('modal_title_placeholder')"
                  class="modal-input"
                />
                @if (dealForm.get('title')?.touched && dealForm.get('title')?.invalid) {
                  <small class="error-text">{{ lang.t('modal_title_error') }}</small>
                }
              </div>

              <!-- Deal Amount -->
              <div class="form-group">
                <label for="amount">{{ lang.t('modal_amount_label') }}</label>
                <input
                  id="amount"
                  type="number"
                  formControlName="amount"
                  placeholder="0.00"
                  class="modal-input"
                  dir="ltr"
                />
                @if (dealForm.get('amount')?.touched && dealForm.get('amount')?.invalid) {
                  <small class="error-text">{{ lang.t('modal_amount_error') }}</small>
                }
              </div>

              <!-- Description / Release conditions -->
              <div class="form-group">
                <label for="description">{{ lang.t('modal_desc_label') }}</label>
                <textarea
                  id="description"
                  rows="3"
                  formControlName="description"
                  [placeholder]="lang.t('modal_desc_placeholder')"
                  class="modal-input"
                ></textarea>
              </div>

              <button
                type="submit"
                class="btn btn-black modal-submit-btn"
                [disabled]="dealForm.invalid || isSubmittingDeal()"
              >
                @if (isSubmittingDeal()) {
                  <span>{{ lang.t('modal_submitting') }}</span>
                } @else {
                  <span>{{ lang.t('modal_submit') }}</span>
                }
              </button>
            </form>
          </div>
        </div>
      }

      <!-- Bottom Mobile Navigation Bar -->
      <nav class="bottom-nav-bar">
        <button class="nav-item" [class.active]="activeNav() === 'home'" (click)="activeNav.set('home')">
          <span class="nav-icon">🏠</span>
          <span class="nav-label">{{ lang.t('home_nav_home') }}</span>
        </button>
        <button class="nav-item" [class.active]="activeNav() === 'deals'" (click)="activeNav.set('deals')">
          <span class="nav-icon">📑</span>
          <span class="nav-label">{{ lang.t('home_nav_deals') }}</span>
        </button>
        <button class="nav-item" [class.active]="activeNav() === 'wallet'" (click)="activeNav.set('wallet')">
          <span class="nav-icon">💳</span>
          <span class="nav-label">{{ lang.t('home_nav_wallet') }}</span>
        </button>
        <button class="nav-item" [class.active]="activeNav() === 'profile'" (click)="activeNav.set('profile')">
          <span class="nav-icon">
            @if (authService.currentUser()?.avatarUrl) {
              <img class="nav-avatar-img" [src]="authService.currentUser()?.avatarUrl" [alt]="lang.t('profile_photo')" />
            } @else {
              👤
            }
          </span>
          <span class="nav-label">{{ lang.t('home_nav_profile') }}</span>
        </button>
      </nav>
    </div>
    } @else {
      <div class="public-landing">
        <header class="landing-nav">
          <a routerLink="/home" class="landing-brand" aria-label="Etamid home">
            <img src="/logo.png" alt="اعتمد" />
          </a>
          <nav class="landing-links" aria-label="Main navigation">
            <a href="#features">{{ lang.t('landing_nav_features') }}</a>
            <a href="#how-it-works">{{ lang.t('landing_nav_steps') }}</a>
          </nav>
          <div class="landing-nav-actions">
            <button class="landing-language" type="button" (click)="lang.toggleLanguage()">{{ lang.langLabel() }} <span>⌄</span></button>
            <a routerLink="/auth/login" class="landing-login">{{ lang.t('welcome_login') }}</a>
            <a routerLink="/auth/welcome" class="landing-nav-cta">{{ lang.t('landing_nav_register') }}</a>
          </div>
        </header>

        <main>
          <section class="landing-hero">
            <div class="landing-hero-copy">
              <span class="landing-eyebrow"><i></i>{{ lang.t('landing_eyebrow') }}</span>
              <h1>{{ lang.t('landing_headline_first') }} <em>{{ lang.t('landing_headline_second') }}</em></h1>
              <p class="landing-subtitle">{{ lang.t('landing_subtitle') }}</p>
              <div class="landing-hero-actions">
                <a routerLink="/auth/welcome" class="landing-primary-action">{{ lang.t('landing_primary_cta') }} <span aria-hidden="true">←</span></a>
                <a routerLink="/auth/register-company" class="landing-secondary-action">{{ lang.t('welcome_create_company') }}</a>
              </div>
              <div class="landing-microcopy"><span>✓</span>{{ lang.t('landing_microcopy') }}</div>
            </div>

            <div class="landing-visual" aria-hidden="true">
              <div class="landing-visual-glow"></div>
              <div class="landing-visual-ring landing-visual-ring-one"></div>
              <div class="landing-visual-ring landing-visual-ring-two"></div>
              <div class="journey-card">
                <div class="journey-card-head">
                  <div><span>{{ lang.t('landing_visual_kicker') }}</span><h2>{{ lang.t('landing_visual_title') }}</h2></div>
                  <b>✓</b>
                </div>
                <div class="journey-track"><i></i><i></i><i></i></div>
                <div class="journey-row"><span class="journey-number">01</span><div><strong>{{ lang.t('landing_step_one_title') }}</strong><small>{{ lang.t('landing_step_one_desc') }}</small></div><span class="journey-check">✓</span></div>
                <div class="journey-row journey-row-active"><span class="journey-number">02</span><div><strong>{{ lang.t('landing_step_two_title') }}</strong><small>{{ lang.t('landing_visual_active') }}</small></div><span class="journey-dot"></span></div>
                <div class="journey-row journey-row-muted"><span class="journey-number">03</span><div><strong>{{ lang.t('landing_step_three_title') }}</strong><small>{{ lang.t('landing_visual_next') }}</small></div><span class="journey-empty"></span></div>
              </div>
              <div class="landing-float-chip landing-float-chip-top"><span>✓</span>{{ lang.t('landing_visual_chip_one') }}</div>
              <div class="landing-float-chip landing-float-chip-bottom"><span>↗</span>{{ lang.t('landing_visual_chip_two') }}</div>
            </div>
          </section>

          <section class="landing-features" id="features">
            <div class="landing-section-heading">
              <span>{{ lang.t('landing_features_kicker') }}</span>
              <h2>{{ lang.t('landing_features_title') }}</h2>
              <p>{{ lang.t('landing_features_desc') }}</p>
            </div>
            <div class="landing-feature-grid">
              <article class="landing-feature-card"><span class="feature-index">01</span><div class="feature-symbol">◎</div><h3>{{ lang.t('landing_feature_one_title') }}</h3><p>{{ lang.t('landing_feature_one_desc') }}</p></article>
              <article class="landing-feature-card"><span class="feature-index">02</span><div class="feature-symbol">↗</div><h3>{{ lang.t('landing_feature_two_title') }}</h3><p>{{ lang.t('landing_feature_two_desc') }}</p></article>
              <article class="landing-feature-card"><span class="feature-index">03</span><div class="feature-symbol">≋</div><h3>{{ lang.t('landing_feature_three_title') }}</h3><p>{{ lang.t('landing_feature_three_desc') }}</p></article>
            </div>
          </section>

          <section class="landing-how" id="how-it-works">
            <div class="landing-section-heading">
              <span>{{ lang.t('landing_steps_kicker') }}</span>
              <h2>{{ lang.t('landing_steps_title') }}</h2>
              <p>{{ lang.t('landing_steps_desc') }}</p>
            </div>
            <div class="landing-steps-grid">
              <article class="landing-step-card"><span>01</span><h3>{{ lang.t('landing_step_one_title') }}</h3><p>{{ lang.t('landing_step_one_desc') }}</p></article>
              <article class="landing-step-card"><span>02</span><h3>{{ lang.t('landing_step_two_title') }}</h3><p>{{ lang.t('landing_step_two_desc') }}</p></article>
              <article class="landing-step-card"><span>03</span><h3>{{ lang.t('landing_step_three_title') }}</h3><p>{{ lang.t('landing_step_three_desc') }}</p></article>
            </div>
          </section>

          <section class="landing-bottom-cta">
            <div><span>{{ lang.t('landing_bottom_kicker') }}</span><h2>{{ lang.t('landing_bottom_title') }}</h2><p>{{ lang.t('landing_bottom_desc') }}</p></div>
            <a routerLink="/auth/welcome">{{ lang.t('landing_primary_cta') }} <span aria-hidden="true">←</span></a>
          </section>
        </main>

        <footer class="landing-footer">
          <a routerLink="/home" class="landing-footer-brand"><img src="/logo.png" alt="اعتمد" /></a>
          <span>{{ lang.t('landing_footer_note') }}</span>
          <a routerLink="/auth/login">{{ lang.t('welcome_login') }}</a>
        </footer>
      </div>
    }
  `,
  styles: [`
    .home-screen {
      width: 100%;
      max-width: none;
      min-height: 100vh;
      background-color: #f8fafc;
      padding-bottom: 80px;
      position: relative;
      box-shadow: none;
    }

    .home-header-card {
      background: #eb5757;
      border-radius: 0 0 28px 28px;
      padding: clamp(1.25rem, 3vw, 2.5rem) clamp(1rem, 5vw, 4.5rem) clamp(1.5rem, 3vw, 2.25rem);
      color: #ffffff;
      box-shadow: 0 10px 25px rgba(235, 87, 87, 0.25);
    }

    .header-top-row,
    .header-cta-wrapper {
      width: 100%;
      max-width: 1480px;
      margin-inline: auto;
    }

    .header-top-row {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 1.5rem;
    }

    .header-greeting {
      .welcome-text {
        font-size: 0.85rem;
        opacity: 0.9;
        font-weight: 500;
        display: block;
      }

      .user-display-name {
        font-size: 1.4rem;
        font-weight: 800;
        margin: 0.15rem 0 0.35rem;
      }

      .guest-desc {
        font-size: 0.8rem;
        opacity: 0.9;
        margin-top: 0.2rem;
      }

      .header-tags {
        display: flex;
        gap: 0.5rem;
        align-items: center;
        margin-top: 0.25rem;

        .merchant-code {
          background: rgba(255, 255, 255, 0.2);
          padding: 2px 8px;
          border-radius: 6px;
          font-size: 0.75rem;
          font-weight: 700;
        }

        .nafath-badge {
          background: rgba(255, 255, 255, 0.25);
          padding: 2px 8px;
          border-radius: 6px;
          font-size: 0.75rem;
          font-weight: 700;
        }

        .unverified-badge {
          background: rgba(0, 0, 0, 0.2);
          padding: 2px 8px;
          border-radius: 6px;
          font-size: 0.75rem;
          font-weight: 600;
          color: #ffe4e6;
        }
      }
    }

    .header-actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;

      .icon-btn {
        background: rgba(255, 255, 255, 0.2);
        border: none;
        width: 38px;
        height: 38px;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        position: relative;
        font-size: 1.1rem;
        color: #ffffff;
        transition: background 0.2s;

        &:hover {
          background: rgba(255, 255, 255, 0.3);
        }

        .unread-dot {
          position: absolute;
          top: 6px;
          right: 6px;
          width: 8px;
          height: 8px;
          background: #ffffff;
          border-radius: 50%;
        }
      }

      .lang-btn {
        font-size: 0.8rem;
        font-weight: 800;
      }

      .logout-btn {
        font-size: 0.95rem;
      }

      .lang-pill {
        background: rgba(255, 255, 255, 0.2);
        padding: 0.35rem 0.75rem;
        border-radius: 20px;
        font-size: 0.8rem;
        font-weight: 700;
        cursor: pointer;
        user-select: none;
        transition: background 0.2s;

        &:hover {
          background: rgba(255, 255, 255, 0.3);
        }
      }
    }

    .header-cta-wrapper {
      margin-top: 0.75rem;

      .btn-create-deal {
        width: 100%;
        height: 52px;
        background: #ffffff;
        color: #eb5757;
        border: none;
        border-radius: 14px;
        font-weight: 800;
        font-size: 1.05rem;
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
        cursor: pointer;
        box-shadow: 0 4px 15px rgba(0, 0, 0, 0.08);
        transition: transform 0.2s, box-shadow 0.2s;

        &:hover {
          transform: translateY(-2px);
          box-shadow: 0 6px 20px rgba(0, 0, 0, 0.12);
        }

        .plus-icon {
          font-size: 1.3rem;
          line-height: 1;
        }
      }
    }

    .home-content-body {
      width: 100%;
      max-width: 1480px;
      margin-inline: auto;
      padding: clamp(1rem, 3vw, 2.5rem) clamp(1rem, 4vw, 3.5rem) 2rem;
      display: flex;
      flex-direction: column;
      gap: clamp(1.25rem, 2.5vw, 2.5rem);
    }

    .section-title-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.75rem;

      .section-title {
        font-size: 1.2rem;
        font-weight: 800;
        color: #111827;
      }

      .status-badge {
        font-size: 0.75rem;
        font-weight: 700;
        padding: 3px 10px;
        border-radius: 20px;
      }

      .active-badge {
        background: #ecfdf5;
        color: #059669;
        border: 1px solid #a7f3d0;
      }

      .inactive-badge {
        background: #f3f4f6;
        color: #6b7280;
        border: 1px solid #e5e7eb;
      }
    }

    .wallet-card {
      background: #ffffff;
      border-radius: 18px;
      padding: 1.25rem;
      box-shadow: 0 2px 10px rgba(0, 0, 0, 0.04);
      border: 1px solid #f1f5f9;
    }

    .active-wallet {
      .wallet-balance-row {
        display: flex;
        justify-content: space-between;
        align-items: flex-end;
        margin-bottom: 1rem;

        .balance-meta {
          .balance-label {
            font-size: 0.8rem;
            color: #6b7280;
            display: block;
          }

          .balance-value {
            font-size: 2rem;
            font-weight: 800;
            color: #111827;
            margin-top: 0.2rem;

            .currency {
              font-size: 0.95rem;
              font-weight: 600;
              color: #4b5563;
              margin-inline-start: 0.25rem;
            }
          }
        }

        .withdraw-pill {
          background: #f9fafb;
          border: 1.5px solid #e5e7eb;
          color: #111827;
          padding: 0.5rem 1rem;
          border-radius: 10px;
          font-size: 0.85rem;
          font-weight: 700;
          cursor: pointer;
          transition: background 0.2s;

          &:hover:not(:disabled) {
            background: #f3f4f6;
          }

          &:disabled {
            opacity: 0.5;
            cursor: not-allowed;
          }
        }
      }

      .wallet-escrow-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        background: #fff5f5;
        border: 1px solid #fecaca;
        padding: 0.6rem 0.85rem;
        border-radius: 10px;
        font-size: 0.8rem;

        .escrow-label {
          color: #991b1b;
        }

        .escrow-value {
          color: #eb5757;
          font-weight: 800;
        }
      }
    }

    .inactive-wallet-card {
      display: flex;
      flex-direction: column;
      gap: 1rem;

      .inactive-icon-row {
        display: flex;
        gap: 0.85rem;
        align-items: center;

        .inactive-shield {
          font-size: 2rem;
          background: #fff5f5;
          padding: 0.5rem;
          border-radius: 12px;
        }

        .inactive-info {
          h3 {
            font-size: 1rem;
            font-weight: 700;
            color: #111827;
          }

          p {
            font-size: 0.8rem;
            color: #6b7280;
            margin-top: 0.2rem;
            line-height: 1.4;
          }
        }
      }

      .btn-sm-wallet {
        height: 44px;
        font-size: 0.9rem;
      }
    }

    .support-card {
      background: #ffffff;
      border: 1px solid #f1f5f9;
      border-radius: 16px;
      padding: 1rem 1.25rem;
      margin-top: 0.85rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      cursor: pointer;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.03);

      .support-content {
        display: flex;
        gap: 0.75rem;
        align-items: center;

        .support-icon {
          font-size: 1.5rem;
        }

        h4 {
          font-size: 0.9rem;
          font-weight: 700;
          color: #111827;
        }

        p {
          font-size: 0.75rem;
          color: #6b7280;
          margin-top: 0.15rem;
        }
      }

      .support-chevron {
        color: #9ca3af;
        font-size: 1.2rem;
      }
    }

    .deals-tabs {
      display: flex;
      gap: 0.5rem;
      border-bottom: 2px solid #e5e7eb;
      margin-bottom: 1rem;

      .tab-item {
        background: none;
        border: none;
        border-bottom: 3px solid transparent;
        margin-bottom: -2px;
        padding: 0.6rem 0.5rem;
        font-size: 0.9rem;
        font-weight: 700;
        color: #6b7280;
        cursor: pointer;
        display: flex;
        align-items: center;
        gap: 0.35rem;
        transition: all 0.2s;

        .tab-count {
          background: #f3f4f6;
          color: #4b5563;
          font-size: 0.7rem;
          padding: 1px 6px;
          border-radius: 10px;
        }

        &.active {
          color: #eb5757;
          border-bottom-color: #eb5757;

          .tab-count {
            background: #fef2f2;
            color: #eb5757;
          }
        }
      }
    }

    .deals-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .deal-card {
      background: #ffffff;
      border-radius: 16px;
      padding: 1.25rem;
      border: 1px solid #e5e7eb;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
      display: flex;
      flex-direction: column;
      gap: 0.85rem;
    }

    .deal-card-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;

      .client-avatar {
        width: 42px;
        height: 42px;
        background: #fef2f2;
        color: #eb5757;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        font-weight: 800;
        font-size: 1.1rem;
      }

      .client-info {
        flex: 1;
        display: flex;
        flex-direction: column;

        .client-name {
          font-size: 0.95rem;
          color: #111827;
        }

        .deal-code {
          font-size: 0.75rem;
          color: #9ca3af;
        }
      }

      .deal-price-badge {
        .deal-amount {
          font-size: 1.25rem;
          font-weight: 800;
          color: #111827;
        }

        .deal-cur {
          font-size: 0.75rem;
          color: #6b7280;
          margin-inline-start: 0.2rem;
        }
      }
    }

    .deal-card-body {
      .deal-title {
        font-size: 0.95rem;
        color: #111827;
        font-weight: 700;
        line-height: 1.4;
      }

      .deal-desc {
        font-size: 0.8rem;
        color: #6b7280;
        margin-top: 0.25rem;
      }

      .deal-date-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-top: 0.5rem;
        font-size: 0.75rem;

        .date-tag {
          color: #9ca3af;
        }

        .status-tag {
          padding: 2px 8px;
          border-radius: 6px;
          font-weight: 700;
        }

        .pending-tag {
          background: #fff7ed;
          color: #c2410c;
        }

        .progress-tag {
          background: #eff6ff;
          color: #1d4ed8;
        }

        .success-tag {
          background: #ecfdf5;
          color: #059669;
        }

        .declined-tag {
          background: #fef2f2;
          color: #b91c1c;
        }
      }
    }

    .deal-actions-row {
      display: flex;
      gap: 0.75rem;
      margin-top: 0.5rem;
      padding-top: 0.75rem;
      border-top: 1px solid #f1f5f9;

      .btn-deal-accept {
        flex: 1.5;
        height: 44px;
        font-size: 0.9rem;
      }

      .btn-deal-decline {
        flex: 1;
        height: 44px;
        font-size: 0.9rem;
      }
    }

    .empty-deals-box {
      text-align: center;
      padding: 2.5rem 1.5rem;
      background: #ffffff;
      border-radius: 16px;
      border: 1.5px dashed #d1d5db;

      .empty-icon {
        font-size: 2.5rem;
        display: block;
        margin-bottom: 0.5rem;
      }

      h4 {
        font-size: 1rem;
        color: #111827;
        font-weight: 700;
        margin-bottom: 0.35rem;
      }

      p {
        font-size: 0.85rem;
        color: #6b7280;
        margin-bottom: 1.25rem;
        line-height: 1.4;
      }

      .btn-empty-cta {
        padding: 0.6rem 1.25rem;
        font-size: 0.9rem;
        margin: 0 auto;
        display: inline-flex;
      }
    }

    .btn {
      border-radius: 12px;
      font-weight: 700;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      text-decoration: none;
      transition: all 0.2s;
      border: none;
    }

    .btn-black {
      background: #111827;
      color: #ffffff;

      &:hover:not(:disabled) {
        background: #000000;
      }

      &:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
    }

    .btn-outline-coral {
      background: #ffffff;
      border: 1.5px solid #eb5757;
      color: #eb5757;

      &:hover {
        background: #fef2f2;
      }
    }

    .bottom-nav-bar {
      position: fixed;
      bottom: 0;
      left: 50%;
      transform: translateX(-50%);
      width: 100%;
      max-width: 440px;
      height: 68px;
      background: #ffffff;
      border-top: 1px solid #e5e7eb;
      display: flex;
      justify-content: space-around;
      align-items: center;
      z-index: 900;
      box-shadow: 0 -4px 15px rgba(0, 0, 0, 0.05);

      .nav-item {
        background: none;
        border: none;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.2rem;
        color: #9ca3af;
        cursor: pointer;
        font-size: 0.75rem;
        font-weight: 600;
        transition: color 0.2s;

        .nav-icon {
          font-size: 1.25rem;
          line-height: 1;
        }

        &.active {
          color: #eb5757;

          .nav-label {
            font-weight: 800;
          }
        }
      }
    }

    .modal-backdrop {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.5);
      display: flex;
      align-items: flex-end;
      justify-content: center;
      z-index: 1000;
    }

    .new-deal-modal {
      background: #ffffff;
      width: 100%;
      max-width: 440px;
      border-radius: 24px 24px 0 0;
      padding: 1.75rem 1.5rem 2rem;

      .modal-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 1.25rem;

        h3 {
          font-size: 1.15rem;
          font-weight: 800;
          color: #111827;
        }

        .modal-close-btn {
          background: none;
          border: none;
          font-size: 1.2rem;
          cursor: pointer;
          color: #6b7280;
        }
      }

      .modal-form {
        display: flex;
        flex-direction: column;
        gap: 1rem;

        .form-group {
          display: flex;
          flex-direction: column;
          gap: 0.35rem;

          label {
            font-size: 0.85rem;
            font-weight: 700;
            color: #374151;
          }

          .modal-input {
            border: 1.5px solid #e5e7eb;
            border-radius: 12px;
            padding: 0.75rem 1rem;
            font-size: 0.95rem;
            outline: none;

            &:focus {
              border-color: #eb5757;
            }
          }

          .error-text {
            color: #eb5757;
            font-size: 0.75rem;
            font-weight: 600;
          }
        }

        .modal-submit-btn {
          height: 48px;
          margin-top: 0.5rem;
        }
      }
    }
  `]
})
export class HomeComponent {
  readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  readonly lang = inject(LanguageService);

  readonly selectedTab = signal<'new' | 'in_progress' | 'completed'>('new');
  readonly activeNav = signal<'home' | 'deals' | 'wallet' | 'profile'>('home');
  readonly showNewDealModal = signal(false);
  readonly isSubmittingDeal = signal(false);
  readonly unreadNotifications = signal(false);

  readonly deals = signal<DealItem[]>([]);
  readonly availableBalance = signal<number>(0.0);

  readonly selectedAvatar = signal<File | null>(null);
  readonly avatarPreview = signal<string | null>(null);
  readonly isUploadingAvatar = signal(false);
  readonly avatarError = signal<string | null>(null);
  readonly avatarSaved = signal(false);
  readonly isEditingProfile = signal(false);
  readonly isSavingProfile = signal(false);
  readonly profileError = signal<string | null>(null);
  readonly profileSaved = signal(false);
  readonly profileForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
  });

  constructor() {
    if (this.authService.isAuthenticated()) {
      this.authService.refreshProfile().subscribe({ error: () => {} });
    }
  }

  startProfileEdit(): void {
    const user = this.authService.currentUser();
    if (!user) return;

    this.profileForm.reset({ name: user.name, email: user.email });
    this.profileError.set(null);
    this.profileSaved.set(false);
    this.isEditingProfile.set(true);
  }

  cancelProfileEdit(): void {
    if (this.isSavingProfile()) return;
    this.isEditingProfile.set(false);
    this.profileError.set(null);
    this.profileForm.reset();
  }

  saveProfile(): void {
    if (this.isSavingProfile()) return;
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    const values = this.profileForm.getRawValue();
    this.isSavingProfile.set(true);
    this.profileError.set(null);
    this.profileSaved.set(false);
    this.authService.updateProfile({ username: values.name.trim(), email: values.email.trim() }).subscribe({
      next: (response) => {
        this.isSavingProfile.set(false);
        if (!response.success) {
          this.profileError.set(this.profileSaveErrorKey(response));
          return;
        }
        this.isEditingProfile.set(false);
        this.profileSaved.set(true);
      },
      error: (error: unknown) => {
        this.isSavingProfile.set(false);
        this.profileError.set(this.profileSaveErrorKey(error));
      },
    });
  }

  private profileSaveErrorKey(error: unknown): string {
    if (typeof error === 'object' && error !== null) {
      const envelope = error as {
        errors?: { code?: string }[];
        error?: { errors?: { code?: string }[] };
      };
      const errors = envelope.error?.errors ?? envelope.errors ?? [];
      if (errors.some((item) => item.code === 'EMAIL_EXISTS')) return 'profile_email_exists';
    }
    return 'profile_save_error';
  }

  profilePhotoUrl(): string | null {
    return this.avatarPreview() || this.authService.currentUser()?.avatarUrl || null;
  }

  onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.avatarError.set(null);
    this.avatarSaved.set(false);
    if (!file.type.startsWith('image/') || file.size > 10 * 1024 * 1024) {
      this.selectedAvatar.set(null);
      this.avatarPreview.set(null);
      this.avatarError.set('profile_invalid_photo');
      return;
    }

    this.selectedAvatar.set(file);
    const reader = new FileReader();
    reader.onload = () => this.avatarPreview.set(typeof reader.result === 'string' ? reader.result : null);
    reader.onerror = () => {
      this.avatarPreview.set(null);
      this.avatarError.set('profile_invalid_photo');
    };
    reader.readAsDataURL(file);
  }

  saveAvatar(): void {
    const file = this.selectedAvatar();
    if (!file || this.isUploadingAvatar()) return;

    this.isUploadingAvatar.set(true);
    this.avatarError.set(null);
    this.avatarSaved.set(false);
    this.authService.uploadAvatar(file).subscribe({
      next: (response) => {
        this.isUploadingAvatar.set(false);
        if (!response.success || !response.data?.avatarUrl) {
          this.avatarError.set('profile_upload_error');
          return;
        }
        this.selectedAvatar.set(null);
        this.avatarPreview.set(null);
        this.avatarSaved.set(true);
      },
      error: () => {
        this.isUploadingAvatar.set(false);
        this.avatarError.set('profile_upload_error');
      },
    });
  }

  readonly escrowHeldBalance = computed(() =>
    this.deals()
      .filter((d) => d.status === 'in_progress')
      .reduce((sum, d) => sum + d.amount, 0)
  );

  readonly currentTabDeals = computed(() => {
    const tab = this.selectedTab();
    if (tab === 'new') {
      return this.deals().filter((d) => d.status === 'pending');
    } else if (tab === 'in_progress') {
      return this.deals().filter((d) => d.status === 'in_progress');
    } else {
      return this.deals().filter((d) => d.status === 'completed');
    }
  });

  readonly newDealsCount = computed(() => this.deals().filter((d) => d.status === 'pending').length);
  readonly inProgressDealsCount = computed(() => this.deals().filter((d) => d.status === 'in_progress').length);
  readonly completedDealsCount = computed(() => this.deals().filter((d) => d.status === 'completed').length);

  readonly dealForm = this.fb.group({
    partyPhone: ['', [Validators.required, Validators.pattern(/^05[0-9]{8}$/)]],
    title: ['', [Validators.required, Validators.minLength(3)]],
    amount: [null as number | null, [Validators.required, Validators.min(10)]],
    description: [''],
  });

  onOpenNewDeal(): void {
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/auth/login']);
    } else {
      this.dealForm.reset();
      this.showNewDealModal.set(true);
    }
  }

  closeModal(): void {
    this.showNewDealModal.set(false);
  }

  onCreateDealSubmit(): void {
    if (this.dealForm.invalid) return;

    this.isSubmittingDeal.set(true);
    const formVal = this.dealForm.getRawValue();

    const newDeal: DealItem = {
      id: Date.now().toString(),
      code: Math.floor(10000 + Math.random() * 90000).toString(),
      counterParty: formVal.partyPhone || 'طرف آخر',
      role: 'بائع',
      title: formVal.title || '',
      description: formVal.description || '',
      amount: Number(formVal.amount),
      date: new Date().toLocaleTimeString(this.lang.isAr() ? 'ar-SA' : 'en-US', { hour: '2-digit', minute: '2-digit' }),
      status: 'pending',
      statusText: this.lang.isAr() ? 'في انتظار موافقة الطرف الآخر' : 'Awaiting counterparty acceptance',
    };

    this.deals.update((current) => [newDeal, ...current]);

    this.isSubmittingDeal.set(false);
    this.showNewDealModal.set(false);
    this.selectedTab.set('new');
  }

  acceptDeal(id: string): void {
    this.deals.update((list) =>
      list.map((d) =>
        d.id === id
          ? {
              ...d,
              status: 'in_progress' as const,
              statusText: this.lang.isAr() ? 'قيد التنفيذ - الأموال محجوزة في الضمان' : 'In Progress - Funds Held in Escrow',
            }
          : d
      )
    );
  }

  declineDeal(id: string): void {
    this.deals.update((list) =>
      list.map((d) =>
        d.id === id
          ? {
              ...d,
              status: 'declined' as const,
              statusText: this.lang.isAr() ? 'تم رفض المعاملة' : 'Deal Declined',
            }
          : d
      )
    );
  }

  completeDeal(id: string): void {
    const deal = this.deals().find((d) => d.id === id);
    if (!deal) return;

    this.availableBalance.update((b) => b + deal.amount);

    this.deals.update((list) =>
      list.map((d) =>
        d.id === id
          ? {
              ...d,
              status: 'completed' as const,
              statusText: this.lang.isAr() ? 'مكتملة ✓ - تم تحرير الأموال' : 'Completed ✓ - Funds Released',
            }
          : d
      )
    );
    this.selectedTab.set('completed');
  }

  onWithdrawClick(): void {
    if (this.availableBalance() <= 0) return;
    const msg = this.lang.isAr()
      ? `طلب سحب لمبلغ ${this.availableBalance()} ر.س إلى حسابك البنكي المعتمد.`
      : `Withdrawal request for ${this.availableBalance()} SAR to your verified bank account.`;
    alert(msg);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/auth/welcome']);
  }
}
