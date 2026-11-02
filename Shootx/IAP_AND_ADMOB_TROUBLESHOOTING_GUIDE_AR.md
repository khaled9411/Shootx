# دليل الصيانة والتشخيص الشامل لأنظمة المشتريات (IAP v5) وإعلانات المكافآت (AdMob)
## مشروع Shootx — محرك Unity 6.3 (منصتي Android و iOS)

---

## 1. الملخص التنفيذي والتحليل الجذري للأعطال (Executive Summary & Root Cause Analysis)

تم إجراء فحص شامل وهندسة إصلاح دقيقة للمشاكل التشغيلية والحرجة التي ظهرت في إصدار الإنتاج (Production) للعبة على متجري Google Play و Apple App Store:

### المشكلة الأولى: فشل تهيئة المتجر عند الشراء (`Store is not initialized`)
- **الأعراض**: عند الضغط على أي زر شراء لحزمة عملات أو وصول دائم للملتي بلاير أو إزالة الإعلانات، تظهر رسالة تفيد بأن المتجر غير مهيأ، ولا يُجدي الانتظار أو تكرار المحاولة نفعاً على كل من أندرويد و iOS.
- **السبب الجذري الأول (UGS Blocking Native Store Initialization)**:
  كان كود `IAPManager` القديم يستدعي `await UnityServices.InitializeAsync()` دون عزل داخل كتلة `try/catch`. إذا فشل اتصال خدمات Unity (UGS) بسبب غياب الإنترنت أو عدم تطابق معرّف المشروع السحابي في بناء الإنتاج، يتوقف التنفيذ تماماً ولا يتم الانتقال نهائياً إلى استدعاء `UnityIAPServices.StoreController()` أو محاولة الربط بمتجري Google Play Billing أو Apple StoreKit.
- **السبب الجذري الثاني (Lack of In-Flight Await & Retry Mechanism)**:
  كانت التهيئة تُستدعى لمرة واحدة فقط في `Start()` دون وجود أي آلية إعادة محاولة (Retry) عند الفشل. وعندما يضغط اللاعب على زر الشراء، كان الكود يقوم بمجرد فحص `IsInitialized` وينتظر 5 ثوانٍ ثم يفشل برسالة "Store not initialized" دون أن يطلق عملية تهيئة للمتجر إذا كانت قد فشلت سابقاً.
- **السبب الجذري الثالث (Single-Instance Destruction on Scene Switch)**:
  كانت الكائنات `IAPManager` و `GameAdManager` تشترك في معالج أحداث `OnactiveSceneChanged` يقوم بتدمير الكائن (`Destroy(gameObject)`) بمجرد الانتقال إلى المشهد ذي الفهرس 3 (`MultiplayerMenu`). عند العودة إلى القائمة الرئيسية، كانت تصبح الإشارة `Instance` إما فارغة (`null`) أو تشير إلى كائن مدمر، مما يكسر عمليات الشراء والإعلانات تماماً.
- **السبب الجذري الرابع على نظام iOS (Bundle Identifier Mismatch)**:
  أظهر فحص ملف `ProjectSettings.asset` أن حقل `applicationIdentifier.iPhone` كان مضبوطاً على المعرّف الافتراضي لقالب اليونيتي: `com.Unity-Technologies.com.unity.template.urp-blank` بدلاً من معرّف الحزمة الحقيقي `com.vgames01`. هذا التناقض جعل مكتبة StoreKit تفشل فورياً في مطابقة أي منتجات من App Store Connect في نسخ الـ iOS.

---

### المشكلة الثانية: تجمد شاشة تحميل إعلان المكافأة (Rewarded Ad Stuck Overlay)
- **الأعراض**: إعلان AdMob للمكافأة (الذي يمنح 24 ساعة دخول مؤقت للملتي بلاير) يمنح المكافأة أحياناً لكن شاشة التحميل (`Loading Overlay`) تظل عالقة ولا تختفي بعد إغلاق الإعلان. وفي بعض الأجهزة، يتجمد التحميل قبل ظهور الإعلان أو عند حدوث أي خطأ في التحميل.
- **السبب الجذري الأول (Premature Ad Destruction)**:
  في الكود القديم لـ `GameAdManager.ShowRewardedAd()`، كان الاستدعاء التالي مباشرة بعد أمر العرض `_rewardedAd.Show(...)` هو `LoadRewardedAd()`. هذا الإجراء كان يقوم فوراً بتدمير كائن الإعلان الحالي (`_rewardedAd.Destroy()`) أثناء بداية عرضه! هذا التدمير المبكر أدى إلى قطع جسر الاتصال الأصلي (Native Bridge/JNI) بين نظام التشغيل ومحرك Unity، مما تسبب في ضياع أحداث الإغلاق والانتقال.
