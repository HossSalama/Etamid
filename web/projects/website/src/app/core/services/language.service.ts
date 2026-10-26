import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export type LanguageCode = 'ar' | 'en';

@Injectable({
  providedIn: 'root',
})
export class LanguageService {
  private readonly http = inject(HttpClient);
  private readonly storageKey = 'etamid_lang';

  readonly currentLang = signal<LanguageCode>(this.getInitialLang());
  readonly isAr = computed(() => this.currentLang() === 'ar');
  readonly isRtl = computed(() => this.currentLang() === 'ar');
  readonly langLabel = computed(() => (this.currentLang() === 'ar' ? 'العربية' : 'English'));
  readonly otherLangLabel = computed(() => (this.currentLang() === 'ar' ? 'English' : 'العربية'));

  private readonly translations: Record<LanguageCode, Record<string, string>> = {
    ar: {
      lang_name: 'العربية',
      lang_other: 'English',
      skip: 'تخطي',
      back: 'رجوع',

      // Welcome
      welcome_tagline: 'منصة الوساطة المالية وحماية التعاملات الرقمية',
      welcome_create_individual: 'انشاء حساب الأفراد',
      welcome_create_company: 'انشاء حساب الشركات',
      welcome_already_have_account: 'لديك حساب بالفعل؟',
      welcome_login: 'تسجيل الدخول',

      // Login
      login_title: 'تسجيل الدخول',
      login_phone_label: 'رقم الجوال',
      login_phone_placeholder: '05XXXXXXXX',
      login_phone_error: 'يرجى إدخال رقم جوال سعودي يبدأ بـ 05 (10 أرقام)',
      login_password_label: 'كلمة المرور',
      login_password_placeholder: '••••••••',
      login_password_error: 'كلمة المرور مطلوبة',
      login_forgot_password: 'هل نسيت كلمة المرور؟',
      login_submit: 'تسجيل الدخول',
      login_logging_in: 'جاري تسجيل الدخول...',
      login_no_account: 'ليس لديك حساب؟',
      login_register_now: 'انشاء حساب',

      // Register Individual
      register_ind_title: 'انشاء حساب الأفراد',
      register_username_label: 'اسم المستخدم',
      register_username_placeholder: 'الاسم الكامل',
      register_username_error: 'يرجى إدخال اسم المستخدم (3 أحرف على الأقل)',
      register_phone_label: 'رقم الجوال',
      register_phone_placeholder: '05XXXXXXXX',
      register_phone_error: 'يرجى إدخال رقم جوال سعودي يبدأ بـ 05 (10 أرقام)',
      register_email_label: 'الايميل',
      register_email_placeholder: 'name@domain.com',
      register_email_error: 'يرجى إدخال بريد إلكتروني صحيح',
      register_password_label: 'كلمة المرور',
      register_password_placeholder: 'Pass#123 (6 خانات + رمز مثل #)',
      register_password_error: 'كلمة المرور 6 خانات وتشمل رمزاً خاصاً (# أو @ أو !)',
      register_terms_agree: 'أوافق على',
      register_terms_link: 'شروط السياسة والاستخدام',
      register_terms_error: 'يجب الموافقة على الشروط للمتابعة',
      register_submit: 'انشاء الحساب',
      register_submitting: 'جاري إنشاء الحساب...',
      register_has_account: 'لديك حساب بالفعل؟',

      // Register Company
      register_comp_title: 'انشاء حساب الشركات',
      register_comp_badge: 'للمؤسسات والشركات التجارية 🏢',
      register_comp_name_label: 'اسم المنشأة / الشركة',
      register_comp_name_placeholder: 'الاسم كما في السجل التجاري',
      register_comp_name_error: 'يرجى إدخال اسم الشركة',
      register_comp_cr_label: 'رقم السجل التجاري (CR)',
      register_comp_cr_placeholder: '10 أرقام',
      register_comp_cr_error: 'السجل التجاري مطلوب (10 أرقام)',
      register_comp_tax_label: 'الرقم الضريبي (اختياري)',
      register_comp_tax_placeholder: '15 رقماً',
      register_comp_owner_phone: 'جوال المفوض / المالك',
      register_comp_owner_phone_error: 'يرجى إدخال رقم هاتف صحيح يبدأ بـ 05',
      register_comp_email: 'البريد الإلكتروني للشركة',
      register_comp_email_error: 'يرجى إدخال بريد إلكتروني صحيح',
      register_comp_terms: 'أوافق على شروط المنشآت والوساطة وسياسة الخصوصية',
      register_comp_submit: 'انشاء حساب الشركة',
      register_comp_submitting: 'جاري تسجيل الشركة...',

      // Verify OTP
      verify_title: 'تفعيل الحساب',
      verify_desc: 'تم إرسال رمز التحقق (OTP) المكون من 6 أرقام إلى بريدك الإلكتروني',
      verify_email_badge: 'تم إرسال الرمز إلى',
      verify_didnt_receive: 'لم يصلك الكود؟',
      verify_resend_in: 'إعادة الإرسال بعد',
      verify_resend_now: 'إعادة إرسال الكود الآن 🔄',
      verify_submit: 'تفعيل الحساب',
      verify_submitting: 'جاري التفعيل...',

      // Forgot Password
      forgot_step_phone: 'رقم الجوال',
      forgot_step_otp: 'رمز التحقق',
      forgot_step_pass: 'كلمة المرور',
      forgot_title: 'نسيت كلمة المرور',
      forgot_subtitle: 'أدخل رقم الجوال لاستلام رمز التحقق وتعيين كلمة مرور جديدة',
      forgot_next: 'التالي',
      forgot_code_title: 'رمز التحقق',
      forgot_code_subtitle: 'تم إرسال رمز التحقق إلى الرقم',
      forgot_new_pass_title: 'تعيين كلمة المرور الجديدة',
      forgot_new_pass_subtitle: 'يرجى كتابة كلمة مرور قوية وتأكيدها للمتابعة',
      forgot_new_pass_label: 'كلمة المرور الجديدة',
      forgot_confirm_pass_label: 'تأكيد كلمة المرور الجديدة',
      forgot_pass_mismatch: 'كلمة المرور غير متطابقة',
      forgot_submit_reset: 'استرداد الحساب',
      forgot_success_title: 'تم استرداد حسابك بنجاح!',
      forgot_success_subtitle: 'تم تغيير كلمة المرور بنجاح، يمكنك الآن تسجيل الدخول إلى حسابك ومتابعة معاملاتك بأمان.',
      forgot_login_btn: 'تسجيل الدخول',
      forgot_remember: 'تذكرت كلمة المرور؟',

      // Home & Dashboard
      home_welcome_back: 'مرحباً مجدداً 👋',
      home_guest_title: 'اعْتَمِدْ للوساطة المالية',
      home_guest_desc: 'منصة حماية التعاملات وحجز أموال الصفقات الرقمية',
      home_code: 'كود',
      home_nafath_verified: '✓ موثق نفاذ',
      home_nafath_unverified: 'غير موثق عبر نفاذ',
      home_create_deal: 'فتح معاملة جديدة',
      home_wallet_title: 'محفظتك',
      home_wallet_active: 'مفعلة ✓',
      home_wallet_inactive: 'غير مفعلة',
      home_available_balance: 'الرصيد المتاح للسحب',
      home_withdraw: 'السحب ↗',
      home_escrow_held: 'المبالغ المحجوزة في الضمان (Escrow):',
      home_inactive_wallet_title: 'محفظتك غير مفعلة',
      home_inactive_wallet_desc: 'يجب عليك تسجيل الدخول للاستفادة من خدمات اعتمد وحماية أموالك',
      home_need_help: 'تحتاج مساعدة؟',
      home_need_help_desc: 'تواصل مع فريق الدعم الفني أو راجع شروط الوساطة',
      home_deals_title: 'المعاملات',
      home_tab_new: 'طلبات جديدة',
      home_tab_in_progress: 'قيد التنفيذ',
      home_tab_completed: 'المكتملة',
      home_deal_code: 'كود الصفقة:',
      home_deal_accept: 'قبول المعاملة',
      home_deal_decline: 'رفض المعاملة',
      home_deal_complete: 'تأكيد استلام وتحرير المبلغ ✓',
      home_empty_deals_title: 'لا توجد معاملات في هذا التبويب',
      home_empty_deals_desc: 'عندما تقوم بفتح معاملة جديدة أو يرسل لك طرف آخر طلباً ستظهر هنا مباشرة.',
      home_open_first_deal: '+ فتح أول معاملة الآن',
      home_nav_home: 'الرئيسية',
      home_nav_deals: 'المعاملات',
      home_nav_wallet: 'المحفظة',
      home_nav_profile: 'حسابي',
      home_sar: 'ر.س',

      // New Deal Modal
      modal_deal_title: 'فتح معاملة وساطة جديدة',
      modal_party_phone: 'رقم جوال الطرف الآخر',
      modal_party_phone_error: 'رقم الجوال مطلوب ويجب أن يبدأ بـ 05 (10 أرقام)',
      modal_title_label: 'عنوان الصفقة أو السلعة',
      modal_title_placeholder: 'مثال: شراء سيارة / تصميم موقع / خدمات برمجية',
      modal_title_error: 'عنوان الصفقة مطلوب',
      modal_amount_label: 'مبلغ المعاملة (ريال سعودي)',
      modal_amount_error: 'يرجى إدخال مبلغ صحيح أكبر من 0',
      modal_desc_label: 'شروط التسليم وفحص السلعة',
      modal_desc_placeholder: 'حدد شروط تسليم المعاملة بوضوح...',
      modal_submit: 'إنشاء المعاملة وحجز الضمان',
      modal_submitting: 'جاري إنشاء المعاملة...',
    },
    en: {
      lang_name: 'English',
      lang_other: 'العربية',
      skip: 'Skip',
      back: 'Back',

      // Welcome
      welcome_tagline: 'Digital Escrow & Safe Transaction Platform',
      welcome_create_individual: 'Individual Account',
      welcome_create_company: 'Corporate Account',
      welcome_already_have_account: 'Already have an account?',
      welcome_login: 'Log in',

      // Login
      login_title: 'Sign In',
      login_phone_label: 'Mobile Number',
      login_phone_placeholder: '05XXXXXXXX',
      login_phone_error: 'Please enter a valid Saudi mobile number starting with 05 (10 digits)',
      login_password_label: 'Password',
      login_password_placeholder: '••••••••',
      login_password_error: 'Password is required',
      login_forgot_password: 'Forgot Password?',
      login_submit: 'Sign In',
      login_logging_in: 'Signing in...',
      login_no_account: 'Don\'t have an account?',
      login_register_now: 'Create Account',

      // Register Individual
      register_ind_title: 'Individual Sign Up',
      register_username_label: 'Username / Full Name',
      register_username_placeholder: 'Your full name',
      register_username_error: 'Please enter a valid username (min 3 characters)',
      register_phone_label: 'Mobile Number',
      register_phone_placeholder: '05XXXXXXXX',
      register_phone_error: 'Please enter a Saudi mobile number (05XXXXXXXX)',
      register_email_label: 'Email Address',
      register_email_placeholder: 'name@domain.com',
      register_email_error: 'Please enter a valid email address',
      register_password_label: 'Password',
      register_password_placeholder: 'Pass#123 (6+ chars & symbol)',
      register_password_error: 'Password must be 6+ chars and contain a special symbol (#, @, !)',
      register_terms_agree: 'I agree to',
      register_terms_link: 'Terms of Service & Privacy Policy',
      register_terms_error: 'You must agree to the terms to proceed',
      register_submit: 'Create Account',
      register_submitting: 'Creating account...',
      register_has_account: 'Already have an account?',

      // Register Company
      register_comp_title: 'Corporate Sign Up',
      register_comp_badge: 'For businesses and commercial enterprises 🏢',
      register_comp_name_label: 'Company / Establishment Name',
      register_comp_name_placeholder: 'Name as registered in CR',
      register_comp_name_error: 'Company name is required',
      register_comp_cr_label: 'Commercial Registration (CR)',
      register_comp_cr_placeholder: '10 digits',
      register_comp_cr_error: 'CR is required (10 digits)',
      register_comp_tax_label: 'Tax Identification Number (Optional)',
      register_comp_tax_placeholder: '15 digits',
      register_comp_owner_phone: 'Authorized Representative Mobile',
      register_comp_owner_phone_error: 'Please enter a valid phone number starting with 05',
      register_comp_email: 'Company Email',
      register_comp_email_error: 'Please enter a valid email address',
      register_comp_terms: 'I agree to Corporate Terms, Escrow Rules, and Privacy Policy',
      register_comp_submit: 'Create Company Account',
      register_comp_submitting: 'Registering company...',

      // Verify OTP
      verify_title: 'Verify Account',
      verify_desc: 'A 6-digit verification code has been sent to your email address',
      verify_email_badge: 'Code sent to',
      verify_didnt_receive: 'Didn\'t receive code?',
      verify_resend_in: 'Resend after',
      verify_resend_now: 'Resend code now 🔄',
      verify_submit: 'Activate Account',
      verify_submitting: 'Activating...',

      // Forgot Password
      forgot_step_phone: 'Mobile',
      forgot_step_otp: 'OTP',
      forgot_step_pass: 'Password',
      forgot_title: 'Forgot Password',
      forgot_subtitle: 'Enter your mobile number to receive verification code and set a new password',
      forgot_next: 'Next',
      forgot_code_title: 'Verification Code',
      forgot_code_subtitle: 'Verification code was sent to',
      forgot_new_pass_title: 'Set New Password',
      forgot_new_pass_subtitle: 'Please enter and confirm your new secure password',
      forgot_new_pass_label: 'New Password',
      forgot_confirm_pass_label: 'Confirm New Password',
      forgot_pass_mismatch: 'Passwords do not match',
      forgot_submit_reset: 'Reset Password',
      forgot_success_title: 'Account Recovered Successfully!',
      forgot_success_subtitle: 'Your password has been reset. You can now sign in and conduct your escrow transactions safely.',
      forgot_login_btn: 'Sign In Now',
      forgot_remember: 'Remembered your password?',

      // Home & Dashboard
      home_welcome_back: 'Welcome back 👋',
      home_guest_title: 'Etamid Escrow Platform',
      home_guest_desc: 'Safeguarding transactions and escrow funding for digital deals',
      home_code: 'Code',
      home_nafath_verified: '✓ Nafath Verified',
      home_nafath_unverified: 'Unverified via Nafath',
      home_create_deal: 'Open New Deal',
      home_wallet_title: 'Your Wallet',
      home_wallet_active: 'Active ✓',
      home_wallet_inactive: 'Inactive',
      home_available_balance: 'Available for Withdrawal',
      home_withdraw: 'Withdraw ↗',
      home_escrow_held: 'Funds Held in Escrow:',
      home_inactive_wallet_title: 'Your wallet is inactive',
      home_inactive_wallet_desc: 'Sign in to access escrow protection and activate your digital wallet',
      home_need_help: 'Need help?',
      home_need_help_desc: 'Contact our customer support team or read escrow FAQ',
      home_deals_title: 'Transactions',
      home_tab_new: 'New Requests',
      home_tab_in_progress: 'In Progress',
      home_tab_completed: 'Completed',
      home_deal_code: 'Deal Code:',
      home_deal_accept: 'Accept Deal',
      home_deal_decline: 'Decline Deal',
      home_deal_complete: 'Confirm Delivery & Release Funds ✓',
      home_empty_deals_title: 'No transactions in this tab',
      home_empty_deals_desc: 'When you open a deal or receive a proposal, it will appear here immediately.',
      home_open_first_deal: '+ Open first deal now',
      home_nav_home: 'Home',
      home_nav_deals: 'Deals',
      home_nav_wallet: 'Wallet',
      home_nav_profile: 'Profile',
      home_sar: 'SAR',

      // New Deal Modal
      modal_deal_title: 'Open New Escrow Deal',
      modal_party_phone: 'Counterparty Mobile Number',
      modal_party_phone_error: 'Mobile number is required and must start with 05 (10 digits)',
      modal_title_label: 'Deal Title or Service',
      modal_title_placeholder: 'e.g. Car purchase / Web development / Consulting',
      modal_title_error: 'Deal title is required',
      modal_amount_label: 'Deal Amount (SAR)',
      modal_amount_error: 'Please enter a valid amount greater than 0',
      modal_desc_label: 'Delivery & Inspection Terms',
      modal_desc_placeholder: 'Clearly state delivery and acceptance conditions...',
      modal_submit: 'Create Deal & Reserve Escrow',
      modal_submitting: 'Creating deal...',
    },
  };

