# اعتمد (Etamid): المواصفات الكاملة للمشروع

**التقنيات:** Backend بـ .NET · Angular (الموقع + لوحة التحكم) · تطبيق موبايل (فريق منفصل) يستهلك الـ REST API
**تاريخ الوثيقة:** الثلاثاء 20 أكتوبر 2026
**المصدر:** مراجعة ~50 شاشة من Zeplin + عقد OpenAPI (`etamid-openapi.yaml`، 78 عملية)

الرموز: 🔓 عام (بدون توكن) · 🔒 يحتاج `Authorization: Bearer <accessToken>`
كل المسارات تبدأ بـ `/api/v1`. كل رد بصيغة `{ success, message, data, errors[], meta }`.

---

## الفهرس
1. نظرة عامة على المنتج والأدوار
2. هيكل الريبو
3. الاتفاقيات المشتركة مع فريق الموبايل
4. الميزات والـ Endpoints (28 ميزة)
5. آلات الحالات
6. نموذج البيانات
7. المهام الخلفية
8. لوحة التحكم (Angular)
9. التكاملات الخارجية
10. المتطلبات غير الوظيفية
11. الجدول الزمني ومواعيد تسليم الـ API
12. قائمة التسليم لفريق الموبايل
13. الأسئلة المفتوحة ونواقص التصميم
14. المخاطر
15. ملحق: كل الـ Endpoints في جدول واحد

---

## 1) نظرة عامة على المنتج والأدوار

«اعتمد» منصة **وساطة مالية (Escrow)** بين مشترٍ وبائع. تحتفظ المنصة بأموال المشتري ولا تحوّلها للبائع إلا بعد أن يؤكد المشتري الاستلام. وإذا حدثت مشكلة يفتح المشتري **نزاعاً** يحسمه مدراء المنصة.

نوعان من الحسابات:
- **فرد:** معاملات شراء وبيع (مثل iPhone) مع شحن اختياري وتقييم ونزاعات.
- **شركة:** معاملات كبيرة بـ **جدول دفعات**. كل دفعة تحتاج موافقة الطرف الآخر، ويُطبَّق **شرط جزائي** تلقائياً عند تأخر الشركة.

العملة: ريال سعودي. اللغات: عربي (RTL) وإنجليزي. التحقق من الهوية: **نفاذ**. الدفع: Apple Pay (الظاهر في التصميم) وغالباً mada والبطاقات. الشحن: **أرامكس**.

| الدور | الوصف |
|---|---|
| زائر | يضغط «تخطي» ويرى الرئيسية بمحفظة غير مفعّلة |
| فرد (مشتري) | ينشئ معاملات، يوافق على التعديلات، يدفع، يؤكد الاستلام، يفتح نزاعاً، يقيّم |
| فرد (بائع) | يستقبل الطلبات؛ يقبل أو يرفض أو يقترح تعديلاً؛ يشحن؛ يستلم المبلغ |
| شركة | تنشئ معاملات متعددة الدفعات وتتابع الدفعات وتعتمدها |
| مدير النظام | لوحة Angular: المستخدمون، المعاملات، النزاعات، المالية، المحتوى، الإعدادات |

يمكن للحساب الفردي أن يكون مشترياً وبائعاً معاً (شاشتا home-seller وhome 2).

---

## 2) هيكل الريبو

التوصية: **Monorepo واحدة للباك والموقع ولوحة التحكم**، و**ريبو منفصلة للموبايل**.

```
etamid/
├── backend/                       # .NET
│   ├── src/
│   │   ├── Etamid.Domain/
│   │   ├── Etamid.Application/
│   │   ├── Etamid.Infrastructure/
│   │   └── Etamid.Api/            # /api/v1 (موبايل + موقع) و /admin/api (لوحة التحكم)
│   └── tests/
├── web/                           # Angular workspace
│   ├── projects/
│   │   ├── website/               # الموقع العام (SSR لتحسين SEO)
│   │   └── admin/                 # لوحة التحكم
│   └── libs/
│       ├── api-client/            # يُولَّد من openapi.yaml
│       ├── shared-ui/             # RTL والألوان والخطوط والمكونات
│       └── shared-utils/
├── contracts/
│   └── openapi.yaml               # المصدر الوحيد للحقيقة للـ API
├── docs/
├── docker-compose.yml             # SQL + Redis + API محلياً
└── .github/workflows/             # backend.yml · web-website.yml · web-admin.yml
```

القواعد:
- عدّل `contracts/openapi.yaml` أولاً، ثم ولّد عميل TypeScript (بـ NSwag أو openapi-generator) ليظهر أي كسر في العقد وقت البناء.
- أبقِ API الإدارة (`/admin/api`) خارج عقد الموبايل، فله مصادقة وصلاحيات مختلفة.
- استخدم CI بفلتر المسارات: تغيير في `web/projects/admin` يبني اللوحة فقط.
- ريبو الموبايل (`etamid-mobile`) تعتمد على العقد والـ Mock فقط.
- افصل العقد في ريبو صغيرة ثالثة فقط إذا انفصل فريق الباك عن فريق الويب.

---

## 3) الاتفاقيات المشتركة مع فريق الموبايل

- **صيغة الرد:** `{ success, message, data, errors:[{code, field?, message}], meta:{page,pageSize,total,totalPages} }`.
- **اللغة:** `Accept-Language: ar|en`. حقل `message` مترجم، و`errors[].code` ثابت وتبني عليه منطق التطبيق.
- **التواريخ:** ISO 8601 بتوقيت UTC. **المبالغ:** رقم بالريال بخانتين عشريتين. **الجوال:** `05XXXXXXXX`.
- **الترقيم:** `page` (الافتراضي 1) و`pageSize` (الافتراضي 20، الأقصى 100).
- **التوكنات:** Access Token قصير العمر (حوالي ساعة) وRefresh Token. عند `401 TOKEN_EXPIRED` يجدد التطبيق التوكن مرة ويعيد الطلب.
- **منع التكرار:** هيدر `Idempotency-Key` (UUID) إجباري على الدفع والسحب.
- **واجهة يقودها الخادم:** ردود المعاملة تتضمن `availableActions[]` (الأزرار التي تظهر للمستخدم الحالي) و`banner` (التنبيه الملوّن) و`badge` (نص ولون الوقت المتبقي). الموبايل لا يحسب الصلاحيات أو الأوقات بنفسه.
- **الإصدارات:** `/v1`. لا تكسر الـ Endpoints الموجودة أبداً، وأضف `/v2` للتغييرات الجذرية. احتفظ بملف Changelog.
- **الأخطاء القياسية:** `401 UNAUTHORIZED/TOKEN_EXPIRED` · `403` · `404 NOT_FOUND` · `409` (تعارض مثل `INVALID_DEAL_STATE` و`PHONE_EXISTS`) · `422` (تحقق) · `429 TOO_MANY_REQUESTS` مع `Retry-After`.

---

## 4) الميزات والـ Endpoints

### الميزة 1: إعدادات التطبيق والقوائم والشروط
**ماذا تفعل:** عند الفتح (Splash) يسأل التطبيق الـ API عن اللغات المدعومة، والحد الأدنى لإصدار التطبيق (التحديث الإجباري)، ورقم واتساب الدعم، والعملة. القوائم الثابتة (أسباب الرفض، أسباب النزاع، شركات الشحن، البنوك...) تأتي من الـ API ليعدّلها الأدمن بدون إصدار تطبيق جديد. والشروط والأحكام تأتي كنص بإصدار، ليُسجَّل أي إصدار وافق عليه المستخدم بالضبط.

