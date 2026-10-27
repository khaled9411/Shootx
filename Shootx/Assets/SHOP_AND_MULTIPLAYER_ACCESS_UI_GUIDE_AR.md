# دليل إعادة تصميم وتكامل واجهتي المتجر (ShopPopup) وبوابة الوصول للعب الجماعي (Multiplayer Access)
# Redesigned ShopPopup & Multiplayer Access UI Technical Documentation

تم إعداد هذا التوثيق الفني الشامل باللغة العربية ليوثق بدقة واحترافية كافة جوانب إعادة التصميم الكاملة لواجهتي **ShopPopup** و **Multiplayer Access** داخل مشروع **Shootx** على محرك **Unity 6 (6000.3.11f1)** بالاعتماد على **DOTween Pro** و **TextMeshPro** و **Unity IAP v5**.

---

## فهرس المحتويات
1. [الصفحات المعدلة ومواقعها في المشروع](#1-الصفحات-المعدلة-ومواقعها-في-المشروع)
2. [العناصر والأنماط البصرية المستوحاة من لوحة المتصدرين (Leaderboard)](#2-العناصر-والأنماط-البصرية-المستوحاة-من-لوحة-المتصدرين)
3. [الهيكلية التفصيلية لنافذة المتجر المعاد تصميمها (ShopPopup Hierarchy)](#3-الهيكلية-التفصيلية-لنافذة-المتجر-المعاد-تصميمها)
4. [معرفات منتجات الحزم الستة وحزم العملات الأربعة (Product IDs)](#4-معرفات-منتجات-الحزم-الستة-وحزم-العملات-الأربعة)
5. [عرض الأسعار المحلية وحالات الشراء والملكية (Localized Pricing & States)](#5-عرض-الأسعار-المحلية-وحالات-الشراء-والملكية)
6. [الهيكلية التفصيلية لنافذة الوصول للعب الجماعي (Multiplayer Access Hierarchy)](#6-الهيكلية-التفصيلية-لنافذة-الوصول-للعب-الجماعي)
7. [ربط خيارات الوصول الثلاثة (Online Access, Remove Ads, Rewarded Ad)](#7-ربط-خيارات-الوصول-الثلاثة)
8. [إعدادات CanvasScaler ومرجع الدقة (CanvasScaler & Resolution)](#8-إعدادات-canvasscaler-ومرجع-الدقة)
9. [قيم المطابقة والنسب المدعومة (Width/Height Matching)](#9-قيم-المطابقة-والنسب-المدعومة)
10. [تعديلات المحاذاة والارتكاز (Anchors & Pivots)](#10-تعديلات-المحاذاة-والارتكاز)
11. [نظام معالجة المساحات الآمنة للشاشات (Safe-Area Setup)](#11-نظام-معالجة-المساحات-الآمنة-للشاشات)
12. [مكونات التخطيط التجاوبي للهواتف والأجهزة اللوحية (Responsive Layout Components)](#12-مكونات-التخطيط-التجاوبي-للهواتف-والأجهزة-اللوحية)
13. [حركات DOTween Pro وإعداداتها القابلة للتخصيص (DOTween Pro Animations)](#13-حركات-dotween-pro-وإعداداتها-القابلة-للتخصيص)
14. [مراجع المفتش والربط التلقائي واليدوي (Inspector References)](#14-مراجع-المفتش-والربط-التلقائي-واليدوي)
15. [دليل اختبار التجاوب عبر أبعاد الشاشات المختلفة (Testing Across Resolutions)](#15-دليل-اختبار-التجاوب-عبر-أبعاد-الشاشات-المختلفة)
16. [تأكيد نطاق العمل الصارم وعدم المساس بالأنظمة الأخرى (Strict Scope Confirmation)](#16-تأكيد-نطاق-العمل-الصارم-وعدم-المساس-بالأنظمة-الأخرى)

---

## 1. الصفحات المعدلة ومواقعها في المشروع

تم حصر كافة التعديلات والتطوير حصرياً داخل الواجهتين المطلوبتين:

1. **نافذة المتجر الرقمي (ShopPopup):**
   - **الكائن في المشهد:** `Canvas > SHOP POPUP` داخل المشهد الرئيسي `Assets/Scenes/Main.unity`.
   - **السكربت المتحكم:** `Assets/Scripts/UI/ShopPopupController.cs`.
   - **المكونات المرتبطة:** `CanvasGroup`, `RectTransform`, `Image (window.png)`, أنظمة التبويب والقوائم التمريرية.

2. **نافذة بوابة الوصول للعب الجماعي (Multiplayer Access Gate Modal):**
   - **الكائن في المشهد:** `Canvas > MULTIPLAYER ACCESS MODAL` داخل المشهد الرئيسي `Assets/Scenes/Main.unity`.
   - **السكربت المتحكم:** `Assets/Scripts/Store/MultiplayerAccessGateModal.cs`.
   - **المكونات المرتبطة:** `CanvasGroup`, `RectTransform`, `ScrollRect`, خيارات الوصول الثلاثة وبانر الحالة الحية.

3. **أداة الأتمتة والتحقق في المحرر (Editor Tooling):**
   - `Assets/Scripts/Store/Editor/ShopAndMultiplayerAccessUIEditor.cs`: أداة لتوليد وتحقق ومطابقة واجهات المشهد آلياً عبر القائمة `Tools > Shootx UI`.

---

## 2. العناصر والأنماط البصرية المستوحاة من لوحة المتصدرين

تم فحص كائنات ومكونات لوحة المتصدرين (`LEADERBOARD POPUP` و `PlayerRankCard.prefab`) واعتماد نفس المعايير الجمالية والهندسية لتوحيد الهوية البصرية:

1. **الخطوط والطباعة (Typography Hierarchy):**
   - **الخط الأساسي للعناوين:** `Orbitron-Regular SDF` (`Assets/ShootX/Fonts/Orbitron-Regular SDF.asset`, GUID: `f8d3b8449dc0d8a4c865342896312a7f`). يعطي مظهراً مستقبلياً أنيقاً يطابق هوية اللعبة.
   - **الخط المساعد للأرقام والأسعار:** `LilitaOne - Regular SDF` (`Assets/GUIPackCartoon/Demo/Fonts/LilitaOne - Regular SDF.asset`, GUID: `015573da50ed04390be9f20287a541d6`). يمنح أرقام العملات والأسعار وضوحاً فائقاً وقراءة سريعة.

2. **إطار النوافذ والخلفيات (Window Frames & Panels):**
   - استخدام صورة الإطار المعدني المستقبلي الأصلي: `Assets/ShootX UI/Leaderboard/window.png` (GUID: `fd46886aadb31af4684f930bed15930b`).
   - خلفية تظليل المشهد (Dim Backdrop): لون غامق ناعم `#070A12` بنسبة شفافية `88%` لمنح النوافذ عمقاً بصرياً وعزل التركيز على المحتوى.

3. **إطارات البطاقات (Card Frames):**
   - بطاقات عادية: `Assets/ShootX UI/Leaderboard/card_normal.png` (GUID: `a95168fc8f446444295c021aa7d08286`).
   - بطاقات مميزة وذهبية: `Assets/ShootX UI/Leaderboard/card_gold.png` (GUID: `43a9ee11c63f28541b8ee4b65ee6688a`).
   - بطاقات فضية: `Assets/ShootX UI/Leaderboard/card_silver.png` (GUID: `312f6462d81366741a6fefc422dd58f3`).

4. **الأزرار والأيقونات (Buttons & Icons):**
   - أزرار الشراء الخضراء عالية التباين: `Assets/ShootX UI/9pause menu screen/Green button.png` (GUID: `8a1edb9c48b9bfe40b93d3509ae2221b`).
   - أزرار التفاعل الزرقاء: `Assets/ShootX UI/9pause menu screen/blue button.png` (GUID: `2662bcb0703c8cc44b2bc75b0b3d9c00`).
   - أيقونة الإغلاق: `Assets/ShootX UI/3Store screen/Close Icon.png` (GUID: `877241b5d30c3f044868a11692d784c0`).
   - أيقونة العملة الذهبية المميزة (Hard Currency): `Assets/ShootX UI/2Main menu HUD/Golden Coin.png` (GUID: `edbf32a1902b5a2448d268281a43c2e4`).
   - أيقونة العملة الفضية العادية (Soft Currency): `Assets/ShootX UI/2Main menu HUD/Silver Coin.png` (GUID: `0081a54586e419545b812f0791e1f6c4`).
   - أيقونة الفيديو لإعلانات المكافأة: `Assets/ShootX UI/9pause menu screen/Video Icon.png` (GUID: `629e06f3f18f5e34a9bc170e090ab68d`).
   - أيقونة اللعب الجماعي: `Assets/ShootX UI/2Main menu HUD/Online Icon.png` (GUID: `202c4c50094f5b741a6dd0fc14a29887`).
   - شارة التأكيد والتملك: `Assets/ShootX UI/2Main menu HUD/CHECK.png` (GUID: `69f5d932b8a79824a8b92fe2fcf8d9f8`).

5. **لوحة الألوان (Harmonious Sci-Fi Palette):**
   - خلفية البطاقات: كحلي غامق `#0D1424`.
   - الحدود والإطارات: سماوي تقني ناعم `#2A3B5D`.
   - ألوان الإبراز (Accents): سيان متوهج `#00E5FF` وذهبي ملكي `#FFD700`.
   - ألوان النجاح والشراء: أخضر نيون زمردي `#00E676`.
   - ألوان التنبيه والخطأ: أحمر ساطع `#FF4545`.

---

## 3. الهيكلية التفصيلية لنافذة المتجر المعاد تصميمها (ShopPopup)

```
SHOP POPUP (CanvasGroup, ShopPopupController)
├── DimBackground (Image - 88% Black Dim)
└── PopupCard (RectTransform [0.035, 0.04] to [0.965, 0.95], Image: window.png)
    ├── Header
    │   ├── Title (TextMeshProUGUI: "PREMIUM STORE", Orbitron Bold, Cyan Glow)
    │   ├── CloseButton (Button, Image: Close Icon.png, Scale Pulse Tween)
    │   └── CurrencyRibbon (HorizontalLayoutGroup)
    │       ├── SoftBadge (Icon: Silver Coin, Text: Soft Currency Balance)
    │       └── HardBadge (Icon: Golden Coin, Text: Hard Currency Balance)
    ├── TabBar (HorizontalLayoutGroup - Responsive Pills)
    │   ├── BundlesTab (Button, Image: card_normal.png, Label: "SPECIAL BUNDLES")
    │   ├── CoinsTab (Button, Image: card_normal.png, Label: "HARD COINS")
    │   └── AccessTab (Button, Image: card_normal.png, Label: "ONLINE PASSES")
    ├── PanelsContainer (RectTransform)
    │   ├── BundlesPanel (CanvasGroup)
    │   │   ├── BundleRibbon (HorizontalLayoutGroup - 6 Bundle Selector Buttons)
    │   │   └── BundleShowcaseBox (Image: card_gold.png)
    │   │       ├── Badge (TextMeshProUGUI: [LEGENDARY], [STARTER], etc.)
    │   │       ├── Title (TextMeshProUGUI: Bundle Name)
    │   │       ├── Desc (TextMeshProUGUI: Bundle Description)
    │   │       ├── ItemsScrollView (ScrollRect - Elastic Horizontal)
    │   │       │   └── Content (HorizontalLayoutGroup)
    │   │       │       └── [Spawned Bundle Item Cards with [OWNED] vs [NEW] badges]
    │   │       └── BuyBundleBtn (Button, Image: Green button.png, Text: Localized Price)
    │   ├── CoinsPanel (CanvasGroup)
    │   │   └── CoinsScrollView (ScrollRect - Elastic Vertical)
    │   │       └── Content (VerticalLayoutGroup)
    │   │           ├── Card_coins_200 (Coin Icon, "+200 HARD COINS", Price Button)
    │   │           ├── Card_coins_500 (Coin Icon, "+500 HARD COINS", [POPULAR] Badge, Price Button)
    │   │           ├── Card_coins_1200 (Coin Icon, "+1,200 HARD COINS", [GREAT VALUE] Badge, Price Button)
    │   │           └── Card_coins_2500 (Coin Icon, "+2,500 HARD COINS", [BEST VALUE] Badge, Price Button)
    │   └── AccessPanel (CanvasGroup)
    │       └── AccessScrollView (ScrollRect - Elastic Vertical)
    │           └── Content (VerticalLayoutGroup)
    │               ├── Pass_online_access (Online Icon, Title, Description, Buy Button)
    │               └── Pass_remove_ads (Check Icon, Title, Description, Buy Button)
    ├── FooterBar
    │   ├── RestoreBtn (Button: "RESTORE PURCHASES")
    │   └── Feedback (TextMeshProUGUI: Status & Success Toasts)
    └── LoadingOverlay (CanvasGroup, Dark Backdrop + "Connecting to store..." Spinner Text)
```

---

## 4. معرفات منتجات الحزم الستة وحزم العملات الأربعة

### أ. الحزم الستة (Bundles - Non-Consumable):
| اسم الحزمة | معرف المنتج (Product ID) | التصنيف الترويجي | المحتويات الفعلية المسجلة في المشروع |
|---|---|---|---|
| **Starter Pack** | `package_01` | `STARTER` | مسدس ريفولفر + مسدس رشاش 1 + زي الوحش + زي المهرج |
| **Advanced Pack** | `package_02` | `POPULAR` | مسدس رشاش 2 + بندقية صغيرة + رشاش خفيف + زي الحارس + زي المتخفي |
| **Elite Pack** | `package_03` | `ELITE` | بندقية قتالية + سلاح الإبر + قناصة + زي درويد + زي الفارس + هيئة الوحش |
| **Character Pack** | `package_04` | `CHARACTERS` | 5 أزياء ومظاهر نادرة (بالادين، روبوت، مستذئب، درع الخوذة، روبوت مرعب) |
| **Legendary Pack** | `package_05` | `LEGENDARY` | بندقية ليزر + 3 بنادق بلازما + قناصة بلازما + أزياء أسطورية |
| **Legendary Weapons**| `package_06` | `ARSENAL` | ترسانة الأسلحة الأسطورية الخمسة الكاملة بدون أزياء |

### ب. حزم العملة الذهبية المميزة (Hard Currency - Consumable):
| الحزمة | المعرف (Product ID) | كمية العملة الذهبية الممنوحة | السعر الافتراضي المرجعي |
|---|---|---:|---|
| **200 Coins** | `coins_200` | **+200** عملة ذهبية | $0.99 |
| **500 Coins** | `coins_500` | **+500** عملة ذهبية (الأكثر طلباً) | $1.99 |
| **1,200 Coins** | `coins_1200` | **+1,200** عملة ذهبية (توفير ممتاز) | $3.99 |
| **2,500 Coins** | `coins_2500` | **+2,500** عملة ذهبية (أعلى قيمة) | $7.99 |

### ج. رخص اللعب الجماعي (Multiplayer Passes - Non-Consumable):
| المنتج | المعرف (Product ID) | المزايا الممنوحة | السعر الافتراضي المرجعي |
|---|---|---|---|
| **Online Access Pass** | `online_access` | وصول دائم غير محدود لطور اللعب الجماعي | $1.99 |
| **Remove Ads + Access** | `remove_ads` | إزالة الإعلانات الإجبارية نهائياً + وصول دائم للعب الجماعي | $3.99 |

---

## 5. عرض الأسعار المحلية وحالات الشراء والملكية

1. **الأسعار المحلية الحقيقية (Real Localized Pricing):**
   - تُقرأ الأسعار مباشرة من المتجر الرسمي (Google Play / App Store) عبر `IAPManager.Instance.GetLocalizedPrice(productId)`.
   - لا توجد أي أسعار ثابتة أو عشوائية؛ إذا لم يكن المتجر مهيأ بعد، يُعرض السعر الافتراضي كـ Fallback ريثما تكتمل إشارة `OnInitializedEvent`.
2. **فصل العملتين (Currency Distinction):**
   - تظهر العملة الذهبية (Hard Coins) بأيقونة الذهب المستقلة مع نص واضح يوضح أنها عملة المتجر المدفوعة.
   - رصيد العملة العادية (Soft Coins) مفصول تماماً بأيقونة الفضة، ويتم تحديثهما فورياً مع أي تغيير عبر الأحداث الثابتة `OnCoinsChanged` و `OnHardCoinsChanged`.
3. **محتويات الحزم الفعلية وحالة العناصر:**
   - عند اختيار أي حزمة، تُقرأ محتوياتها الحقيقية من كائن الأصل `StoreCatalogDatabase.Instance.GetBundleByProductId(id)`.
   - يتم فحص ملكية كل عنصر على حدة:
     - إذا كان العنصر مملوكاً: يظهر باللون الأخضر `<color=#00FF88>[OWNED]</color>`.
     - إذا كان العنصر جديداً: يظهر باللون الذهبي `<color=#FFD700>[NEW]</color>`.
4. **حالة التملك الكامل (Bundle Ownership):**
   - إذا امتلك اللاعب كافة عناصر الحزمة، يتحول زر الشراء تلقائياً إلى نص أخضر: `ALL ITEMS OWNED` ويتم تعطيل خاصية التفاعل (`interactable = false`) لمنع الشراء غير الضروري.
5. **معاملات الشراء الآمنة:**
   - أثناء معالجة الشراء، يتم قفل الأزرار وإظهار طبقة الانتظار `LoadingOverlay`.
   - لا تُمنح أي عملة أو محتويات حزمة إلا بعد وصول حدث التأكيد الرسمي `HandlePurchaseSuccess`.
   - عند الإلغاء أو الفشل، يظهر إشعار فوري وتعود الأزرار لحالتها التفاعلية الطبيعية دون أي تجميد.

---

## 6. الهيكلية التفصيلية لنافذة الوصول للعب الجماعي (Multiplayer Access)

```
MULTIPLAYER ACCESS MODAL (CanvasGroup, MultiplayerAccessGateModal)
├── Backdrop (Image - 88% Dark Tint Fullscreen)
└── ModalBox (RectTransform [0.045, 0.04] to [0.955, 0.96], Image: window.png)
    ├── Header
    │   ├── Title (TextMeshProUGUI: "ONLINE MULTIPLAYER ACCESS", Orbitron Cyan)
    │   └── CloseBtn (Button, Image: Close Icon.png, Scale Punch Animation)
    ├── StatusBanner (Image: card_normal.png with Dynamic Status Glow)
    │   ├── Status (TextMeshProUGUI: PERMANENT / TEMPORARY / REQUIRED)
    │   └── Timer (TextMeshProUGUI: Live Remaining Countdown "Expires in: 23h 45m 12s")
    ├── OptionsScrollView (ScrollRect - Elastic Vertical)
    │   └── Content (VerticalLayoutGroup)
    │       ├── Card_RewardedAd (Option C: Video Icon, "WATCH VIDEO AD", "+24H PASS", Green Button)
    │       ├── Card_OnlineAccess (Option A: Online Icon, "ONLINE ACCESS PASS", Real Price, Blue Button)
    │       └── Card_RemoveAds (Option B: Star/Check Icon, "REMOVE ADS + ACCESS", Real Price, Gold Card Button)
    └── FooterArea
        ├── EnterMultiplayerBtn (Primary Action, Green Glowing Button, Pulse Loop Tween)
        ├── RestoreBtn (Button: "RESTORE PURCHASES")
        ├── Feedback (TextMeshProUGUI: Toast Messages)
        └── LoadingOverlay (CanvasGroup, Dark Dim + Spinner Text)
```

---

## 7. ربط خيارات الوصول الثلاثة

### الخيار أ — Online Access Pass (`online_access`):
- **النوع:** شراء داخل التطبيق دائم (Non-consumable).
- **السلوك:** يستدعي `IAPManager.Instance.BuyProduct("online_access")`.
- **الامتياز:** عند النجاح، يستدعي `MultiplayerAccessManager.Instance.GrantPermanentAccess()` لمنح وصول دائم لا ينتهي.
- **عرض الحالة:** يتحول الزر إلى `OWNED` مع علامة خضراء عند التملك ويُعطل الشراء المكرر.

### الخيار ب — Remove Ads + Online Access (`remove_ads`):
- **النوع:** شراء داخل التطبيق دائم شامل (Non-consumable).
- **السلوك:** يستدعي `IAPManager.Instance.BuyProduct("remove_ads")`.
- **الامتياز:** يزيل كافة الإعلانات الإجبارية (`GameDataManager.Instance.SetAdsRemoved(true)`) ويمنح في نفس اللحظة وصولاً دائماً لطور اللعب الجماعي (`GrantPermanentAccess()`).
- **عرض الحالة:** يتحول الزر إلى `OWNED` فور تملك إزالة الإعلانات.

### الخيار ج — Rewarded Video Ad (وصول مؤقت 24 ساعة):
- **النوع:** مشاهدة إعلان مكافأة مجاني (Rewarded Video).
- **السلوك:** يستدعي الدالة المركزية `MultiplayerAccessManager.Instance.RequestRewardedAdAccess()`.
- **الامتياز:** يمنح صلاحية دخول لمدة **24 ساعة كاملة** فقط بعد تأكيد اكتمال مشاهدة الإعلان من قِبل شبكة الإعلانات الرسمية (`GameAdManager`).
- **عرض الحالة:** يظهر عداد تنازلي حي ومباشر في البانر العلوي (`Expires in: 23h 45m 12s`)، ويتحول نص الزر إلى `EXTEND PASS (+24H)` ليتمكن اللاعب من تمديد الصلاحية متى شاء.
- **زر الدخول المباشر (Enter Multiplayer):**
  - يظهر الزر الأخضر الكبير فور امتلاك وصول صالح (دائم أو مؤقت).
  - عند الضغط عليه، ينتقل اللاعب بسلاسة إلى مشهد قائمة اللعب الجماعي `MultiplayerMenu`.

---

## 8. إعدادات CanvasScaler ومرجع الدقة

- **الموقع:** كائن `Canvas` الرئيسي في مشهد `Assets/Scenes/Main.unity`.
- **UI Scale Mode:** `Scale With Screen Size` (القيمة الرقمية `1`).
- **Reference Resolution:** `1080 x 1920` (الدقة المرجعية المعتمدة رسمياً في المشروع للوضع الرأسي Portrait).
- **Screen Match Mode:** `Match Width Or Height` (القيمة الرقمية `0`).
- **Reference Pixels Per Unit:** `100`.

---

## 9. قيم المطابقة والنسب المدعومة

- **قيمة المطابقة الحالية في المشروع:** `Match = 1.0` (مطابقة الارتفاع Height).
- **التوافق مع الهواتف:** في الشاشات الطويلة (مثل 19.5:9 أو 20:9)، تحافظ الواجهة على ثبات أبعاد العناصر الرأسية بينما تتكيف العروض بهوامش متناسقة.
- **التوافق مع الأجهزة اللوحية (Tablets):** في الشاشات العريضة الرأسية (مثل 3:4 في أجهزة iPad)، تم تزويد الواجهتين بمحدد أقصى للعرض (`MaxWidth = 800px – 920px`) بحيث تبقى النافذة متموضعة في منتصف الشاشة كصندوق حوار أنيق وبأبعاد مريحة بدلاً من التمدد المفرط على كامل العرض.

---

## 10. تعديلات المحاذاة والارتكاز (Anchors & Pivots)

1. **صندوق متجر الشراء (`PopupCard`):**
   - **الارتكاز (Pivot):** `(0.5, 0.5)` في المركز التام.
   - **الحد الأدنى للمحاذاة (AnchorMin):** `{ x: 0.035, y: 0.04 }`.
   - **الحد الأقصى للمحاذاة (AnchorMax):** `{ x: 0.965, y: 0.95 }`.
   - **الإزاحات (Offsets):** `offsetMin = Vector2.zero`, `offsetMax = Vector2.zero`.
   - **الفائدة:** بعد أن كانت النافذة القديمة محصورة بين y: 0.35 إلى 0.71 (أقل من ثلث الشاشة)، أصبحت الآن تستغل المساحة بنسبة متوازنة (4% إلى 95%) لإعطاء مساحة كافية لكافة الحزم وقوائم التمرير.

2. **صندوق بوابة الوصول (`ModalBox`):**
   - **الارتكاز (Pivot):** `(0.5, 0.5)`.
   - **الحد الأدنى للمحاذاة (AnchorMin):** `{ x: 0.045, y: 0.04 }`.
   - **الحد الأقصى للمحاذاة (AnchorMax):** `{ x: 0.955, y: 0.96 }`.
   - **الإزاحات (Offsets):** صفرية تامة لضمان المرونة الكاملة.

---

## 11. نظام معالجة المساحات الآمنة للشاشات (Safe-Area Setup)

تم بناء محرك تجاوبي داخلي في كلا المتحكمين يقرأ تلقائياً `Screen.safeArea`:
- حساب الهوامش العلوية للمناطق المقطوعة (Notch / Dynamic Island) وقفل مسافة أمان رأسية بين `2%` إلى `8%`.
- حساب الهوامش السفلية لشريط الإيماءات (Home Indicator Bar) وقفل مسافة أمان بين `2%` إلى `8%`.
- ضمان بقاء زر الإغلاق وعناوين الواجهة أسفل النوتش تماماً على أجهزة iPhone الحديثة وهواتف Android ذات الكاميرا المثقوبة، وبقاء أزرار الشراء والدخول فوق شريط السحب السفلي.

---

## 12. مكونات التخطيط التجاوبي للهواتف والأجهزة اللوحية

تعتمد الواجهتان المعاد تصميمهما على مزيج ذكي من مكونات التخطيط التلقائية الرسمية في Unity UI:
1. **قوائم التمرير المرنة (ScrollRect):**
   - قائمة تمرير أفقية مع تخميد مرن (Elastic) لعرض العناصر المتضمنة في الحزمة دون قص أو اختفاء.
   - قوائم تمرير رأسية للعملات الذهبية، رخص الأونلاين، وخيارات الوصول لضمان سهولة الوصول لكافة العناصر حتى على الشاشات الصغيرة جداً.
2. **مجموعات التخطيط (Layout Groups):**
   - `VerticalLayoutGroup` و `HorizontalLayoutGroup` لترتيب البطاقات والأزرار بمسافات متناسقة (Spacing) وهوامش داخلية (Padding).
   - استخدام `childForceExpandWidth = true` و `childForceExpandHeight = false` لمنع تشويه ارتفاع البطاقات أو تمددها غير الطبيعي.
3. **محدد الحجم التلقائي (ContentSizeFitter):**
   - ضبط `Vertical Fit` إلى `PreferredSize` على كائنات `Content` لتمكين `ScrollRect` من حساب ارتفاع القائمة الحقيقي بدقة ودون أي تداخل.

---

## 13. حركات DOTween Pro وإعداداتها القابلة للتخصيص

تم استغلال إمكانيات مكتبة **DOTween Pro** المثبتة في المشروع لتقديم تجربة حركية سلسة:

1. **حركة فتح النوافذ (Popup Show Transition):**
   - تلاشي تدريجي للشفافية: `DOFade(1f, 0.25f)` باستخدام `CanvasGroup`.
   - ارتداد ناعم للمقياس: `DOScale(1f, 0.3f).SetEase(Ease.OutBack)`.
2. **حركة إغلاق النوافذ (Popup Hide Transition):**
   - تقليص المقياس: `DOScale(0.82f, 0.2f).SetEase(Ease.InBack)`.
   - تلاشي الشفافية: `DOFade(0f, 0.2f)`.
3. **تبديل التبويبات (Tab Switching Crossfade):**
   - تكبير طفيف للتبويب النشط: `DOScale(1.04f, 0.2f)`.
   - إضاءة خلفية التبويب وتلاشي اللوحة المختارة: `DOFade(1f, 0.2f)`.
4. **تأثير اللمس على الأزرار (Tactile Button Punch):**
   - ارتداد انضغاطي ملموس عند النقر: `DOPunchScale(new Vector3(-0.08f, -0.08f, 0), 0.18f)`.
5. **نبض زر الدخول (Enter Multiplayer Pulse):**
   - نبض دوري جذاب للزر الأخضر عندما يكون الوصول متاحاً: `DOScale(1.04f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)`.
6. **أمان الذاكرة والحركات (Tween Safety):**
   - كافة الحركات تضبط `SetUpdate(true)` لتعمل حتى لو كانت اللعبة في وضع الإيقاف المؤقت (`Time.timeScale == 0`).
   - تنظيف وقتل كافة الـ Tweens النشطة فورياً عند إغلاق النافذة أو تدميرها (`DOKill()`) لمنع أي تسريب للذاكرة أو وصول لعناصر محذوفة.
   - **قاعدة ذهبية:** لا ترتبط أي جائزة أو عملية شراء أو انتقال لمشهد باكتمال أي حركة (Animations never determine logic).

---

## 14. مراجع المفتش والربط التلقائي واليدوي

### أ. الربط التلقائي المكتمل:
تم ربط كافة الحقول والمكونات الأساسية برمجياً وعبر المشهد `Main.unity`:
- تم تعيين خطوط `titleFont` و `bodyFont`.
- تم تعيين صور الإطارات `windowFrameSprite` و `cardNormalSprite` و `cardGoldSprite`.
- تم تعيين صور الأزرار والأيقونات لكلا الواجهتين.
- تم ربط أزرار الإغلاق والتبويبات وأزرار الشراء.
- في حال الحاجة لإعادة توليد أو تحديث الواجهتين في المشهد بأي وقت، يتوفر خياران فوريان:
  1. عبر القائمة العلوية في المحرر: **`Tools > Shootx UI > Setup Redesigned Shop & Multiplayer Access in Main Scene`**.
  2. عبر الضغط بالزر الأيمن على المكون في Inspector واختيار **`Rebuild Shop UI`** أو **`Rebuild Access Gate UI`**.

### ب. صور المنتجات (Artwork Handling):
- بناءً على التعليمات الصارمة، تركت مراجع صور المنتجات للحزم والأسلحة فارغة (`null`) عمداً بانتظار تزويد المشروع بالرسومات الرسمية.
- تتعامل الواجهتان مع ذلك بأناقة عبر عرض صندوق معاينة تقني محايد بدون توليد أي صور عشوائية وبدون التسبب بأي أخطاء كونسول (`No NullReferenceExceptions`).

---

## 15. دليل اختبار التجاوب عبر أبعاد الشاشات المختلفة

لاختبار الواجهتين داخل محرر Unity والتأكد من توافقهما التام مع الهواتف والأجهزة اللوحية:

1. افتح نافذة **Game View** أو **Device Simulator** في محرر Unity.
2. اختبر الأبعاد التالية من قائمة الدقة:
   - **الهواتف القياسية (Standard Phones - 16:9):** `1080 x 1920` أو `720 x 1280`.
     - *التحقق:* يجب أن تملأ النافذة الشاشة بهوامش أنيقة (93% عرض)، مع إمكانية الوصول السهل لزر الإغلاق.
   - **الهواتف الطويلة والحديثة (Tall / Notch Phones - 19.5:9 / 20:9):** `1080 x 2340` أو `1080 x 2400` (مثل Samsung S22/S23 أو iPhone 14/15 Pro).
     - *التحقق:* يجب أن تظل العناصر بعيدة عن النوتش العلوي وشريط السحب السفلي بفضل نظام Safe-Area.
   - **الهواتف المدمجة (Compact Phones):** `750 x 1334` (مثل iPhone SE).
     - *التحقق:* يجب أن تعمل قوائم التمرير `ScrollRect` بنعومة دون أي قص أو تداخل بين الأزرار.
   - **الأجهزة اللوحية الرأسية (Tablets - 3:4 / 4:3):** `1536 x 2048` (iPad).
     - *التحقق:* يجب ألا تتمدد النافذة لتغطي كامل العرض بشكل مبالغ، بل تلتزم بعرض أقصى محدد (`800px – 920px`) وتتوسط الشاشة بشكل متناسق وفخم.
3. افتح القائمة `Tools > Shootx UI > Validate Redesigned UI` للتأكد من نجاح كافة الفحوصات وظهور 0 أخطاء في الكونسول.

---

## 16. تأكيد نطاق العمل الصارم وعدم المساس بالأنظمة الأخرى

نؤكد بصورة رسمية وقاطعة أن العمل اقتصر تماماً وبدقة متناهية على الواجهتين المطلوبتين:

1. **الواجهات والأنظمة التي لم تُمسس نهائياً وبقيت على حالتها الأصلية:**
   - متجر الأسلحة (`GunsPanel` / `Weapons Store`).
   - متجر الأزياء والشخصيات (`OutfitsPanel` / `Outfits Store`).
   - نافذة الإعدادات (`Settings Popup`).
   - لوحة المتصدرين (`LEADERBOARD POPUP`).
   - القائمة الرئيسية (`Main Menu`).
   - واجهة المهام (`Missions Popup`).
   - كافة مشاهد اللعب الجماعي (`MainMultiplayer`, `MultiplayerLobby`, `MultiplayerMenu`).
   - خرائط اللعب الجماعي الأربعة (`PVP Map 1` إلى `PVP Map 4`).
   - أنظمة الشبكة والمعارك وإدارة المباريات.
2. **استقرار الاقتصاد والمشتريات:**
   - لم يتم تغيير أي معرف منتج (Product ID).
   - لم يتم تغيير أي من أسعار الحزم أو أسعار العملات.
   - لم يتم تكرار أو تجاوز `IAPManager` أو `GameDataManager`؛ كافة العمليات تمر عبر نفس دورة الحياة المعتمدة في المشروع.
   - تم التحقق من سلامة البناء وتجميع المشروع (`0 Compilation Errors`) واستقرار كافة الوظائف.

---

## 17. معمارية الحفاظ على التصميم اليدوي (Non-Destructive Data-Binding Architecture)

بناءً على التعديلات اليدوية التي أجريتها في محرر Unity، تم ضبط وتحديث الأكواد البرمجية (`ShopPopupController` و `MultiplayerAccessGateModal`) لتعمل وفق **المعمارية غير الهدامة (Non-Destructive Pattern)**:

1. **عدم المساس بالتخطيط والأبعاد إطلاقاً (Zero Transform Overrides):**
   - تم إيقاف أي كود برمجي كان يقوم بتعديل `anchorMin` أو `anchorMax` أو `sizeDelta` أو `pivot` أو `anchoredPosition` أثناء التشغيل.
   - يعتمد النظام بنسبة 100% على التخطيط والأبعاد والمواقع التي حددتها أنت يدوياً داخل المحرر (Inspector / Scene View).
2. **تحديث القيم والبيانات فقط (Pure Value Binding):**
   - ينحصر دور الكود في تحديث النصوص (`TextMeshProUGUI.text`) مثل الأسعار المحلية، أسماء الحزم، العداد التنازلي للوقت المتبقي، وحالات الملكية (`OWNED`).
   - ربط وتفعيل الأحداث البرمجية للأزرار (`onClick.AddListener`) مع نظام المشتريات `IAPManager` وبوابة اللعب الجماعي `MultiplayerAccessManager`.
3. **استخدام الأزرار والبطاقات اليدوية كقوالب (Prefab / Template Pattern):**
   - عند عرض عناصر أي حزمة داخل صندوق العرض `itemsContainer`، لا يقوم الكود ببناء عناصر برمجية عشوائية؛ بل يأخذ أول بطاقة صممتها أنت يدوياً ويعتبرها **قالب أصلي (Template)**، ويقوم باستنساخها (`Instantiate`) مع الحفاظ على كافة خصائصها البصرية من أبعاد وخطوط ومواد، ويعدل فقط نصوص الاسم والنوع وحالة الملكية.
   - وكذلك في شريط أزرار الحزم `BundleRibbon`، يتم استخدام أزرارك الحالية كقوالب لأي زر إضافي، مما يضمن تطابقاً بصرياً تاماً مع تصميمك اليدوي.

---
**تم إنجاز هذا العمل وفق أعلى المعايير الهندسية والجمالية لمحرك Unity 6 ليكون جاهزاً للإنتاج والتشغيل الفوري.**