- **السبب الجذري الثاني (Missing Fullscreen Content Callbacks)**:
  لم يكن الكود القديم يشترك نهائياً في أحداث المحتوى بملء الشاشة الخاصة بـ Google Mobile Ads:
  - `OnAdFullScreenContentClosed`
  - `OnAdFullScreenContentFailed`
  وكان يعتمد فقط على رد النداء عند ربح الجائزة `userRewardEarnedCallback`. وبالتالي، إذا قام اللاعب بمشاهدة الإعلان وإغلاقه، أو تخطيه، أو حدث فشل أثناء العرض، لا يتم استدعاء أي كود لإنهاء التحميل، فتظل الشاشة السوداء / شاشة الانتظار معروضة للأبد.
- **السبب الجذري الثالث (Decoupled UI Dismissal & Missing Watchdog)**:
  كان المودال `MultiplayerAccessGateModal` يرفع علم `_isAdLoading = true` ويظهر شاشة التحميل دون وجود مؤقت أمان (`Watchdog Timer`) يضمن استعادة التحكم وإخفاء نافذة التحميل إذا لم تستجب شبكة الإعلانات أو حدث انقطاع في الاتصال.

---

## 2. مصفوفة إصدارات الحزم والاعتماديات (Package Versions Matrix)

تم التحقق من الإصدارات المثبتة فعلياً في المشروع عبر فحص `Packages/manifest.json` و `Packages/packages-lock.json` وخصائص الـ DLLs المترجمة:

| الحزمة / الاعتمادية | الإصدار الدقيق (Exact Version) | ملاحظات التوافق |
| :--- | :--- | :--- |
| **Unity In-App Purchasing** | `com.unity.purchasing: 5.4.3` | يعتمد معمارية IAP v5 الحديثة المبنية على `UnityIAPServices` و `StoreController` والعمليات اللاتزامنية (`Task/Async`). |
| **Google Mobile Ads Unity SDK** | `GoogleMobileAds: 11.2.0` | حزم DLL مسبقة البناء في `Assets/GoogleMobileAds/`. متوافقة بالكامل مع أحدث واجهات Google Mobile Ads v11+. |
| **Unity Services Core** | `com.unity.services.core: 1.18.0` | الأساس لخدمات Unity السحابية. تم عزلها بحيث لا يعطل فشلها المتاجر الأصلية. |
| **DOTween Pro** | `Demigiant DOTween v1.0` | يُستخدم في حركات الـ UI والمودال مع ضمان التنظيف عند الإلغاء. |
| **Unity Engine Target** | `Unity 6000.3.11f1 (Unity 6.3)` | بيئة العمل الأساسية. |

---

## 3. المعمارية المحدثة لنظام المشتريات (Unity IAP v5 Architecture)

### 3.1 آلة الحالات المركزية (State Machine)
تم تزويد `IAPManager` بآلة حالات موحدة تمنع التضارب وتتيح مراقبة دقيقة لمرحلة الاتصال:
```csharp
public enum IAPInitializationState
{
    NotInitialized,  // لم تبدأ التهيئة بعد
    Initializing,    // جاري الاتصال والتسجيل
    StoreConnected,  // تم الاتصال بنجاح مع المتجر الأصلي (Google Play / StoreKit)
    ProductsFetched, // تم جلب الكتالوج وتأكيد توفر المنتجات
    Ready,           // المتجر جاهز تماماً لتنفيذ عمليات الشراء
    Failed,          // فشلت التهيئة (مع حفظ رسالة الخطأ)
    Unavailable      // المتجر غير مدعوم على هذه المنصة أو معطل أمنياً
}
```