| الحماية | Method | Path | الغرض | التفاصيل |
|---|---|---|---|---|
| 🔓 | GET | `/config` | إعدادات التطبيق | يرجع `languages`, `minVersion{android,ios}`, `forceUpdate`, `supportWhatsapp`, `currency` |
| 🔓 | GET | `/lookups/{type}` | القوائم الثابتة | `type`: `reject-reasons`, `dispute-reasons`, `shipping-companies`, `banks`, `duration-units`, `payment-methods`, `company-reject-reasons`. العنصر: `id, code, name, iconUrl` (مترجم) |
| 🔓 | GET | `/terms/{type}` | الشروط والأحكام | `type`: `platform`, `deal`, `company`. يرجع `version, title, items[]` |

### الميزة 2: إنشاء حساب (فرد وشركة)
**ماذا تفعل:** يختار المستخدم حساب أفراد أو شركات من شاشة Splash2. التسجيل ينشئ حساباً **غير مفعّل** ويرسل كود OTP بالرسائل. الجوال والإيميل فريدان. كلمات المرور تُخزَّن مشفّرة. الموافقة على الشروط إجبارية ويُحفظ إصدارها. حساب الشركة قد يبقى «قيد المراجعة» إن كان التحقق من السجل التجاري يدوياً.

| الحماية | Method | Path | الغرض | Request | الردود |
|---|---|---|---|---|---|
| 🔓 | POST | `/auth/register/individual` | تسجيل فرد | `username, phone, email, password (8 على الأقل), acceptTerms=true` | `201` + `phone, resendAfterSeconds, expiresInSeconds`؛ `409 PHONE_EXISTS/EMAIL_EXISTS`؛ `422` |
| 🔓 | POST | `/auth/register/company` | تسجيل شركة | `companyName, ownerPhone, commercialRegNo, taxNo?, email, password, acceptTerms` | نفسها + `409 COMMERCIAL_REG_EXISTS` |

### الميزة 3: تفعيل الحساب (OTP)
**ماذا تفعل:** شاشة «تفعيل الحساب» فيها 6 خانات وعدّاد لإعادة الإرسال (مثل 00:56). الكود صالح عدة دقائق مع حد أقصى للمحاولات الخاطئة. لإعادة الإرسال فترة انتظار (Rate Limit) تمنع الإزعاج وتكلفة الرسائل. عند النجاح يدخل المستخدم مباشرة ويستلم التوكنات.

| الحماية | Method | Path | الغرض | Request | الردود |
|---|---|---|---|---|---|
| 🔓 | POST | `/auth/otp/verify` | تفعيل الحساب | `phone, code` | `200` توكنات + `user`؛ `400 OTP_INVALID/OTP_EXPIRED/OTP_ATTEMPTS_EXCEEDED` |
| 🔓 | POST | `/auth/otp/resend` | إعادة إرسال الكود | `phone, purpose` (`register` أو `password_reset`) | `200` + `resendAfterSeconds`؛ `429` مع `Retry-After` |

### الميزة 4: تسجيل الدخول وإدارة الجلسة
**ماذا تفعل:** الدخول بالجوال وكلمة المرور. يرجع Access Token وRefresh Token (يتغير عند كل تجديد). الخروج يلغي الـ Refresh Token وتوكن الإشعارات للجهاز.

| الحماية | Method | Path | الغرض | Request | الردود |
|---|---|---|---|---|---|
| 🔓 | POST | `/auth/login` | دخول | `phone, password` | `200` توكنات + `user`؛ `401 INVALID_CREDENTIALS`؛ `403 ACCOUNT_NOT_VERIFIED` (يوجَّه لشاشة OTP) أو `ACCOUNT_SUSPENDED` |
| 🔓 | POST | `/auth/refresh` | تجديد التوكن | `refreshToken` | `200` توكنات جديدة؛ `401` إن كان منتهياً أو مستخدماً |
| 🔒 | POST | `/auth/logout` | خروج | `refreshToken?, deviceToken?` | `200` |

### الميزة 5: استرداد كلمة المرور (3 خطوات)
**ماذا تفعل:** تطابق شاشات forget password: (1) إدخال الجوال، (2) إدخال الكود، (3) كلمة مرور جديدة، ثم شاشة النجاح. بين الخطوتين 2 و3 يصدر الـ API `resetToken` قصير العمر ويُستخدم مرة واحدة، فلا يمكن تخطي الخطوة 2.

| الحماية | Method | Path | الغرض | Request | الردود |
|---|---|---|---|---|---|
| 🔓 | POST | `/auth/password/forgot` | الخطوة 1: إرسال الكود | `phone` | `200` + مواعيد الإرسال؛ `404` إن لم يوجد (أو `200` دائماً حتى لا تُكشف الأرقام المسجلة، وهو قرار أمني) |
| 🔓 | POST | `/auth/password/verify-otp` | الخطوة 2: التحقق | `phone, code` | `200` + `resetToken`؛ `400` |
| 🔓 | POST | `/auth/password/reset` | الخطوة 3: كلمة جديدة | `resetToken, newPassword, confirmPassword` | `200`؛ `422` عدم تطابق أو كلمة ضعيفة |

### الميزة 6: وضع الزائر («تخطي»)
**ماذا تفعل:** «تخطي» يدخل التطبيق بدون حساب فتظهر بطاقة «محفظتك غير مفعّلة، سجّل الدخول». ليس له Endpoint مخصص. الـ Endpoints العامة تعمل بدون توكن (`/config` و`/lookups` و`/terms` و`/support/faqs`)، واستدعاء `/me/home` بدون توكن يرجع `accountType: "guest"` و`wallet.activated=false`. أي إجراء آخر يرجع `401` فيوجّه التطبيق المستخدم لشاشة الدخول.

### الميزة 7: الشاشة الرئيسية
**ماذا تفعل:** استدعاء واحد يملأ الرئيسية حسب نوع الحساب:
- **فرد:** بطاقة المحفظة (الرصيد، مفعّلة أو لا)، المعاملات النشطة مع **الشارة الزمنية** (أصفر: متبقي 3 أيام، أحمر: متبقي 6 ساعات)، ونقطة الإشعارات.
- **شركة:** عدد المعاملات النشطة، إجمالي المستحقات، الدفعات القادمة بحالتها (في موعدها / قريبة / قادمة).
- **زائر:** بطاقة تسجيل الدخول فقط.

| الحماية | Method | Path | الغرض | الرد |
|---|---|---|---|---|
| 🔓/🔒 | GET | `/me/home` | بيانات الرئيسية | `accountType, unreadNotifications, wallet{activated,balance,heldAmount,currency}, activeDeals[], activeDealsCount, company{...}` |
| 🔒 | GET | `/company/dashboard` | لوحة الشركة (نقطة مستقلة) | `activeDealsCount, totalDues, upcomingPaymentsCount, upcomingPayments[]` |

### الميزة 8: الإشعارات وتسجيل الجهاز
**ماذا تفعل:** أيقونة الجرس تعرض قائمة الإشعارات. لكل إشعار `type` و`dealId`/`paymentId` ليفتح الضغط عليه الشاشة المناسبة. الأنواع: `deal_request, deal_accepted, deal_rejected, deal_edit, deal_funded, deal_shipped, deal_delivered, dispute_update, payment_approval, payment_due, penalty, wallet, system`. ولإرسال Push يسجّل التطبيق توكن FCM الخاص بالجهاز.