  constructor() {
    this.applyDomDirection(this.currentLang());
  }

  t(key: string): string {
    const lang = this.currentLang();
    return this.translations[lang]?.[key] ?? this.translations['ar']?.[key] ?? key;
  }

  toggleLanguage(): void {
    const nextLang = this.currentLang() === 'ar' ? 'en' : 'ar';
    this.setLanguage(nextLang);
  }

  setLanguage(lang: LanguageCode): void {
    this.currentLang.set(lang);
    localStorage.setItem(this.storageKey, lang);
    this.applyDomDirection(lang);

    // Sync with backend if authenticated
    const token = localStorage.getItem('etamid_access_token');
    if (token) {
      this.http.put('/api/v1/me/language', { language: lang }).subscribe({
        error: () => {},
      });
    }
  }

  private applyDomDirection(lang: LanguageCode): void {
    if (typeof document !== 'undefined') {
      const html = document.documentElement;
      html.setAttribute('lang', lang);
      html.setAttribute('dir', lang === 'ar' ? 'rtl' : 'ltr');
      if (document.body) {
        document.body.dir = lang === 'ar' ? 'rtl' : 'ltr';
      }
    }
  }

  private getInitialLang(): LanguageCode {
    if (typeof localStorage !== 'undefined') {
      const stored = localStorage.getItem(this.storageKey);
      if (stored === 'ar' || stored === 'en') return stored;
    }
    return 'ar';
  }
}
