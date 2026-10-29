# تفعيل البريد واستعادة كلمة المرور (Email OTP)

هذا التغيير يستبدل OTP المؤقت المحفوظ في الذاكرة برموز محفوظة في قاعدة البيانات بصيغة hash، ويستخدمه لتفعيل حسابات الأفراد والشركات واستعادة كلمة المرور.

## سياسة الرمز

- الرمز مكوّن من 6 أرقام وصلاحيته 10 دقائق.
- بعد 5 محاولات خاطئة يُرفض الرمز؛ ويؤدي طلب رمز جديد إلى إبطال الرمز السابق.
- يوجد فاصل 60 ثانية بين إرسال الرموز.
- الرمز وReset Token لا يُحفظان كنص واضح في قاعدة البيانات، وReset Token صالح لمرة واحدة.
- استعادة كلمة المرور لا تكشف في الرد ما إذا كان رقم الجوال مرتبطًا بحساب.

## الإعداد المحلي

إذا لم تكن بيانات SMTP مضبوطة في بيئة `Development`، فإن `Smtp:LogCodeWhenDisabled=true` يسمح بتسجيل الرمز في سجل تشغيل الـAPI للاختبار المحلي فقط؛ لا يرسل بريدًا حقيقيًا. هذا الخيار لا يكفي للإنتاج.

لإرسال البريد عبر Gmail، استخدم **App Password** جديدة بعد تفعيل التحقق بخطوتين. احفظها في User Secrets بدل `appsettings*.json` أو Git. من PowerShell:

```powershell
cd D:\etamid\backend

dotnet user-secrets set "Smtp:Host" "smtp.gmail.com" --project .\src\Etamid.Api\Etamid.Api.csproj
dotnet user-secrets set "Smtp:Port" "587" --project .\src\Etamid.Api\Etamid.Api.csproj
dotnet user-secrets set "Smtp:SenderEmail" "you@example.com" --project .\src\Etamid.Api\Etamid.Api.csproj
dotnet user-secrets set "Smtp:AppPassword" "APP-PASSWORD-GOES-HERE" --project .\src\Etamid.Api\Etamid.Api.csproj
```

استبدل القيم التجريبية ببياناتك محليًا، ولا تضف App Password إلى ملف إعدادات متعقّب أو إلى commit. إذا لم يكن `UserSecretsId` مفعّلًا في مشروع API، نفّذ `dotnet user-secrets init --project .\src\Etamid.Api\Etamid.Api.csproj` مرة واحدة.

## تحديث قاعدة البيانات

بعد سحب التغييرات وضبط `ConnectionStrings:DefaultConnection` محليًا، شغّل من `D:\etamid\backend`:

```powershell
dotnet ef database update 20261028081144_EmailOtpVerification `
  --project .\src\Etamid.Infrastructure\Etamid.Infrastructure.csproj `
  --startup-project .\src\Etamid.Api\Etamid.Api.csproj `
  --context ApplicationDbContext
```

هذه migration تضيف جدول `EmailOtps` وفهارسه فقط، وتأتي بعد `20261027072703_Cycle3Deals`؛ لا تعيد إنشاء جداول الصفقات أو دفتر الأستاذ. تأكد أولًا أن قاعدة البيانات مطبّق عليها Cycle 3.

## مسارات الـAPI

- `POST /api/v1/auth/register/individual` و`POST /api/v1/auth/register/company`: التسجيل وإرسال رمز تفعيل إلى البريد.
- `POST /api/v1/auth/otp/verify` مع `{ "phone": "05…", "code": "123456" }`: التحقق من البريد وتفعيل الحساب.
- `POST /api/v1/auth/otp/resend` مع `{ "phone": "05…" }`: إعادة الإرسال؛ الرد العام لا يكشف وجود الحساب.
- `POST /api/v1/auth/password/forgot` مع `{ "phone": "05…" }`، ثم `POST /api/v1/auth/password/verify-otp` بالهاتف والرمز، ثم `POST /api/v1/auth/password/reset` بـ`resetToken` وكلمة المرور الجديدة وتأكيدها.

## الاختبارات

اختبارات التكامل تستخدم خدمة بريد وهمية تلتقط OTP داخل الاختبار، فلا تحتاج SMTP حقيقيًا. من مجلد `backend`:

```powershell
dotnet test .\Etamid.slnx
```