### 3.2 دورة حياة الدالة الموحدة `EnsureInitializedAsync(bool forceRetry = false)`
1. **الفحص السريع (Fast Return)**: إذا كانت الحالة `Ready`، تعود الدالة فوراً بنجاح (`true`).
2. **منع التكرار (In-Flight Task Await)**: إذا كانت هناك عملية تهيئة جارية بالفعل من قبل زر أو واجهة أخرى (`Initializing`)، تنتظر جميع الاستدعاءات نفس الـ `Task` القائم دون إطلاق مهام متوازية تسبب تضارب المتجر.
3. **عزل UGS (Resilient UGS Init)**:
   ```csharp
   try
   {
       if (UnityServices.State != ServicesInitializationState.Initialized)
       {
           await UnityServices.InitializeAsync();
       }
   }
   catch (Exception ugsEx)
   {
       Debug.LogWarning($"[IAP] UGS initialization optional bypass: {ugsEx.Message}");
       // نستمر في تهيئة المتجر الأصلي حتى وإن تعطلت خدمات Unity السحابية!
   }
   ```
4. **تسجيل الكتالوج وجلب المنتجات (Catalog Configuration)**:
   يتم تسجيل كافة المنتجات الـ 12 المعتمدة مع معرّفاتها ونوعها (`Consumable` أو `NonConsumable`).
5. **الاتصال وتأكيد الجاهزية**:
   يتم استدعاء `_storeController.Connect()`، والاستماع إلى `OnProductsFetched` وتحديث قاموس التوفر `_productAvailability` لكل منتج على حدة قبل الانتقال إلى حالة `Ready`.

### 3.3 حماية مشهد الملتي بلاير وعدم تدمير الكائن
تمت إزالة كود تدمير الكائنات عند فتح مشهد الملتي بلاير (`CurrentScene.buildIndex == 3`) في كل من `IAPManager` و `GameAdManager`، مع الإبقاء على `DontDestroyOnLoad(gameObject)` لضمان استمرارية الـ Singleton طوال جلسة اللعب بدون تسريبات ذاكرة وبدون إعادة إنشاء غير ضرورية.

### 3.4 معالجة الشراء الآمن ومنع التكرار (Idempotency)
- قبل منح العملات أو الامتياز، يتم فحص المعرّف الفريد للمعاملة:
  `GameDataManager.Instance.IsTransactionProcessed(transactionId)`
- إذا سُجلت المعاملة سابقاً، يتم تأكيدها لدى المتجر (`ConfirmPendingPurchase`) دون تكرار منح الجائزة لمنع عمليات الاحتيال أو التكرار غير المقصود.
- في دالة استعادة المشتريات `RestorePurchasesAsync`:
  يتم حصر الاستعادة على المنتجات الدائمة غير القابلة للاستهلاك (`NonConsumable`) مثل `online_access` و `remove_ads` وتفعيلها، بينما لا يتم نهائياً إعادة منح حزم العملات المستهلكة (`Consumable`) تماشياً مع سياسات Google Play و Apple App Store.

---

## 4. جدول المنتجات الـ 12 المعتمدة (Product Catalog Configuration)

تتطابق المعرّفات المسجلة كودياً في `IAPManager.OfficialProducts` بدقة مع المعرّفات المرفوعة على Google Play Console و App Store Connect:

| معرّف المنتج (Product ID) | نوع المنتج (Product Type) | الوصف والمكافأة | الفئة السعرية التقريبية (Tier) |
| :--- | :--- | :--- | :--- |
| `coins_200` | Consumable (مستهلك) | 200 عملة ذهبية | Tier 1 ($0.99) |
| `coins_500` | Consumable (مستهلك) | 500 عملة ذهبية | Tier 2 ($1.99) |
| `coins_1200` | Consumable (مستهلك) | 1,200 عملة ذهبية | Tier 5 ($4.99) |
| `coins_2500` | Consumable (مستهلك) | 2,500 عملة ذهبية | Tier 10 ($9.99) |
| `online_access` | NonConsumable (دائم) | وصول دائم وغير محدود لطور اللعب الجماعي | Tier 5 ($4.99) |
| `remove_ads` | NonConsumable (دائم) | إزالة الإعلانات البينية والبانر نهائياً | Tier 3 ($2.99) |
| `package_01` | Consumable (حزمة) | حزمة المبتدئين (عملات ومكافأة) | Tier 2 ($1.99) |
| `package_02` | Consumable (حزمة) | حزمة المقاتل | Tier 4 ($3.99) |
| `package_03` | Consumable (حزمة) | حزمة النخبة | Tier 7 ($6.99) |
| `package_04` | Consumable (حزمة) | حزمة البطل | Tier 10 ($9.99) |
| `package_05` | Consumable (حزمة) | الحزمة الأسطورية | Tier 15 ($14.99) |
| `package_06` | Consumable (حزمة) | الحزمة الملكية القصوى | Tier 20 ($19.99) |