| الحماية | Method | Path | الغرض | التفاصيل |
|---|---|---|---|---|
| 🔒 | GET | `/notifications` | القائمة | مرقّمة. العنصر: `id, type, title, body, isRead, dealId, paymentId, createdAt` |
| 🔒 | GET | `/notifications/unread-count` | عدد غير المقروء | `{count}` لنقطة الجرس |
| 🔒 | PUT | `/notifications/{id}/read` | تعليم واحد كمقروء | |
| 🔒 | PUT | `/notifications/read-all` | تعليم الكل كمقروء | |
| 🔒 | POST | `/devices` | تسجيل جهاز | `deviceToken, platform (android/ios)` |
| 🔒 | DELETE | `/devices/{deviceToken}` | إلغاء تسجيل الجهاز | يُستدعى عند الخروج |

### الميزة 9: الملف الشخصي («حسابي»)
**ماذا تفعل:** يعرض الاسم والإيميل والصورة وشارة «موثق عبر نفاذ» والحساب البنكي وروابط المساعدة والإعدادات. يعدّل المستخدم بياناته ويغيّر كلمة المرور واللغة ويحذف حسابه. يُرفض الحذف (`409`) إذا وُجدت معاملات نشطة أو رصيد.

| الحماية | Method | Path | الغرض | Request / الرد |
|---|---|---|---|---|
| 🔒 | GET | `/me` | بياناتي | `id, code (كود البحث), type, name, phone, email, avatarUrl, language, phoneVerified, nafathVerified, walletActivated, bankAccount, company{commercialRegNo,taxNo}` |
| 🔒 | PUT | `/me` | تعديل البيانات | `username?, email?`؛ `422` تكرار الإيميل |
| 🔒 | PUT | `/me/password` | تغيير كلمة المرور | `currentPassword, newPassword` |
| 🔒 | PUT | `/me/language` | تغيير اللغة | `language: ar|en` |
| 🔒 | POST | `/me/avatar` | رفع الصورة | `multipart/form-data` بحقل `file` |
| 🔒 | DELETE | `/me` | حذف الحساب | `409` عند وجود معاملات نشطة أو رصيد |

### الميزة 10: التحقق من الهوية عبر نفاذ
**ماذا تفعل:** بطاقة «التحقق من الهوية» (اختيارية) تبدأ عملية نفاذ. يرجع الـ API **رقماً عشوائياً** (مثل 42) يختاره المستخدم داخل تطبيق نفاذ. يسأل الموبايل عن الحالة كل 3 ثوانٍ حتى تصير `verified` أو `rejected` أو `expired` (بعد نحو دقيقتين). عند النجاح تظهر شارة «موثق» ويُفتح السحب. التكامل الفعلي يحتاج اعتماداً مسبقاً من نفاذ.

| الحماية | Method | Path | الغرض | الرد |
|---|---|---|---|---|
| 🔒 | POST | `/me/kyc/nafath/start` | بدء التحقق | `requestId, randomNumber, expiresInSeconds` |
| 🔒 | GET | `/me/kyc/nafath/status?requestId=` | الحالة (Polling) | `status: pending | verified | rejected | expired` |

### الميزة 11: الحساب البنكي
**ماذا تفعل:** يُستخدم للسحب. يدخل المستخدم اسم صاحب الحساب والـ IBAN. يتحقق الـ API من صحة الـ IBAN (الطول وخوارزمية المراجعة) ويستنتج اسم البنك وشعاره من كود البنك. ويرجع دائماً **IBAN مقنّعاً فقط** (مثل `SA•••••••••4582`).

| الحماية | Method | Path | الغرض | Request / الرد |
|---|---|---|---|---|
| 🔒 | GET | `/me/bank-account` | عرض الحساب | `holderName, ibanMasked, bankName, bankLogoUrl` (أو `null`) |
| 🔒 | PUT | `/me/bank-account` | إضافة أو تعديل | `holderName, iban`؛ `422 INVALID_IBAN` |

### الميزة 12: البحث عن البائع
**ماذا تفعل:** في «معاملة جديدة» حقل لجوال البائع أو كوده مع أيقونة بحث. تعرض النتيجة بطاقة فيها الاسم والتوثيق والتقييم وعدد المعاملات الناجحة ليتأكد المشتري قبل المتابعة. لا تُرجع بيانات حساسة (بدون إيميل).

| الحماية | Method | Path | الغرض | الرد |
|---|---|---|---|---|
| 🔒 | GET | `/users/lookup?q=` | بحث بالجوال أو الكود | `id, name, phone, whatsappUrl, avatarUrl, verified, rating, successfulDeals`؛ `404` إن لم يوجد |

### الميزة 13: إنشاء معاملة (فرد)
**ماذا تفعل:** يدخل المشتري: اسم الصفقة، البائع، تفاصيل الاتفاق (حتى 200 حرف)، القيمة، مدة التنفيذ (رقم + ساعة/يوم)، هل يحتاج شحناً (فيختار شركة الشحن والعنوان)، ويوافق على الشروط. تُنشأ المعاملة بحالة `pending_seller` ويصل البائع إشعار وتظهر شاشة «تم الإرسال بنجاح». **لا يُسحب أي مبلغ بعد**؛ يُحجز المبلغ بعد قبول البائع.

| الحماية | Method | Path | الغرض | Request | الردود |
|---|---|---|---|---|---|
| 🔒 | POST | `/deals` | إنشاء معاملة | `title, sellerPhoneOrCode, description (≤200), amount, durationValue, durationUnit, needShipping, shippingCompanyId?, shippingAddressId?, acceptTerms` | `201` + المعاملة كاملة؛ `422` (مثل: شحن بدون شركة، أو البائع هو نفسه المشتري) |

### الميزة 14: قبول المعاملة أو رفضها
**ماذا تفعل:** يرى البائع الطلبات في تبويب «طلبات جديدة». **القبول** يعرض شاشة الشروط وعند التأكيد تصير الحالة `accepted` ويُطلب من المشتري الدفع. **الرفض** يتطلب سبباً من القائمة أو «سبب آخر» مع نص (إجباري عندها). ويستخدم المشتري نقطة القبول نفسها بعد مراجعة تعديل البائع. يتحقق الـ API أن الحالة الحالية تسمح بالإجراء، وإلا `409 INVALID_DEAL_STATE`.

| الحماية | Method | Path | الغرض | Request | الردود |
|---|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/accept` | قبول | `acceptTerms=true` | `200` المعاملة بحالتها الجديدة؛ `409` |
| 🔒 | POST | `/deals/{id}/reject` | رفض | `reasonId, note? (≤200)` | `200`؛ `409` |

### الميزة 15: اقتراح التعديل والموافقة عليه
**ماذا تفعل:** بدل الرفض يستطيع البائع اقتراح تعديل **القيمة أو المدة أو التفاصيل**. تصير الحالة `edit_proposed` ويصل المشتري إشعار ويرى مقارنة «قبل وبعد» ثم يوافق أو يرفض. لا يسري التعديل إلا بعد موافقة الطرف الآخر. عند الرفض يرى البائع بانراً أحمر «تم رفض التعديلات من قبل المشتري».

| الحماية | Method | Path | الغرض | Request |
|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/edits` | اقتراح تعديل | `amount?, durationValue?, durationUnit?, description?` |
| 🔒 | POST | `/deals/{id}/edits/{editId}/approve` | الموافقة | لا يوجد |
| 🔒 | POST | `/deals/{id}/edits/{editId}/reject` | الرفض | لا يوجد |

الثلاثة ترجع المعاملة كاملة مع `pendingEdit{previous, proposed, proposedBy, status}`.

