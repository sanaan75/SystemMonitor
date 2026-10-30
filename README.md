# System Monitor Widget

ویجت ویندوز برای نمایش مصرف CPU، حافظه، بار سه‌بعدی GPU، حافظه‌ی اختصاصی GPU و نرخ دریافت/ارسال شبکه. نمایشگر دسکتاپ پس‌زمینه‌ی نیمه‌شفاف با ظاهر شیشه‌ای و نوار CPU سبز دارد و دکمه‌ی جداگانه‌ای در نوار وظیفه نشان نمی‌دهد.

## پروژه‌ها

- `SystemMonitor.WidgetProvider`: provider سی‌شارپ. برای ویرایش کد می‌توانید همین پروژه را در Rider باز کنید.
- `SystemMonitor.WidgetProvider.Package`: پروژه‌ی Windows Application Packaging Project شامل MSIX manifest، ثبت COM و ثبت ویجت در Widgets Board.
- `SystemMonitor.DesktopWidget`: نمایشگر مستقل دسکتاپ؛ پنجره‌ی بدون CMD با داده‌های زنده‌ی سیستم.

پروژه روی .NET 8 و Windows App SDK `2.6.4` تنظیم شده است. Provider در هر دو حالت Debug و Release به‌صورت `WinExe` اجرا می‌شود تا هنگام فعال شدن ویجت پنجره‌ی CMD باز نشود. خطاها از طریق `Trace` ثبت می‌شوند.

## اجرا و بررسی در ویندوز

1. در Visual Studio Community، workload توسعه‌ی Windows و قالب **Windows Application Packaging Project** را نصب کنید.
2. فایل `SystemMonitorWidget.sln` را باز کنید. Provider با فعال شدن ویجت توسط Widgets اجرا می‌شود و پس از حذف آخرین ویجت بسته می‌شود.
3. برای ظاهر شدن ویجت در `Windows + W`، پروژه‌ی `SystemMonitor.WidgetProvider.Package` را build و deploy کنید؛ build کردن provider به‌تنهایی آن را در Widgets Board ثبت نمی‌کند.
   این نوع ویجت در پنل Widgets ویندوز نمایش داده می‌شود؛ نمایشگر مستقل دسکتاپ در پروژه‌ی `SystemMonitor.DesktopWidget` قرار دارد.
4. برای دیدن نمایشگر روی دسکتاپ، در Solution Explorer روی `SystemMonitor.DesktopWidget` راست‌کلیک کنید و **Set as Startup Project** را بزنید، سپس `F5` را اجرا کنید. پنجره بدون CMD و بدون دکمه‌ی taskbar باز می‌شود و پشت پنجره‌های برنامه‌های دیگر می‌ماند. بستن پنجره آن را به بخش اعلان‌ها می‌فرستد؛ از منوی آیکن همان‌جا می‌توانید آن را نشان دهید، **Start with Windows** را روشن کنید، یا برنامه را کامل ببندید.
5. برای deploy محلی ویجت Win+W، از تنظیمات Signing پروژه‌ی پکیج یک test certificate بسازید. مقدار Publisher گواهی باید با Publisher فعلی در `Package.appxmanifest` یکی باشد.
6. ویجت Win+W در اندازه‌های Medium و Large ثبت شده است. کارت Medium چهار گروه را در دو ردیف فشرده می‌چیند و Large آن‌ها را عمودی نشان می‌دهد.

نمونه‌ی image picker در `SystemMonitor.WidgetProvider.Package/ProviderAssets/SystemMonitorPreview.png` است. در صورت تغییر آیکون یا تصویر، اسکریپت `Generate-Assets.ps1` را با PowerShell اجرا کنید.

## آماده‌سازی برای Microsoft Store

برای ارسال، در Partner Center یک محصول بسازید و Publisher دقیق حساب را جایگزین `Publisher="CN=System Monitor Widget Publisher"` در manifest کنید. سپس در Visual Studio پروژه‌ی Package را به محصول Store مرتبط و از مسیر **Create App Packages → Microsoft Store** فایل `.msixupload` را بسازید. نام package، نسخه و Publisher باید با محصول Partner Center مطابقت داشته باشند.

## منابع Microsoft

- [C# widget provider walkthrough](https://learn.microsoft.com/windows/apps/develop/widgets/implement-widget-provider-cs)
- [Widget provider manifest format](https://learn.microsoft.com/windows/apps/develop/widgets/widget-provider-manifest)
- [Windows App SDK stable releases](https://learn.microsoft.com/windows/apps/windows-app-sdk/release-channels)