---

## 5. دليل تشخيص أخطاء المشتريات على أندرويد و iOS (Diagnostic Guide)

### 5.1 أوامر فحص Android Logcat
لفرز سجلات الشراء وتشخيص اتصال متجر Google Play بدقة من موجه الأوامر (Terminal / PowerShell):

```bash
# تتبع جميع السجلات الصادرة من IAPManager
adb logcat -s Unity | grep -E "\[IAP\]|UnityIAP|DEBUG_STORE"

# تتبع استجابات مكتبة الفوترة Google Play Billing الأصلية
adb logcat | grep -iE "GooglePlayBilling|BillingClient|PurchaseActivity"
```

#### جدول تشخيص أكواد أخطاء Google Play Billing الشائعة:
- **`BILLING_RESPONSE_RESULT_ITEM_UNAVAILABLE` (الكود 4)**:
  - السبب: معرّف المنتج غير مفعل (Inactive) في Google Play Console، أو أن حزمة التطبيق (AAB) المرفوعة على المسار الداخلي لم تُعتمد بعد.
- **`BILLING_RESPONSE_RESULT_DEVELOPER_ERROR` (الكود 5)**:
  - السبب: خطأ في إعدادات التطبيق، مثل استخدام معرّف غير صحيح أو عدم توقيع التطبيق بنفس مفتاح الرفع (Keystore).
- **`BILLING_RESPONSE_RESULT_USER_CANCELED` (الكود 1)**:
  - السبب: قام اللاعب بإغلاق نافذة الشراء أو إلغاء الدفع. يتم التعامل معه بنعومة دون إظهار أخطاء فادحة.
- **`BILLING_RESPONSE_RESULT_ITEM_ALREADY_OWNED` (الكود 7)**:
  - السبب: محاولة شراء منتج `NonConsumable` يمتلكه اللاعب بالفعل. يقوم الكود المحدث بالتعرف عليه وتأكيد ملكيته محلياً فوراً.

### 5.2 تشخيص أخطاء Apple StoreKit على نظام iOS
عند الفحص عبر نافذة Xcode Console، يتم البحث عن وسوم `StoreKit` و `UnityIAP`:

#### جدول تشخيص أكواد أخطاء StoreKit الشائعة:
- **`SKErrorPaymentCancelled`**: إلغاء العملية من قبل المستخدم.
- **`SKErrorPaymentNotAllowed`**: الجهاز مقيد بقيود الرقابة الأبوية أو أن حساب Apple ID غير مخول لإجراء مدفوعات داخل التطبيقات.
- **`SKErrorPaymentInvalid` / `Invalid Product ID`**:
  - السبب الشائع: عدم تطابق `Bundle Identifier` بين المشروع و App Store Connect (تم حله بتحديث `applicationIdentifier.iPhone` إلى `com.vgames01`).
  - أو عدم توقيع اتفاقية "Paid Applications Agreement" في حساب المطور.

---

## 6. المعمارية المحدثة لإعلانات المكافآت AdMob وحل تجمد شاشة التحميل

### 6.1 آلة حالات إعلان المكافأة (RewardedAdState)
```csharp
public enum RewardedAdState
{
    Idle,    // خامل، لا توجد عمليات جارية
    Loading, // جاري طلب الإعلان من خوادم AdMob
    Ready,   // الإعلان جاهز ومخزن في الذاكرة ومستعد للعرض
    Showing, // الإعلان يُعرض حالياً بملء الشاشة أمام اللاعب
    Closed,  // انتهى الإعلان وتم إغلاقه
    Failed   // فشل التحميل أو العرض
}
```