### الميزة 16: الدفع وحجز المبلغ (Escrow)
**ماذا تفعل:** بعد قبول البائع يدفع المشتري. **تحتفظ المنصة بالمبلغ** ولا يصل للبائع حتى يؤكد المشتري الاستلام. الوسائل: Apple Pay (الظاهرة في التصميم) وmada وبطاقة أو رصيد المحفظة. قد تُطلب خطوة تحقق 3DS (`requires_action` مع `redirectUrl`). **هيدر `Idempotency-Key` إجباري** (UUID يولّده التطبيق) حتى لا يُخصم المبلغ مرتين لو أُعيد الطلب. النتيجة النهائية تؤكدها البوابة بشكل غير متزامن عبر الـ Webhook (الميزة 27).

| الحماية | Method | Path | الغرض | Request | الردود |
|---|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/pay` | حجز المبلغ | هيدر `Idempotency-Key`؛ والجسم `paymentMethod (apple_pay|mada|card|wallet), paymentToken?` | `200` + `status: succeeded|requires_action|failed, redirectUrl?, deal`؛ `402 PAYMENT_FAILED` |

بعد النجاح: `fundsHeld=true`، ويرى المشتري «المبلغ محجوز لدينا حتى انتهاء المعاملة»، ويُخطَر البائع ليبدأ التنفيذ.

### الميزة 17: الشحن والتتبع
**ماذا تفعل:** في المعاملات المشحونة يسجّل البائع الشحنة (إنشاء بوليصة أرامكس عبر التكامل، أو إدخال رقم تتبع يدوياً). تصير الحالة `shipped` ويُخطَر المشتري. يتتبع المشتري الشحنة من التطبيق. وتتابع مهمة خلفية حالة أرامكس وتحوّل المعاملة إلى «تم التسليم» تلقائياً.

| الحماية | Method | Path | الغرض | Request / الرد |
|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/ship` | البائع يسجل الشحن | `trackingNumber?` (فارغ = إنشاء شحنة أرامكس تلقائياً) |
| 🔒 | GET | `/deals/{id}/tracking` | تتبع الشحنة | `trackingNumber, status, events[{at, description}]` |

### الميزة 18: تأكيد الاستلام و«لم أستلم»
**ماذا تفعل:** هذه آلية الأمان الأساسية.
- **تأكيد الاستلام:** يؤكد المشتري فيُحرَّر المبلغ من الحجز إلى رصيد البائع وتصير الحالة `completed`، وتظهر شاشة النجاح مع التقييم.
- **لم أستلم:** تظهر شاشة «deal failure» (يبقى المبلغ محجوزاً ولا يُحوَّل شيء) ويُفتح مسار النزاع.