### 6.2 دورة الحياة المحصنة وآلية الإغلاق المضمون (Guaranteed Cleanup)
1. **معالجة العرض المنفصلة**:
   عند استدعاء `ShowRewardedAd()`:
   - يتم التأكد من أن `_rewardedAd.CanShowAd()`.
   - يتم توليد معرّف جلسة فريد `operationId = Guid.NewGuid().ToString("N")`.
   - يتم الاشتراك في **كافة أحداث المحتوى بملء الشاشة**:
     - `OnAdFullScreenContentOpened`: يطلق حدث `GameAdManager.OnRewardedAdOpened` لتنبيه الواجهات بإخفاء مؤشرات التحميل لأن الإعلان يغطي الشاشة الآن.
     - `OnAdFullScreenContentClosed`: يستدعي `FinalizeRewardedAdOperation` لتدمير كائن الإعلان القديم بأمان، وإطلاق `OnRewardedAdClosed`، ثم استدعاء رد النداء الخاص بالواجهة لمنح المكافأة وتفريغ شاشات التحميل، وتجهيز إعلان جديد في الخلفية.
     - `OnAdFullScreenContentFailed`: يستدعي `FinalizeRewardedAdOperation` بنتيجة فشل لضمان إخفاء أي شاشة تحميل فوراً.
2. **مؤقتات الأمان الصارمة (Watchdog Coroutines)**:
   - **مؤقت أمان التحميل (10 ثوانٍ)**: إذا لم يرد خادم AdMob خلال 10 ثوانٍ من طلب الإعلان، يتم إلغاء التحميل واستدعاء رد النداء بـ `false` لمنع تعليق واجهة اللعبة.
   - **مؤقت أمان العرض (120 ثانية)**: في حال حدوث عطل غير متوقع في نظام التشغيل منع وصول حدث الإغلاق، يتدخل المؤقت لتحرير الواجهة تلقائياً.
   - **مؤقت أمان المودال (`MultiplayerAccessGateModal` 15 ثانية)**: يراقب المودال بنفسه مدة انتظار الإعلان، ويقوم بإخفاء `Loading Overlay` وإعادة تفعيل الأزرار تلقائياً في حال تأخر الاستجابة.

---

## 7. آلية إدارة صلاحية الملتي بلاير وحمايتها (Multiplayer Access Entitlements)

### 7.1 حساب وتخزين وقت الـ 24 ساعة
- يتم حساب انتهاء صلاحية الوصول المؤقت بالاعتماد الصارم على التوقيت العالمي المنسق (`UTC`):
  ```csharp
  DateTime expirationTime = DateTime.UtcNow.AddHours(24);
  GameDataManager.Instance.SetTemporaryMultiplayerAccess(expirationTime);
  ```
- يتم حفظ التاريخ كـ `ISO 8601 string` في `PlayerPrefs` المشفرة محلياً لضمان عدم تأثره بتغيير المناطق الزمنية (Time Zones).

### 7.2 قاعدة حماية الجلسة النشطة (Active Session Non-Interruption)
- **القاعدة الذهبية**: "إذا انتهت الـ 24 ساعة أثناء وجود اللاعب داخل غرفة لعب جماعي نشطة، **لا يتم طرده نهائياً**".
- يتم فحص الصلاحية فقط عند:
  1. محاولة الدخول إلى ردهة اللعب الجماعي (`MultiplayerMenu`).
  2. إنشاء أو الانضمام إلى غرفة جديدة.
- أثناء وجود اللاعب داخل مشهد اللعب القتالي (`MultiplayerGameplay`)، لا تتدخل أي مراقبة زمنية لقطع الاتصال أو تعطيل المباراة.

---

## 8. خطوات التحقق داخل محرر Unity (Inspector & Scene Verification)

1. افتح المشهد الأولي للعبة (مشهد البداية / `Main`).
2. تأكد من وجود كائن الـ GameObject الحامل لسكربت `IAPManager` وكائن `GameAdManager`.
3. افحص مكون `IAPManager` في الـ Inspector:
   - تأكد من تفعيل خاصية `Initialize On Start`.
   - لا تضع أكثر من كائن واحد في المشهد (الكود يقوم تلقائياً بحذف أي نسخ مكررة وتطبيق `DontDestroyOnLoad`).