| الحماية | Method | Path | الغرض | الرد |
|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/confirm-delivery` | تأكيد الاستلام وتحرير المبلغ | المعاملة `completed`؛ `409` إن لم تسمح الحالة |
| 🔒 | POST | `/deals/{id}/not-received` | الإبلاغ بعدم الاستلام | المعاملة وفي `availableActions` الإجراء `open_dispute` |

### الميزة 19: التقييم وصفحة المتجر
**ماذا تفعل:** بعد الاكتمال يقيّم المشتري المتجر (من 1 إلى 5 نجوم + تعليق اختياري)، تقييم واحد لكل معاملة (`409` عند التكرار). تعرض صفحة المتجر التقييم العام (مثل 4.9) وعدد المعاملات الناجحة (مثل 345) وقائمة تقييمات المستخدمين مع شارة المراجع الموثّق.

| الحماية | Method | Path | الغرض | Request / الرد |
|---|---|---|---|---|
| 🔒 | POST | `/deals/{id}/rating` | إضافة تقييم | `stars (1-5), comment? (≤300)` |
| 🔒 | GET | `/sellers/{id}/store` | صفحة المتجر | `id, name, category, bannerUrl, rating, successfulDeals` |
| 🔒 | GET | `/sellers/{id}/reviews` | تقييمات المتجر | مرقّمة: `reviewerName, reviewerAvatarUrl, verified, stars, comment, createdAt` |

### الميزة 20: قوائم المعاملات وتفاصيلها وسجل الحالات
**ماذا تفعل:** يتصفح المشتري والبائع المعاملات في تبويبات (جديدة / قيد التنفيذ / المكتملة). تتغير شاشة التفاصيل حسب الحالة، لذلك يرجع الـ API:
- `status`: الحالة.
- `banner`: التنبيه الملوّن أعلى الشاشة (أزرق: عدّل البائع، أخضر: وافق المشتري، أحمر: رُفضت التعديلات، أصفر: المبلغ محجوز).
- **`availableActions`**: الأزرار التي تظهر **للمستخدم الحالي** فقط (`accept, reject, edit, approve_edit, reject_edit, pay, ship, confirm_delivery, report_not_received, rate, open_dispute, contact_support, view_tracking`).
- `badge`: نص ولون الوقت المتبقي جاهزين.

| الحماية | Method | Path | الغرض | التفاصيل |
|---|---|---|---|---|
| 🔒 | GET | `/deals?role=&tab=&page=&pageSize=` | القائمة | `role: buyer|seller` (إجباري)، `tab: new|in_progress|completed`. العناصر `DealSummary` |
| 🔒 | GET | `/deals/{id}` | التفاصيل | `Deal` كامل: الطرف الآخر، الشحن، `pendingEdit`، سبب الرفض، `dispute`، `rated`؛ `404` إن لم يكن المستخدم طرفاً |
| 🔒 | GET | `/deals/{id}/timeline` | سجل الحالات | `[{status, label, at}]` |

### الميزة 21: المحفظة والسحب
**ماذا تفعل:** تعرض الرصيد المتاح والمبالغ المحجوزة وسجل الحركات (مدفوع Paid، مسترد Refund، تحرير، سحب، غرامة). المحفظة **غير مفعّلة** للزائر ولمن لم يستوفِ الشروط. **السحب** يحوّل الرصيد إلى الحساب البنكي للمستخدم ويتطلب حساباً بنكياً مسجلاً، وتوثيق نفاذ (حسب قرار العمل)، ورصيداً كافياً. وهو أيضاً بـ `Idempotency-Key`. كل حركة تُسجَّل في دفتر قيد مزدوج ولا تُعدَّل أبداً.

| الحماية | Method | Path | الغرض | Request / الردود |
|---|---|---|---|---|
| 🔒 | GET | `/wallet` | الرصيد | `activated, balance, heldAmount, currency` |
| 🔒 | GET | `/wallet/transactions` | السجل | `type (paid|refund|release|withdrawal|topup|penalty), amount (موجب/سالب), dealNumber, createdAt` |
| 🔒 | POST | `/wallet/withdraw` | طلب سحب | هيدر `Idempotency-Key`؛ الجسم `amount`؛ `422 INSUFFICIENT_BALANCE / BANK_ACCOUNT_REQUIRED / KYC_REQUIRED` |
| 🔒 | GET | `/wallet/withdrawals` | طلبات السحب | `id, amount, status (pending|processing|completed|rejected), createdAt` |

### الميزة 22: رفع الملفات والنزاعات
**ماذا تفعل:** عند «لم أستلم» أو أي مشكلة يفتح المشتري **نزاعاً**: يختار سبباً من 6 أسباب، ويكتب التفاصيل (≤200 حرف)، ويرفق حتى **5 ملفات** (صور أو PDF)، ويؤكد «أؤكد صحة المعلومات». تُرفع الملفات أولاً وتعطي `fileId` يُمرَّر عند فتح النزاع. فتح النزاع يجمّد المبلغ، ويأخذ رقماً مثل `D-45872` وحالة «قيد المراجعة»، ويُخطَر البائع. يراجع الأدمن من لوحة Angular ويقرر: تحرير للبائع أو استرجاع للمشتري، ويمكنه طلب مستندات إضافية من الطرفين عبر الرسائل.

| الحماية | Method | Path | الغرض | Request / الردود |
|---|---|---|---|---|
| 🔒 | POST | `/uploads` | رفع ملف | `multipart` بحقل `file` (صورة/PDF، حتى 10MB)؛ `201` + `fileId, url`؛ `422` نوع أو حجم غير مسموح |
| 🔒 | POST | `/disputes` | فتح نزاع | `dealId, reasonId, details (≤200), fileIds[≤5], confirmed=true`؛ `201` + `Dispute{number, status...}`؛ `409` نزاع مفتوح أو حالة لا تسمح |
| 🔒 | GET | `/disputes` | نزاعاتي | قائمة مرقّمة |
| 🔒 | GET | `/disputes/{id}` | تفاصيل النزاع | `number, reason, details, status (under_review|awaiting_info|resolved_buyer|resolved_seller|closed), files, resolution` |
| 🔒 | POST | `/disputes/{id}/messages` | رسالة أو مستندات إضافية | `message?, fileIds[]?` |

### الميزة 23: الدعم والمساعدة
**ماذا تفعل:** قسم المساعدة في «حسابي»: الأسئلة الشائعة (عامة حتى للزائر)، «تواصل معنا» (ينشئ تذكرة يمكن ربطها بمعاملة)، و«طلبات الدعم» لمتابعة التذاكر وحالتها. زر «تواصل مع الدعم» يظهر في المعاملات المكتملة. وزر واتساب في المعاملة يفتح محادثة مع الطرف الآخر برابط `whatsappUrl` من بيانات الطرف.

| الحماية | Method | Path | الغرض | التفاصيل |
|---|---|---|---|---|
| 🔓 | GET | `/support/faqs` | الأسئلة الشائعة | `id, question, answer` |
| 🔒 | POST | `/support/contact` | إنشاء تذكرة | `subject, message, dealId?` |
| 🔒 | GET | `/support/tickets` | طلبات الدعم | `id, number (S-1021), subject, status (open|in_review|closed), createdAt` |

*مقترح وغير موجود في العقد بعد:* `GET /support/tickets/{id}` (تفاصيل التذكرة والردود).

### الميزة 24: إنشاء معاملة شركة (معالج من 5 خطوات)
**ماذا تفعل:** تدفق «فتح معاملة جديدة» للشركات من خمس خطوات. كل خطوة تُحفظ في **مسودة (Draft)** ليتمكن المستخدم من الرجوع أو الإكمال لاحقاً، ثم المراجعة والإرسال:
1. **تفاصيل المعاملة:** الشركة (الطرف الآخر)، اسم المعاملة، جوال الشركة، التفاصيل، القيمة الإجمالية، تاريخ البدء، إرفاق العقد.
2. **جدول الدفعات:** إضافة وحذف دفعات (مبلغ + تاريخ). **القاعدة:** مجموع الدفعات = القيمة الإجمالية، وكل تاريخ بعد تاريخ البدء.
3. **إعدادات اعتماد الدفعات:** قبل الاستحقاق بكم يوم يُرسل طلب الموافقة، ومهلة الرد، وملاحظات.
4. **الشرط الجزائي:** نسبة من القيمة الإجمالية وبنود. يُطبَّق تلقائياً إذا تأخرت الشركة في أي دفعة أكثر من **30 يوماً** بعد تاريخ الاستحقاق.
5. **المراجعة والإرسال:** عرض كل شيء ثم الإرسال. يصل الطرف الآخر إشعار وتظهر شاشة النجاح.

| الحماية | Method | Path | الغرض | Request / الردود |
|---|---|---|---|---|
| 🔒 | POST | `/company/deals/drafts` | الخطوة 1: إنشاء مسودة | `companyName, title, companyPhone, details (≤200), totalAmount, startDate, contractFileId?`؛ `201` + `draftId` |
| 🔒 | PUT | `/company/deals/drafts/{draftId}` | تعديل الخطوة 1 | نفس حقول الخطوة 1 |
| 🔒 | PUT | `/company/deals/drafts/{draftId}/schedule` | الخطوة 2: الجدول | `payments[{amount, dueDate}]`؛ `422 PAYMENTS_TOTAL_MISMATCH` |
| 🔒 | PUT | `/company/deals/drafts/{draftId}/approval-settings` | الخطوة 3: إعدادات الاعتماد | `sendRequestDaysBefore, responseDeadlineDays, notes?` |
| 🔒 | PUT | `/company/deals/drafts/{draftId}/penalty` | الخطوة 4: الشرط الجزائي | `percentage, graceDays (30), clauses?` |
| 🔒 | GET | `/company/deals/drafts/{draftId}/review` | الخطوة 5: المراجعة | `CompanyDeal` كامل بالدفعات والشرط |
| 🔒 | POST | `/company/deals/drafts/{draftId}/submit` | إنشاء وإرسال | `201` + `CompanyDeal`؛ `422` إن نقصت خطوة |

### الميزة 25: إدارة معاملات الشركات
**ماذا تفعل:** قائمة المعاملات في تبويبات (جديدة / نشطة / مكتملة). تفاصيل الصفقة تعرض القيمة وتاريخ البدء و**جدول الدفعات وحالاتها** (تم السداد / بانتظار الموافقة / قادمة) والشرط الجزائي وزر «عقد المعاملة». يقوم الطرف الآخر (الذي أُنشئت له المعاملة) بـ **القبول أو الرفض** مع ذكر السبب عند الرفض. ويُحمَّل العقد عبر رابط مؤقت لحماية الملف.

| الحماية | Method | Path | الغرض | Request / الرد |
|---|---|---|---|---|
| 🔒 | GET | `/company/deals?tab=` | القائمة | `tab: new|active|completed`. العنصر: `id, number, title, counterpartyName, totalAmount, startDate, status` |
| 🔒 | GET | `/company/deals/{id}` | التفاصيل | `CompanyDeal` مع `payments[], approvalSettings, penalty, hasContract` |
| 🔒 | POST | `/company/deals/{id}/accept` | قبول (الطرف الآخر) | لا يوجد |
| 🔒 | POST | `/company/deals/{id}/reject` | رفض (الطرف الآخر) | `reasonId, note?` |
| 🔒 | GET | `/company/deals/{id}/contract` | رابط العقد | `url, expiresAt` |

### الميزة 26: الدفعات واعتمادها
**ماذا تفعل:** لكل دفعة دورة حياة: `scheduled` ← (قبل الاستحقاق بالأيام المحددة) `awaiting_approval` ← `approved` أو `rejected` ← `paid` أو `overdue` ← `penalty_applied`. قبل موعد الاستحقاق يصل الطرف الآخر طلب اعتماد فيراجع التفاصيل والمرفقات ويتأكد من استيفاء مراحل المعاملة، ثم **يعتمد** أو **يرفض مع ذكر السبب** خلال مهلة الرد. تعرض الرئيسية «الدفعات القادمة» بشارة: في موعدها / قريبة / قادمة / متأخرة. والتأخر الذي يتجاوز 30 يوماً يُطبَّق عليه الشرط الجزائي تلقائياً عبر مهمة خلفية.

| الحماية | Method | Path | الغرض | Request / الردود |
|---|---|---|---|---|
| 🔒 | GET | `/company/payments/upcoming` | الدفعات القادمة (عرض الكل) | `paymentId, dealId, counterpartyName, amount, dueDate, badge (on_time|near|upcoming|overdue)` |
| 🔒 | GET | `/company/payments/{id}` | تفاصيل دفعة (شاشة الاعتماد) | `order, label, amount, dueDate, status, dealNumber, dealTitle, notes, availableActions[approve|reject]` |
| 🔒 | POST | `/company/payments/{id}/approve` | اعتماد | `200`؛ `409` إن لم تسمح الحالة |
| 🔒 | POST | `/company/payments/{id}/reject` | رفض | `reason (≤200)` |

### الميزة 27: Webhook بوابة الدفع (داخلي)
**ماذا تفعل:** تُبلغ البوابة الـ Backend بنتائج الدفع بشكل غير متزامن (نجاح، فشل، استرداد). **لا يستخدمه تطبيق الموبايل أبداً.** يُؤمَّن بالتحقق من توقيع البوابة وتجاهل الأحداث المكررة. يحدّث الحجز والمعاملة والمحفظة ويرسل Push.

| الحماية | Method | Path | الغرض |
|---|---|---|---|
| 🔓 (بتوقيع) | POST | `/payments/webhook` | استقبال أحداث البوابة |

*مقترح وغير موجود في العقد بعد:* `POST /payments/intents` (إنشاء نية دفع قبل الدفع من الموبايل)، فقط إن كانت بوابتك تتطلبه.

### الميزة 28: ميزات بلا Endpoints للموبايل
**المهام الخلفية (Hangfire):** انظر القسم 7.
**لوحة التحكم و`/admin/api`:** انظر القسم 8.

---

## 5) آلات الحالات

**معاملة فرد**
```
Draft → PendingSeller → (Rejected | EditProposed ⇄ PendingSeller/PendingBuyerApproval) → Accepted
→ Funded (محجوز) → Shipped → Delivered → Completed (تحرير المبلغ)
                          ↘ Disputed → (ReleasedToSeller | RefundedToBuyer)
Expired / Cancelled ممكنة في المراحل المبكرة
```
قيم الـ API: `draft, pending_seller, edit_proposed, pending_buyer_approval, accepted, funded, shipped, delivered, completed, rejected, disputed, refunded, expired, cancelled`.

**دفعة شركة**
```
Scheduled → ApprovalRequested → (Approved | Rejected) → Paid | Overdue → PenaltyApplied
```
قيم الـ API: `scheduled, awaiting_approval, approved, rejected, paid, overdue, penalty_applied`.

تُحسب الشارات الزمنية من مدة التنفيذ ووقت القبول (أصفر عند بقاء 3 أيام، أحمر عند بقاء 6 ساعات).

---

## 6) نموذج البيانات (الكيانات الرئيسية)

Users, CompanyProfiles, Otps, RefreshTokens, Devices (FCM), NafathVerifications, BankAccounts, Banks,
Deals, DealEditRequests, DealStatusHistory, DealAttachments, RejectReasons, TermsVersions + TermsAcceptances,
Shipments, ShippingCompanies,
Wallets, WalletTransactions (دفتر قيد مزدوج), EscrowHolds, PaymentIntents, WithdrawalRequests,
Ratings, Disputes, DisputeReasons, DisputeMessages, DisputeFiles, SupportTickets, Faqs,
CompanyDeals, PaymentSchedules (الدفعات), PaymentApprovals, PenaltyClauses, PenaltyCharges, Contracts,
Notifications, AuditLogs, AdminUsers/Roles/Permissions, AppSettings.

القواعد: حذف ناعم (Soft delete)؛ سجل تدقيق على كل حركة مالية؛ المبالغ `decimal(18,2)`؛ السجلات المالية لا تُعدَّل بل تُعاكَس بقيد جديد؛ تشفير الـ IBAN في قاعدة البيانات.

---

## 7) المهام الخلفية

- إنهاء المعاملات التي لم يرد عليها البائع في مدتها.
- إرسال تذكيرات قبل المواعيد النهائية (Push + SMS) وتحديث الشارات الزمنية.
- إرسال طلبات اعتماد الدفعات قبل الاستحقاق وتطبيق مهلة الرد.
- **محرك الشرط الجزائي:** بعد 30 يوماً من التأخر يحسب الغرامة ويسجلها تلقائياً.
- مزامنة حالة شحنات أرامكس وتحويل المعاملة إلى «تم التسليم».
- التحرير التلقائي للمبلغ بعد مدة من التسليم إن لم يعترض المشتري (فقط إن اعتُمد كقاعدة عمل).
- تنظيف أكواد OTP والتوكنات المنتهية.
- مطابقة يومية مع بوابة الدفع.

---

## 8) لوحة التحكم (Angular) و`/admin/api`

التصميم الحالي للموبايل فقط، فاللوحة تحتاج تصميماً أو مكتبة مكونات جاهزة (PrimeNG أو Angular Material) بدعم RTL. وهي ليست جزءاً من عقد الموبايل؛ اكتب لها OpenAPI منفصلاً.

| الصفحة | الوظائف |
|---|---|
| تسجيل الدخول والصلاحيات | أدوار وصلاحيات (RBAC)، مصادقة ثنائية للأدمن |
| لوحة المؤشرات | المستخدمون، المعاملات، القيمة المحجوزة، النزاعات المفتوحة |
| المستخدمون | بحث، تفعيل/إيقاف، حالة نفاذ، مراجعة حسابات الشركات والسجل التجاري |
| المعاملات | قوائم وفلاتر وتفاصيل وسجل الحالات وتدخل يدوي |
| النزاعات | قائمة، تفاصيل، مرفقات، رسائل، القرار (تحرير للبائع أو استرجاع للمشتري) |
| المالية | الحجوزات، السحوبات (اعتماد/رفض)، دفتر الحركات، تقارير وتصدير Excel |
| معاملات الشركات | الدفعات والغرامات والتأخيرات |
| المحتوى | الأسئلة الشائعة، الشروط (بإصدارات)، أسباب الرفض والنزاع |
| الإشعارات | إرسال جماعي، قوالب |
| الدعم | صندوق التذاكر والرد عليها |
| الإعدادات | العمولات، شركات الشحن، البنوك، مدد الانتظار |
| سجل التدقيق | من فعل ماذا ومتى |

---

## 9) التكاملات الخارجية

| التكامل | الاستخدام | ملاحظة |
|---|---|---|
| مزود SMS | OTP والتنبيهات | مزود سعودي محلي؛ يتطلب تسجيل Sender ID |
| FCM / APNs | الإشعارات Push | |
| **نفاذ** | التحقق من الهوية | يتطلب اعتماداً مسبقاً؛ قدّم الطلب فوراً |
| بوابة دفع | Apple Pay / mada / بطاقات | تحتاج حساب تاجر. **الاحتفاظ بأموال العملاء يتطلب ترخيصاً أو شريكاً مرخصاً من ساما** |
| أرامكس | إنشاء الشحنات والتتبع | غلّفه بطبقة Adapter ليسهل تبديل المزود |
| التحقق من IBAN والسجل التجاري | فحص البيانات | اختياري |
| تخزين الملفات | S3 أو Azure Blob | روابط موقّعة قصيرة العمر للعقود |

---

## 10) المتطلبات غير الوظيفية

- **الأمان:** JWT قصير العمر + تدوير Refresh؛ Argon2/BCrypt؛ Rate Limit على OTP والدخول؛ Idempotency على endpoints المال؛ تشفير IBAN؛ OWASP؛ الامتثال لنظام حماية البيانات الشخصية (PDPL)؛ توقيع الـ Webhooks.
- **سلامة المال:** معاملات قاعدة البيانات (Transactions)، قفل تفاؤلي لمنع الصرف المزدوج، دفتر قيد مزدوج.
- **الأداء:** Redis للتخزين المؤقت، فهارس، ترقيم صفحات.
- **المراقبة:** Serilog + Seq/ELK، Health Checks، تنبيهات.
- **التقنيات المقترحة:** أحدث .NET LTS، Clean Architecture، EF Core، SQL Server أو PostgreSQL، MediatR، FluentValidation، Swagger/OpenAPI، Hangfire، Redis، Docker، CI/CD. وAngular بأحدث إصدار مع RTL وNgRx أو Signals.
- **الاختبارات:** اختبارات وحدة وتكامل (حوالي 70% عاماً و90% فأكثر للمنطق المالي)، واختبار أمان واختبار اختراق قبل الإطلاق.

---

## 11) الجدول الزمني ومواعيد تسليم الـ API

أسبوع العمل: الأحد إلى الخميس. البداية: **الأحد 18 أكتوبر 2026**.
الفريق المفترض: 2 Backend، 2 Angular، 1 QA، DevOps جزئي، وفريق موبايل منفصل. إن كان شخص واحد ينفذ كل شيء فالمتوقع من 6 إلى 8 أشهر تقريباً.

| المرحلة | المدة | التواريخ | المخرجات |
|---|---|---|---|
| 0. التأسيس | أسبوع | 18 – 22 أكتوبر | المعمارية، قاعدة البيانات، عقد OpenAPI، CI/CD، بيئات Dev/Staging، بدء طلبات نفاذ والبوابة وأرامكس |
| 1. الحسابات والدخول | أسبوعان | 25 أكتوبر – 5 نوفمبر | التسجيل، OTP، الدخول، استرداد كلمة المرور، وضع الزائر، اللغات |
| 2. الملف والإشعارات | أسبوعان | 8 – 19 نوفمبر | الملف الشخصي، البنك/IBAN، نفاذ، الإشعارات (Push/SMS)، الدعم والأسئلة الشائعة |
| 3. معاملات الأفراد | 4 أسابيع | 15 نوفمبر – 10 ديسمبر | الإنشاء، القبول/الرفض/التعديل، الشروط، آلة الحالات، الرئيسية، الشارات الزمنية |
| 4. المحفظة والدفع | 4 أسابيع | 29 نوفمبر – 24 ديسمبر | البوابة، الحجز والتحرير والاسترجاع، الدفتر، السحب |
| 5. الشحن والتقييم | 3 أسابيع | 13 – 31 ديسمبر | أرامكس، التتبع، تأكيد الاستلام، التقييمات وصفحة المتجر |
| 6. النزاعات والدعم | 3 أسابيع | 20 ديسمبر – 7 يناير | رفع الملفات، النزاعات، التذاكر، قرارات الأدمن |
| 7. معاملات الشركات | 5 أسابيع | 13 ديسمبر – 14 يناير | المعالج بـ5 خطوات، الدفعات، الاعتماد، الشرط الجزائي، العقود |
| 8. لوحة Angular | متوازية | 8 نوفمبر – 14 يناير | تُسلَّم تدريجياً مع كل وحدة |
| 9. الاختبار والتأمين | 3 أسابيع | 10 – 28 يناير | اختبار تكامل وأمان وأحمال، تصحيح الأخطاء |
| 10. UAT والإطلاق | أسبوعان | 31 يناير – 11 فبراير 2027 | اختبار القبول، النشر على الإنتاج، دعم ما بعد الإطلاق |

**الإطلاق المتوقع: منتصف فبراير 2027** (حوالي 17 أسبوعاً). لإطلاق أبكر يمكن إصدار MVP للأفراد فقط في أواخر يناير، والشركات في إصدار ثانٍ.

**مواعيد تسليم الـ API لفريق الموبايل**

| التسليم | التاريخ |
|---|---|
| عقد OpenAPI + خادم Mock (كل الوحدات) | 22 أكتوبر 2026 |
| الحسابات والدخول (Staging) | 5 نوفمبر 2026 |
| الملف والإشعارات | 19 نوفمبر 2026 |
| معاملات الأفراد | 10 ديسمبر 2026 |
| المحفظة والدفع | 24 ديسمبر 2026 |
| الشحن والتقييم والنزاعات | 7 يناير 2027 |
| وحدة الشركات | 14 يناير 2027 |

---

## 12) قائمة التسليم لفريق الموبايل

**قبل أي كود Backend (الأسبوع الأول)**
- عقد OpenAPI (`etamid-openapi.yaml`) بعد مراجعته والاتفاق عليه مع فريق الموبايل.
- **خادم Mock** مبني من العقد (Postman Mock Server، أو Prism: `prism mock openapi.yaml`).
- الاتفاقيات المشتركة (القسم 3): صيغة الرد، أكواد الأخطاء الثابتة، الترقيم، صيغة التاريخ والمبلغ، هيدر اللغة.

**البيئات والأدوات**
- بيئة Staging ثابتة مع رابط Swagger.
- Postman Collection محدّثة مع كل تسليم.
- حسابات تجريبية: فرد مشترٍ، فرد بائع، شركة؛ وكود OTP ثابت في Staging (مثل `123456`).
- بيانات تجريبية لمعاملات في كل حالة (جديدة، تعديل مقترح، محجوزة، مشحونة، نزاع، مكتملة...).

**اتفاقات يجب حسمها مع فريق الموبايل**
- `availableActions` و`banner` و`badge` يأتون من الخادم.
- عمر التوكن، التجديد التلقائي، والتصرف عند `401`.
- شكل الإشعار وتوجيهه (`type` + `dealId`).
- من يتعامل مع بوابة الدفع (الموبايل مباشرة أم عبر الـ Backend) وكيف يُعرف نجاح الدفع (Polling أم Push).
- حدود الرفع والأنواع المسموحة؛ الحد الأدنى لإصدار التطبيق من `/config`؛ أي Endpoints عامة للزائر.
- التحديث اللحظي: Push فقط أم SignalR لحالة المعاملة.
- نفاذ: SDK مباشرة أم عبر الـ Backend.

**بشكل مستمر**
- اجتماع أسبوعي من 15 دقيقة؛ ملف Changelog؛ لا تكسر الـ Endpoints الموجودة؛ الرد على المشاكل خلال يوم.

---

## 13) الأسئلة المفتوحة ونواقص التصميم

**أسئلة تؤثر على التصميم**
1. هل Angular للوحة الإدارة فقط أم أيضاً بوابة ويب للشركات؟
2. في معاملة الشركة: من يدفع ومن يستلم؟ (الشاشات تذكر «إجمالي المستحقات» وشرطاً جزائياً عند تأخر الشركة.)
3. هل يُحجز المبلغ عند **قبول البائع** أم عند **إرسال المشتري**؟ (الشاشات توحي بالقبول، والعقد مبني عليه.)
4. ما العمولة وعلى من تقع؟ ومتى يُحرَّر المبلغ تلقائياً إذا لم يؤكد المشتري؟
5. شاشة طريقة الدفع تظهر Apple Pay فقط. هل نحتاج mada والبطاقات والتحويل البنكي؟
6. هل دفعات الشركات تتم عبر المنصة أم تُسجَّل خارجياً فقط؟
7. هل نفاذ إلزامي قبل السحب أو قبل المعاملات الكبيرة؟
8. من مزود الدفع المرخص؟ (أكبر نقطة مخاطرة: الاحتفاظ بأموال الغير يتطلب ترخيصاً.)
9. الحد الأدنى والأقصى للمعاملة وللسحب.
10. هل يظهر التقييم بعد الاكتمال فقط؟ وهل يستطيع البائع الرد؟

**افتراضات في العقد تحتاج تأكيدك:** وسائل الدفع `apple_pay | mada | card | wallet`؛ `/deals/{id}/accept` يخدم البائع والمشتري (بعد التعديل)؛ الحجز يتم في `/deals/{id}/pay`؛ الطرف الآخر في معاملات الشركات اسمه `counterparty`.

**ناقص في التصميم:** شاشات الإشعارات، الأسئلة الشائعة، تواصل معنا، طلبات الدعم، الأمان والخصوصية، تتبع الشحنة، السحب، تعديل البنك، تعديل الملف؛ جانب الطرف الآخر في معاملات الشركات؛ حالات الفراغ والخطأ؛ وبعض الشاشات مقطوعة عند الأزرار السفلية.

---

## 14) المخاطر

| الخطر | الأثر | الحل |
|---|---|---|
| ترخيص حجز الأموال | يوقف الإطلاق | التعاقد مع مزود Escrow مرخص من البداية |
| تأخر اعتماد نفاذ | تأخير توثيق الملف الشخصي | تقديم الطلب في الأسبوع الأول وجعله اختيارياً في الإصدار الأول |
| تغير القواعد المالية | إعادة عمل | تثبيت آلة الحالات والـ Ledger مبكراً ومراجعتها مع المالية |
| شاشات ناقصة | تعطل فريق الموبايل | إرسال قائمة النواقص للمصمم الآن |
| الاعتماد على أرامكس | تأخير الشحن | طبقة Adapter تسمح بتبديل المزود |

---

## 15) ملحق: كل الـ Endpoints في جدول واحد

| # | Method | Path | الحماية | الميزة |
|---|---|---|---|---|
| 1 | GET | /config | 🔓 | 1 |
| 2 | GET | /lookups/{type} | 🔓 | 1 |
| 3 | GET | /terms/{type} | 🔓 | 1 |
| 4 | POST | /auth/register/individual | 🔓 | 2 |
| 5 | POST | /auth/register/company | 🔓 | 2 |
| 6 | POST | /auth/otp/verify | 🔓 | 3 |
| 7 | POST | /auth/otp/resend | 🔓 | 3 |
| 8 | POST | /auth/login | 🔓 | 4 |
| 9 | POST | /auth/refresh | 🔓 | 4 |
| 10 | POST | /auth/logout | 🔒 | 4 |
| 11 | POST | /auth/password/forgot | 🔓 | 5 |
| 12 | POST | /auth/password/verify-otp | 🔓 | 5 |
| 13 | POST | /auth/password/reset | 🔓 | 5 |
| 14 | GET | /me/home | 🔓/🔒 | 7 |
| 15 | GET | /company/dashboard | 🔒 | 7 |
| 16 | GET | /notifications | 🔒 | 8 |
| 17 | GET | /notifications/unread-count | 🔒 | 8 |
| 18 | PUT | /notifications/{id}/read | 🔒 | 8 |
| 19 | PUT | /notifications/read-all | 🔒 | 8 |
| 20 | POST | /devices | 🔒 | 8 |
| 21 | DELETE | /devices/{deviceToken} | 🔒 | 8 |
| 22 | GET | /me | 🔒 | 9 |
| 23 | PUT | /me | 🔒 | 9 |
| 24 | DELETE | /me | 🔒 | 9 |
| 25 | PUT | /me/password | 🔒 | 9 |
| 26 | PUT | /me/language | 🔒 | 9 |
| 27 | POST | /me/avatar | 🔒 | 9 |
| 28 | POST | /me/kyc/nafath/start | 🔒 | 10 |
| 29 | GET | /me/kyc/nafath/status | 🔒 | 10 |
| 30 | GET | /me/bank-account | 🔒 | 11 |
| 31 | PUT | /me/bank-account | 🔒 | 11 |
| 32 | GET | /users/lookup | 🔒 | 12 |
| 33 | POST | /deals | 🔒 | 13 |
| 34 | GET | /deals | 🔒 | 20 |
| 35 | GET | /deals/{id} | 🔒 | 20 |
| 36 | POST | /deals/{id}/accept | 🔒 | 14 |
| 37 | POST | /deals/{id}/reject | 🔒 | 14 |
| 38 | POST | /deals/{id}/edits | 🔒 | 15 |
| 39 | POST | /deals/{id}/edits/{editId}/approve | 🔒 | 15 |
| 40 | POST | /deals/{id}/edits/{editId}/reject | 🔒 | 15 |
| 41 | POST | /deals/{id}/pay | 🔒 | 16 |
| 42 | POST | /deals/{id}/ship | 🔒 | 17 |
| 43 | GET | /deals/{id}/tracking | 🔒 | 17 |
| 44 | POST | /deals/{id}/confirm-delivery | 🔒 | 18 |
| 45 | POST | /deals/{id}/not-received | 🔒 | 18 |
| 46 | POST | /deals/{id}/rating | 🔒 | 19 |
| 47 | GET | /deals/{id}/timeline | 🔒 | 20 |
| 48 | GET | /sellers/{id}/store | 🔒 | 19 |
| 49 | GET | /sellers/{id}/reviews | 🔒 | 19 |
| 50 | GET | /wallet | 🔒 | 21 |
| 51 | GET | /wallet/transactions | 🔒 | 21 |
| 52 | POST | /wallet/withdraw | 🔒 | 21 |
| 53 | GET | /wallet/withdrawals | 🔒 | 21 |
| 54 | POST | /uploads | 🔒 | 22 |
| 55 | POST | /disputes | 🔒 | 22 |
| 56 | GET | /disputes | 🔒 | 22 |
| 57 | GET | /disputes/{id} | 🔒 | 22 |
| 58 | POST | /disputes/{id}/messages | 🔒 | 22 |
| 59 | GET | /support/faqs | 🔓 | 23 |
| 60 | POST | /support/contact | 🔒 | 23 |
| 61 | GET | /support/tickets | 🔒 | 23 |
| 62 | POST | /company/deals/drafts | 🔒 | 24 |
| 63 | PUT | /company/deals/drafts/{draftId} | 🔒 | 24 |
| 64 | PUT | /company/deals/drafts/{draftId}/schedule | 🔒 | 24 |
| 65 | PUT | /company/deals/drafts/{draftId}/approval-settings | 🔒 | 24 |
| 66 | PUT | /company/deals/drafts/{draftId}/penalty | 🔒 | 24 |
| 67 | GET | /company/deals/drafts/{draftId}/review | 🔒 | 24 |
| 68 | POST | /company/deals/drafts/{draftId}/submit | 🔒 | 24 |
| 69 | GET | /company/deals | 🔒 | 25 |
| 70 | GET | /company/deals/{id} | 🔒 | 25 |
| 71 | POST | /company/deals/{id}/accept | 🔒 | 25 |
| 72 | POST | /company/deals/{id}/reject | 🔒 | 25 |
| 73 | GET | /company/deals/{id}/contract | 🔒 | 25 |
| 74 | GET | /company/payments/upcoming | 🔒 | 26 |
| 75 | GET | /company/payments/{id} | 🔒 | 26 |
| 76 | POST | /company/payments/{id}/approve | 🔒 | 26 |
| 77 | POST | /company/payments/{id}/reject | 🔒 | 26 |
| 78 | POST | /payments/webhook | 🔓 (بتوقيع) | 27 |