4. افحص مكون `GameAdManager`:
   - تأكد من تعبئة `Rewarded Id` لمعرّف أندرويد: `ca-app-pub-8291738977037870/9932310465`.
   - تأكد من وجود معرّفات iOS في حال الرغبة في اختبار إعلانات حقيقية على أجهزة آبل، أو تركها لتأخذ معرّفات الاختبار الافتراضية.
5. افحص نافذة `MultiplayerAccessGateModal`:
   - تأكد من ربط حقل `loadingOverlay` بكائن الـ UI المناسب (Panel أو CanvasGroup).
   - تأكد من أن مكون `CanvasGroup` يحتوي على `blocksRaycasts` لتعطيل النقر أثناء التحميل وإعادة تفعيله فور انتهائه.

---

## 9. قائمة مراجعة متطلبات المتاجر الرسمية قبل إطلاق التحديث (Pre-Launch Checklist)

### 9.1 متجر Google Play Console
- [ ] التأكد من رفع نسخة الإنتاج كـ Android App Bundle (`.aab`) موقعة بمفتاح الإصدار النهائي (`Release Keystore`).
- [ ] التأكد من إضافة صلاحية الفوترة في الـ `AndroidManifest.xml`:
  `<uses-permission android:name="com.android.vending.BILLING" />`
- [ ] في قسم "منتجات داخل التطبيق" (In-app products): التأكد من تفعيل جميع المنتجات الـ 12 وحالتها **نشطة (Active)**.
- [ ] عند الاختبار على حساب تجريبي: التأكد من إضافة البريد الإلكتروني للمختبر في قائمة "License Testing" في قسم إعدادات المطور، وضبط نوع الاستجابة على `RESPOND_NORMALLY`.
- [ ] تفعيل مسار الاختبار المغلق (Closed Testing Track) واعتماد النسخة من قِبل Google قبل بدء الشراء المباشر.

### 9.2 متجر Apple App Store Connect
- [ ] التأكد من تطابق الـ Bundle ID في المشروع: `com.vgames01`.
- [ ] توقيع وتفعيل اتفاقية التطبيقات المدفوعة (Paid Applications Agreement) في حساب المطور وتعبئة البيانات البنكية والضريبية.
- [ ] في قسم "In-App Purchases": التأكد من أن جميع المنتجات الـ 12 بحالة **Ready to Submit** أو **Approved**.
- [ ] اختبار المشتريات على جهاز حقيقي باستخدام حساب **Sandbox Tester** تم إنشاؤه في App Store Connect (وليس حساب Apple ID الشخصي الأساسي).

### 9.3 لوحة تحكم Google AdMob
- [ ] التأكد من اكتمال إعداد ملف `app-ads.txt` والتحقق منه على النطاق الخاص بك في AdMob.
- [ ] التأكد من تفعيل الحساب البنكي ومعلومات الدفع لتجنب إيقاف تقديم الإعلانات (Ad Serving Limits).
- [ ] التحقق من أن حدود الحد الأدنى للأرباح (eCPM Floors) ليست مرتفعة للغاية مما يمنع جلب الإعلانات في بعض الدول.

---

## 10. أوامر سريعة لتشخيص المشاكل عبر سطر الأوامر (Command-Line Cheat Sheet)

```powershell
# مسح سجلات أندرويد وبدء مراقبة حية للمشتريات والإعلانات
adb logcat -c
adb logcat -v time | Select-String "\[IAP\]", "\[AdMob\]", "MultiplayerAccessGateModal"

# تتبع استعادة المشتريات على أندرويد
adb logcat | Select-String "RestorePurchases", "ConfirmPendingPurchase"

# التحقق من تثبيت شهادات ومفتاح التوقيع على جهاز الاختبار
adb shell pm dump com.vgames01 | Select-String "signatures"
```

---
*تم إعداد هذا التقرير الفني المعتمد ليكون المرجع الشامل لفريق التطوير وإدارة العمليات في مشروع Shootx.*

