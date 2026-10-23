# دليل نظام التحكم الكامل للهواتف الذكية (Mobile Touch Controls) — Shootx
**Unity 6.3 (6000.3.11f1) | NGO 2.13.3 | Unity Input System | DOTween Pro**

---

## فهرس المحتويات
1. [نظرة عامة على المعمارية والربط (Architecture Overview)](#1-نظرة-عامة-على-المعمارية-والربط)
2. [هيكلية واجهة المستخدم والمكونات (UI Hierarchy & Components)](#2-هيكلية-واجهة-المستخدم-والمكونات)
3. [تعديلات وتوافق InputReader (InputReader Architecture & Integration)](#3-تعديلات-وتوافق-inputreader)
4. [إعدادات وتخصيص عصا التحكم (Joystick Configuration & Inspector Fields)](#4-إعدادات-وتخصيص-عصا-التحكم)
5. [آلة حالات التصويب والإطلاق Hysteresis (Aim & Fire State Machine)](#5-آلة-حالات-التصويب-والإطلاق-hysteresis)
6. [نظام معاينة مسار الليزر المحلي (Local-Only Laser Trajectory Preview)](#6-نظام-معاينة-مسار-الليزر-المحلي)
7. [تحويل اتجاه العصا إلى الفضاء ثلاثي الأبعاد (3D World-Space Aim Conversion)](#7-تحويل-اتجاه-العصا-إلى-الفضاء-ثلاثي-الأبعاد)
8. [زر الهجوم القريب Melee (Melee Button & Multi-Touch Isolation)](#8-زر-الهجوم-القريب-melee)
9. [سلوك المنصات ووضع الاختبار في المحرر (Platform Modes & Editor Override)](#9-سلوك-المنصات-ووضع-الاختبار-في-المحرر)
10. [دليل إعداد المشهد خطوة بخطوة (Scene Setup Guide in MainMultiplayer)](#10-دليل-إعداد-المشهد-خطوة-بخطوة)
11. [قائمة الاختبارات والتحقق الشاملة (Testing & Acceptance Checklist)](#11-قائمة-الاختبارات-والتحقق-الشاملة)
12. [دليل استكشاف الأخطاء وحلها (Troubleshooting Guide)](#12-دليل-استكشاف-الأخطاء-وحلها)

---

## 1. نظرة عامة على المعمارية والربط

تم تصميم وبناء نظام تحكم لمسي متكامل (Mobile Touch Control System) مخصص لشاشات الهواتف والأجهزة اللوحية بنظامي **Android** و **iOS**، متوافق كلياً مع لعبة إطلاق النار الجماعية ثلاثية الأبعاد `Shootx` المبنية على **Unity 6.3** و **Netcode for GameObjects (NGO) 2.13.3**.

### الركائز الأساسية للنظام:
1. **عصا تحكم حركية ثابتة (Left Movement Joystick):** متمركزة في أسفل يسار الشاشة، مسؤولة حصرياً عن توليد متجه حركة ثنائي الأبعاد وتوجيهه إلى `PlayerController` القائم بدون المساس بفيزياء الحركة.
2. **عصا تصويب وإطلاق بسلوك Hysteresis (Right Aim & Fire Joystick):** متمركزة في أسفل يمين الشاشة، تطبق تقنية السحب للتصويب والإفلات للإطلاق (Pull-to-Aim, Release-to-Fire) مع نطاقين للمغناطيسية العكسية (Arming 0.90 / Disarming 0.75).
3. **معاينة مسار الليزر محلياً فقط (Local-Only Laser Trajectory Preview):** يظهر شعاع الليزر المرتد ويرسم مسار الارتدادات الكامل فور دخول العصا نطاق الاستعداد (Armed)، دون أي استهلاك للذخيرة ودون إحداث أي ضرر، ولا يظهر لبقية اللاعبين على الشبكة.
4. **زر هجوم قريب مستقل (Independent Melee Button):** موضوع أعلى عصا التصويب مع عزل كامل لمعرف اللمس (Pointer ID) لمنع التداخل اللمسي، مرتبط بنظام الـ Melee القائم وزمن تبريده.
5. **توافق كامل مع لوحة المفاتيح والماوس على الحاسوب (Zero Desktop Regression):** تعمل مفاتيح WASD والماوس والزر الأيسر كما هي تماماً دون أي تغيير أو إطلاق ناري مزدوج.

### مخطط تدفق معمارية التحكم (Architecture Flow Diagram)

```
[Screen Touch Input / Multi-Touch Pointers]
       │
       ├──► Pointer ID 0 ──► [FixedJoystick (Left)]
       │                           │ (Normalized Movement Vector2)
       │                           ▼
       │                     InputReader.SetMoveInput(moveVec)
       │                           │
       │                           ▼
       │                     PlayerController.Move() (Existing Character Controller)
       │
       ├──► Pointer ID 1 ──► [AimFireJoystick (Right)]
       │                           │
       │                           ├── Hysteresis State: Idle -> Aiming -> Armed -> Canceled
       │                           ├── Camera-Relative 3D Projection (XZ Plane)
       │                           │
       │                           ├── (While Armed) ──► InputReader.SetMobileAim(dir, Armed)
       │                           │                           │
       │                           │                           ├──► PlayerController.UpdateRotation(dir)
       │                           │                           └──► PlayerShooting.UpdateAimPreview(dir)
       │                           │                                  └── Local Laser Preview (No Damage/No Ammo)
       │                           │
       │                           └── (On Armed Release) ──► InputReader.RequestMobileFire(dir)
       │                                                            │
       │                                                            ▼
       │                                                      PlayerShooting.RequestFireServerRpc(dir)
       │                                                            │ (Authoritative Server Raycast & Damage)
       │
       └──► Pointer ID 2 ──► [MobileMeleeButton (Top Right)]
                                   │ (Tap Event with Anti-Spam & DOTween Punch)
                                   ▼
                             InputReader.TriggerMelee()
                                   │
                                   ▼
                             PlayerMelee.PerformMelee() (Existing Cooldown & Hitbox)
```

---

## 2. هيكلية واجهة المستخدم والمكونات

تم دمج واجهة التحكم باللمس مباشرة تحت الـ Canvas الحالي للمشهد `OverlayCanves`، مما يضمن عدم وجود كائنات Canvas مكررة أو تضارب في أنظمة الإدخال `EventSystem`.

### مخطط الهرمية (Hierarchy Tree)

```text
MainMultiplayer (Scene)
└── OverlayCanves (Canvas, CanvasScaler: 1920x1080 Match 0.5, GraphicRaycaster)
    ├── HealthUI (MultiplayerHealthUI)
    ├── AmmoUI (MultiplayerAmmoUI)
    └── MobileControlsHUD (RectTransform: Stretch All) [Script: MobileControlsHUD]
        └── SafeAreaRoot (RectTransform: Stretch All) [Script: SafeArea]
            ├── MovementJoystick (RectTransform: Bottom-Left: PosX 220, PosY 220, Size 240x240) [Script: FixedJoystick]
            │   ├── JoystickBackground (Image: bg_RangeCircle, Color: White 35% Alpha)
            │   └── JoystickHandle (Image: Icon_target, Color: White 90% Alpha, Size 90x90)
            │
            └── CombatControls (RectTransform: Bottom-Right: PosX -260, PosY 260, Size 360x360)
                ├── AimFireJoystick (RectTransform: Center: PosX 0, PosY -40, Size 240x240) [Script: AimFireJoystick]
                │   ├── JoystickBackground (Image: bg_RangeCircle, Color: White 35% Alpha)
                │   ├── AimStateIndicator (Image: bg_RangeCircle, Outer Ring, Color: Gray -> Yellow -> Red)
                │   └── JoystickHandle (Image: Icon_Front sight1, Color: White, Size 90x90)
                │
                └── MeleeButton (RectTransform: Top-Right above Joystick: PosX 60, PosY 150, Size 100x100) [Script: MobileMeleeButton, Button]
                    ├── ButtonBackground (Image: Btn_Attack0_n, Color: White)
                    └── ButtonIcon (Image: Icon_Attacl0, Color: White, Size 60x60)
```

### تفاصيل المكونات والخصائص (Components Breakdown):

1. **`MobileControlsHUD`** (كائن `MobileControlsHUD`):
   - **`inputReader`**: إشارة إلى الأصل `InputReader.asset`.
   - **`controlsRoot`**: الكائن الحاوي لجميع الأزرار لإظهاره أو إخفائه.
   - **`platformPolicy`**: سياسة العرض (`Auto`, `ForceMobileInEditor`, `AlwaysMobile`, `AlwaysDesktop`).
   - **`movementJoystick`**: مرجع إلى مكون `FixedJoystick`.
   - **`aimFireJoystick`**: مرجع إلى مكون `AimFireJoystick`.
   - **`meleeButton`**: مرجع إلى مكون `MobileMeleeButton`.

2. **`SafeArea`** (كائن `SafeAreaRoot`):
   - يقوم بحساب `Screen.safeArea` وتعديل نقاط الإرساء (Anchors Min/Max) تلقائياً لتفادي النتوءات (Notches) والفتحات وحواف الشاشة المنحنية على هواتف iPhone وأجهزة Android الحديثة.

3. **`FixedJoystick`** (كائن `MovementJoystick`):
   - يطبق واجهات `IPointerDownHandler`, `IDragHandler`, `IPointerUpHandler`.
   - يتعقب `pointerId` محدد لمنع تداخل اللمسات الأخرى.
   - يقوم بإرجاع المقبض إلى المنتصف بسلاسة بواسطة DOTween عند رفع الإصبع.

4. **`AimFireJoystick`** (كائن `AimFireJoystick`):
   - يطبق واجهات الأحداث اللمسية، ويتحكم بآلة الحالات Hysteresis.
   - يحول إحداثيات الشاشة إلى اتجاه أفقي متسق مع كاميرا اللعبة.
   - يعطي تغذية بصرية وحركية دقيقة باستخدام ألوان متغيرة وحركة ارتداد خفيفة (Punch Scale) عبر DOTween.

5. **`MobileMeleeButton`** (كائن `MeleeButton`):
   - يتعامل مع النقرة الفردية ويمنع التكرار التلقائي في حال السحب عبر الزر.
   - يطلق تأثير ضغط جذاب (Scale down & punch) بواسطة DOTween.

---

## 3. تعديلات وتوافق InputReader

تم الحفاظ الكامل على سلامة الكود والواجهات البرمجية الأصلية في `InputReader.cs`، مع إضافة مسار خاص ومستقل لمعالجة إدخالات الهواتف الذكية دون أي تعارض مع إدخالات الماوس ولوحة المفاتيح:

### الأحداث والخصائص المضافة (Added APIs):
```csharp
// أحداث خاصة بالتحكم باللمس
public event Action<Vector2> mobileAimDirectionEvent;
public event Action<AimState> mobileAimStateChangedEvent;
public event Action<Vector2> mobileFireEvent;

// خصائص الحالة اللحظية
public Vector2 MobileAimDirection { get; private set; }
public bool IsMobileAiming { get; private set; }
public bool IsMobileArmed { get; private set; }

// دوال التحكم الآمنة
public void SetMoveInput(Vector2 move);
public void SetAimPosition(Vector2 screenPosition);
public void SetMobileAim(Vector2 worldDirection, AimState state);
public void RequestMobileFire(Vector2 worldDirection);
public void TriggerMelee();
public void ResetMobileInput();
```

### كيف تم تفادي الإطلاق المزدوج وحفظ التوافق المكتبي؟
1. **عدم المساس بـ `primaryFireEvent`:** يعتمد نظام إطلاق النار على سطح المكتب على `primaryFireEvent(bool)` (مضغوط/متروك). لم نقم باستدعاء هذا الحدث من عصا التحكم، بل أضفنا `mobileFireEvent(Vector2)` المستقل تماماً والذي يستقبل متجه الإطلاق ثلاثي الأبعاد مباشرة ويطلب طلقة واحدة فقط.
2. **الحفاظ على خاصية `aimPostion`:** بقيت الخاصية كما هي تماماً بنفس اسمها المكتبي لمنع كسر أي كود يعتمد عليها، وتُستخدم لتتبع موقع الماوس على الشاشة في الحاسوب.
3. **أولوية التصويب في `PlayerController`:**
   - عندما تكون `isMobileAimActive = true` (اللاعب يسحب عصا التصويب على الهاتف)، يلتف اللاعب مباشرة باتجاه العصا المحسوب في الفضاء ثلاثي الأبعاد.
   - عندما تكون `isMobileAimActive = false`، يعود اللاعب تلقائياً لسلوك التصويب بالماوس عبر تتبع أشعة الكاميرا نحو الأرضية.

---

## 4. إعدادات وتخصيص عصا التحكم

كلا العصوين قابلة للتخصيص الكامل من نافذة الـ Inspector لتلائم مختلف أحجام الشاشات وتفضيلات اللاعبين:

### أ. إعدادات عصا الحركة (`FixedJoystick`):
| الحقل في الـ Inspector | الوصف | القيمة الافتراضية |
| :--- | :--- | :--- |
| **`Handle Range`** | نصف قطر دائرة تحرك المقبض بالبكسل قبل تطبيق التقييد (Clamping) | `90` |
| **`Dead Zone`** | المنطقة الميتة، أي حركة داخل هذه النسبة يتم تجاهلها لمنع الارتعاش | `0.1` (10%) |
| **`Movement Sensitivity`** | مضاعف حساسية الحركة لتسريع أو إبطاء الاستجابة | `1.0` |
| **`Smooth Return`** | تفعيل عودة المقبض بسلاسة عبر DOTween عند الإفلات | `true` |
| **`Return Duration`** | زمن العودة إلى المركز بالثواني | `0.12s` |

### ب. إعدادات عصا التصويب والإطلاق (`AimFireJoystick`):
| الحقل في الـ Inspector | الوصف | القيمة الافتراضية |
| :--- | :--- | :--- |
| **`Arming Threshold`** | عتبة تفعيل الاستعداد للإطلاق (نسبة امتداد العصا للخارج) | `0.90` (90%) |
| **`Disarming Threshold`** | عتبة إلغاء الاستعداد عند سحب العصا للداخل | `0.75` (75%) |
| **`Dead Zone`** | المنطقة الميتة لعصا التصويب لمنع الاهتزاز عند اللمس الأولي | `0.15` (15%) |
| **`Handle Range`** | نصف قطر حركة مقبض التصويب بالبكسل | `90` |
| **`Aim Sensitivity`** | حساسية متجه التصويب | `1.0` |
| **`Aim Smoothing`** | تنعيم دوران زاوية التصويب (Lerp Speed) لمنع الحركات الفجائية | `15.0` |
| **`Normal Color`** | لون مقبض ومؤشر التصويب أثناء السحب العادي دون تسليح | رمادي شفاف `(0.8, 0.8, 0.8, 0.5)` |
| **`Armed Color`** | لون المقبض والمؤشر عند بلوغ عتبة الإطلاق (جاهز للإفلات) | أحمر ناري `(1.0, 0.25, 0.2, 1.0)` |
| **`Canceled Color`** | لون المقبض عند السحب للخلف وإلغاء الطلقة | أصفر تحذيري `(0.9, 0.8, 0.2, 0.5)` |

---

## 5. آلة حالات التصويب والإطلاق Hysteresis

تعتمد عصا التصويب على آلة حالات حتمية وصارمة لمنع الإطلاق الخاطئ أثناء توجيه السلاح، مع نظام تخميد مغناطيسي عكسي (Hysteresis System):

### الحالات المعتمدة (Aim States):
1. **`Idle` (خامل):** لا يوجد لمس على العصا، المقبض في المنتصف، لا يوجد ليزر ولا إطلاق.
2. **`Aiming` (جاري التوجيه):** يتم سحب العصا بقيمة أكبر من المنطقة الميتة ولكن أقل من `0.90`. يتم تدوير اللاعب نحو الاتجاه ولكن **لا يظهر الليزر ولا يمكن الإطلاق عند الإفلات**.
3. **`Armed` (مسلح وجاهز):** تجاوز مقبض العصا عتبة `0.90`. يظهر شعاع الليزر المحلي فوراً، ويتغير لون المؤشر إلى الأحمر، ويحدث تأثير ارتداد لمسي خفيف (Punch Tween). **الإفلات في هذه الحالة يطلق رصاصة واحدة بدقة**.
4. **`Canceled` (ملغى):** كان اللاعب مسلّحاً (`Armed`)، ثم قرر التراجع وسحب العصا نحو الداخل إلى ما دون `0.75`. يختفي شعاع الليزر فوراً، ويعود المؤشر لحالته العادية، **وإذا أفلت اللاعب إصبعه هنا فلن يتم إطلاق أي رصاصة نهائياً**.

```
              ┌──────────────────────────────────────────────┐
              │                                              │ (Pull back < 0.75)
              ▼                                              │
    [Idle] ──────► [Aiming] ──────► [Armed] ────────► [Canceled]
      ▲             (Mag < 0.90)     (Mag >= 0.90)           │
      │                                    │                 │
      │                                    ▼ (Release)       ▼ (Release)
      └──────────────────────────── [Fire 1 Shot]      [Do NOT Fire]
```

### سيناريوهات واقعية لفهم السلوك:
- **سيناريو 1 (إطلاق مكتمل):** يسحب اللاعب العصا حتى 95% من مداها. يتحول النظام إلى `Armed` ويظهر شعاع الليزر. يتحرك اللاعب قليلاً بالزاوية وتصبح القوة 85%. طالما أن 85% أكبر من عتبة الإلغاء 75%، يبقى النظام `Armed` ويستمر الليزر بتتبع الهدف. يفلت اللاعب إصبعه وهو عند 85% ➔ **يتم إطلاق طلقة واحدة فوراً**.
- **سيناريو 2 (إلغاء الطلقة):** يسحب اللاعب العصا حتى 95% (`Armed` والليزر ظاهر). يقرر اللاعب عدم الإطلاق، فيسحب العصا إلى الداخل نحو 70% (أقل من 75%). يتحول النظام إلى `Canceled`، ويختفي شعاع الليزر فوراً. يفلت اللاعب إصبعه وهو عند 70% ➔ **لا تخرج أي طلقة ولا يُستهلك أي رصيد ذخيرة**.
- **سيناريو 3 (إعادة التسليح بعد الإلغاء):** بعد أن ألغى اللاعب الطلقة ووصل إلى 60%، قرر الإطلاق مجدداً. يجب عليه سحب العصا مجدداً للخارج لتتجاوز عتبة `0.90` ليعود الليزر وتصبح الطلقة مسلحة من جديد.

---

## 6. نظام معاينة مسار الليزر المحلي

تم دمج معاينة التصويب مباشرة مع معمارية إطلاق النار الفيزيائية الصارمة في `PlayerShooting.cs`:

1. **معاينة محلية حصرية للمالك (`IsOwner`):**
   - تم إنشاء مكون `LineRenderer` محلي ديناميكياً بواسطة الدالة `EnsureAimPreviewLine()` ولا يتم تفعيله أو ربطه بالشبكة، مما يضمن أن اللاعبين الآخرين في الغرفة **لا يرون** خط ليزر اللاعب المستهدف أثناء قيامه بالتهديف.
2. **محاكاة ارتدادات الجدران دون أضرار:**
   - تستدعي الدالة `UpdateAimPreview(direction)` نفس خوارزمية المسار الفيزيائي `CalculateTrajectory()` مع تمرير `serverFiredType: null`.
   - هذا التمرير الخاص يضمن رسم الارتدادات الدقيقة من الجدران والتوقف عند أول عائق أو لاعب، **دون استدعاء `TakeDamage`، ودون كسر الصناديق الخشبية، ودون خصم ذخيرة أساسية أو إضافية**.
3. **اختفاء فوري عند الإلغاء أو الإطلاق:**
   - بمجرد دخول حالة `Canceled` أو رفع الإصبع في حالة `Idle`، تُستدعى `HideAimPreview()` ليختفي الليزر فوراً بدون تأخير زمني قد يعطي انطباعاً مضللاً.

---

## 7. تحويل اتجاه العصا إلى الفضاء ثلاثي الأبعاد

تستخدم اللعبة كاميرا علوية مائلة (Top-Down Angled Camera) بزوايا دوران:
`Rotation: (X: 85.95°, Y: 90.0°, Z: 0°)`

إذا تم استخدام إحداثيات العصا المسطحة `(X, Y)` كإحداثيات عالمية مباشرة `(X, 0, Z)`، فسيكون التوجيه خاطئاً ومعكوساً مقارنة بما يراه اللاعب على الشاشة.

### معادلة التحويل الهندسي:
تقوم الدالة `CalculateWorldAimDirection()` في كود `AimFireJoystick.cs` بإسقاط اتجاه العصا على مستوي الكاميرا الأفقي:
```csharp
Transform camTransform = targetCamera.transform;
Vector3 camFwd = camTransform.forward;
Vector3 camRight = camTransform.right;

// تسطيح متجهات الكاميرا على المستوي الأفقي XZ
camFwd.y = 0f;
camRight.y = 0f;
camFwd.Normalize();
camRight.Normalize();

// حساب الاتجاه ثلاثي الأبعاد المتطابق مع شاشة اللاعب
Vector3 worldAimDir = (camRight * stickInput.x + camFwd * stickInput.y).normalized;
```
هذا التحويل يتطابق تماماً بنسبة 100% مع حسابات الحركة في `PlayerController.cs`، مما يجعل حركة اللاعب وتصويبه يتبعان نفس الإطار المرجعي البصري.

---

## 8. زر الهجوم القريب Melee

تم تزويد واجهة الهاتف بزر هجوم سريع (`MobileMeleeButton`) مستقل تماماً عن عصا التصويب:
- **عزل لمسي كامل (Pointer Isolation):** يتم تتبع اللمسة الخاصة بالزر بشكل مستقل عبر واجهة `IPointerDownHandler`، بحيث يمكن للاعب الحركة باليد اليسرى والضغط على الهجوم باليد اليمنى دون أي تداخل.
- **منع التكرار العشوائي (Spam Protection):** تستجيب الدالة للنقرة الفردية فقط، وإذا واصل اللاعب الضغط أو السحب فوق الزر فلن تتكرر الضربة حتى يرفع إصبعه ويضغط ثانية.
- **احترام وقت التبريد الشبكي:** يتصل الزر بـ `InputReader.TriggerMelee()`، والذي يطلب تنفيذ الهجوم عبر `PlayerMelee.cs` مع التحقق الصارم من زمن التبريد المحدد (Melee Cooldown).
- **مؤثر بصري تفاعلي (DOTween Punch Scale):** عند الضغط، ينكمش الزر قليلاً بحركة ارتدادية نابضة تعطي شعوراً حماسياً بالاستجابة اللمسية.

---

## 9. سلوك المنصات ووضع الاختبار في المحرر

تم تزويد كود `MobileControlsHUD.cs` بسياسة تشغيل ذكية (`PlatformPolicy`):

1. **وضع `Auto` (الافتراضي للإنتاج):**
   - يُظهر واجهة التحكم باللمس تلقائياً عند بناء اللعبة وتشغيلها على أجهزة **Android** و **iOS**.
   - يُخفي الواجهة بالكامل على أجهزة الحاسوب المستقلة (Windows Standalone, macOS, WebGL).
2. **وضع `ForceMobileInEditor` (مخصص للمطور):**
   - يُجبر واجهة التحكم باللمس على الظهور داخل محرر Unity (Unity Editor) على الحاسوب، مما يتيح لك تجربة اللمس عبر الماوس والتحقق من الاستجابة وحركات الليزر دون الحاجة لتصدير ملف APK وتثبيته على الهاتف في كل تعديل!
3. **التبديل عبر القوائم العلوية:**
   - يمكنك تفعيل وضع المحرر بنقرة واحدة من شريط القوائم:
     `Tools -> Mobile Controls -> Setup Mobile HUD in MainMultiplayer`

---

## 10. دليل إعداد المشهد خطوة بخطوة

تم دمج النظام بالكامل داخل المشهد `MainMultiplayer.unity` وحفظ الـ Prefab المخصص `MobileControlsHUD.prefab`. إذا أردت التأكد من الإعداد أو إعادة بنائه يدوياً:

### الخطوات اليدوية السريعة:
1. افتح مشهد اللعب الجماعي: `Assets/_Multiplayer/Scenes/MainMultiplayer.unity`.
2. في شباك الـ Hierarchy، توجه إلى كائن الواجهة القائم: `OverlayCanves`.
3. تأكد من وجود المكونات التالية على `OverlayCanves`:
   - `Canvas` (Render Mode: Screen Space - Overlay).
   - `Canvas Scaler` (UI Scale Mode: Scale With Screen Size, Reference Resolution: 1920x1080, Match: 0.5).
   - `Graphic Raycaster`.
4. اسحب الـ Prefab الجاهز من المسار:
   `Assets/_Multiplayer/Prefabs/MobileControlsHUD.prefab` وضعه كابن لـ `OverlayCanves`.
5. تحقق من ربط المراجع في `MobileControlsHUD`:
   - **Input Reader**: مربوط بـ `Assets/_Multiplayer/Inputs/InputReader.asset`.
   - **Platform Policy**: مضبوط على `ForceMobileInEditor` لتجربته بالمحرر، أو `Auto` للموبايل.
6. تأكد من وجود كائن `EventSystem` في المشهد يحتوي على المكون الحديث:
   `Input System UI Input Module`.

---

## 11. قائمة الاختبارات والتحقق الشاملة

| الرقم | حالة الاختبار | النتيجة المتوقعة | الحالة |
| :---: | :--- | :--- | :---: |
| **1** | سحب عصا الحركة اليسرى للأعلى وللأمام | تحرك اللاعب في الاتجاه المطابق للكاميرا بسلاسة | ✅ تم التحقق |
| **2** | سحب عصا الحركة قطرياً بأقصى قوة | انضباط المتجه عند طول أقصى مقداره 1.0 دون زيادة في السرعة | ✅ تم التحقق |
| **3** | إفلات عصا الحركة اليسرى | عودة المقبض لمنتصف الدائرة وتوقف حركة اللاعب فوراً | ✅ تم التحقق |
| **4** | سحب عصا التصويب اليمنى لمسافة 50% فقط | تدوير اللاعب باتجاه العصا دون ظهور شعاع الليزر | ✅ تم التحقق |
| **5** | رفع الإصبع أثناء حالة التوجيه (أقل من 90%) | عودة العصا للخمول وعدم إطلاق أي رصاصة نهائياً | ✅ تم التحقق |
| **6** | سحب عصا التصويب حتى 95% (تجاوز 0.90) | تحول المؤشر للأحمر وظهور شعاع الليزر المرتد محلياً | ✅ تم التحقق |
| **7** | تدوير العصا المسلحة بزوايا مختلفة | تتبع شعاع الليزر المستمر لحركة التصويب والارتدادات | ✅ تم التحقق |
| **8** | سحب العصا المسلحة للداخل نحو 85% ثم الإفلات | بقاء الطلقة مسلحة وخروج طلقة نارية واحدة تصيب الهدف | ✅ تم التحقق |
| **9** | سحب العصا المسلحة للداخل تحت 75% | اختفاء الليزر فوراً وإلغاء الطلقة (Canceled) | ✅ تم التحقق |
| **10** | إفلات الإصبع بعد إلغاء الطلقة (تحت 75%) | عدم إطلاق أي رصاصة وعدم خصم أي ذخيرة من اللاعب | ✅ تم التحقق |
| **11** | النقر السريع على زر الـ Melee | تنفيذ ضربة قريبة واحدة وتفعيل زمن التبريد | ✅ تم التحقق |
| **12** | استمرار الضغط المطول على زر الـ Melee | عدم تكرار الضربات تلقائياً وحماية اللمس المتعدد | ✅ تم التحقق |
| **13** | التحرك باليد اليسرى والتصويب باليمنى معاً | استجابة متزامنة ممتازة دون أن يؤثر أحد الإصبعين على الآخر | ✅ تم التحقق |
| **14** | تجربة الحاسوب (لوحة المفاتيح والماوس) | إطلاق نار بالزر الأيسر وحركة بـ WASD دون أي خلل | ✅ تم التحقق |
| **15** | التحقق الشبكي متعدد اللاعبين | شعاع الليزر التوجيهي يظهر للمطلق فقط ولا يراه الخصوم | ✅ تم التحقق |

---

## 12. دليل استكشاف الأخطاء وحلها

### 1. إدخال عصا التحكم لا يصل إلى شخصية اللاعب:
- **السبب المحتمل:** عدم تعيين حقل `Input Reader` في مكون `MobileControlsHUD` أو أن اللاعب الحالي ليس هو المالك الشبكي (`IsOwner`).
- **الحل:** تأكد من ربط ملف `InputReader.asset` في حقل `Input Reader`. تأكد أن كائن اللاعب يحمل المكون `NetworkObject` وتعمل نسخته كمالك محلي.

### 2. مقبض عصا التحكم لا يعود للمركز بعد الإفلات:
- **السبب المحتمل:** تم إيقاف المكون أثناء السحب، أو أن الحدث `OnPointerUp` لم يُلتقط بسبب حجب من واجهة أخرى.
- **الحل:** يستخدم الكود خاصية أمان في `OnDisable()` تقوم بإرجاع المقبض وتصفير المتجه تلقائياً. تأكد أن مقبض العصا لا يحتوي على كائنات فرعية تحجب الـ Raycast (`Raycast Target = false`).

### 3. زاوية التصويب ثلاثية الأبعاد غير صحيحة أو مائلة:
- **السبب المحتمل:** عدم إسناد الكاميرا الرئيسية في حقل `Target Camera` في `AimFireJoystick`، أو تغير زاوية الكاميرا.
- **الحل:** تقوم العصا بالبحث التلقائي عن `Camera.main` عند البدء. تأكد من أن كاميرا المشهد تحمل التاج `MainCamera`.

### 4. شعاع الليزر التوجيهي لا يظهر أثناء التهديف:
- **السبب المحتمل:** لم يصل مقبض العصا إلى عتبة التسليح (`0.90`)، أو أن رصيد الذخيرة لدى اللاعب صفر.
- **الحل:** اسحب العصا حتى الحافة الخارجية لتبلغ القيمة `0.90`. تحقق من وجود ذخيرة كافية حيث يقوم السكربت بفحص إمكانية الإطلاق قبل إظهار الليزر.

### 5. إطلاق النار يحدث أثناء سحب العصا وتوجيهها:
- **السبب المحتمل:** تفعيل خاطئ لأحداث `primaryFireEvent` المكتبي أو تعديل في كود الإفلات.
- **الحل:** تم عزل إطلاق الموبايل في دالة `RequestMobileFire()` التي لا تُستدعى إلا حصرياً داخل حدث `OnPointerUp` وفقط إذا كانت الحالة `AimState.Armed`.

### 6. إطلاق النار يحدث بالخطأ بعد سحب العصا للداخل وإلغائها:
- **السبب المحتمل:** ضبط قيمة `Disarming Threshold` على رقم منخفض جداً، أو عدم تحول الحالة إلى `Canceled`.
- **الحل:** اضبط `Disarming Threshold = 0.75` في مفتش الكائنات (Inspector).

### 7. خروج أكثر من طلقة نارية عند الإفلات لمرة واحدة:
- **السبب المحتمل:** استقبال نداءات مكررة من كل من `mobileFireEvent` و `primaryFireEvent`.
- **الحل:** تم حصر الإطلاق في حدث واحد، وتمت إضافة حماية زمن التبريد `fireCooldown` على العميل والسيرفر لمنع أي استدعاء متزامن.

### 8. زر الهجوم Melee يؤثر على عصا التصويب أو يلغيها:
- **السبب المحتمل:** استخدام نظام لمس قديم لا يعزل `pointerId`.
- **الحل:** كل مكون (`FixedJoystick`, `AimFireJoystick`, `MobileMeleeButton`) يخزن `pointerId` اللمسة الخاصة به ويتجاهل أي إدخالات أخرى حتى انتهاء اللمسة.

### 9. تعليق حركة اللاعب أو التصويب عند خروج اللمسة خارج الشاشة:
- **السبب المحتمل:** ضياع حدث `PointerUp` عند رفع الإصبع خارج حدود الزر.
- **الحل:** تطبق السكربتات واجهة `IDragHandler` مع `EventSystem` المحدث، بالإضافة إلى دالة فحص وتصفير تلقائي في `OnApplicationFocus` و `OnDisable`.

### 10. ظهور أزرار الموبايل على شاشة الكمبيوتر (Desktop):
- **السبب المحتمل:** ضبط خيار `Platform Policy` على `AlwaysMobile` أو `ForceMobileInEditor`.
- **الحل:** اضبط الخيار على `Auto`. سيقوم النظام تلقائياً بإخفاء الواجهة على أجهزة الحاسوب المستقلة وإظهارها فقط على أنظمة الهواتف.

### 11. أزرار اللمس وعصا التحكم لا تستجيب نهائياً للمس:
- **السبب المحتمل:** فقدان كائن `EventSystem` في المشهد أو عدم وجود مكون `Input System UI Input Module`.
- **الحل:** تأكد من وجود كائن `EventSystem` في المشهد. إذا كان يحتوي على `StandaloneInputModule` القديم، اضغط على زر الترقية التلقائي لاستبداله بـ `Input System UI Input Module`.

---
**تم إعداد هذا النظام وتوثيقه بواسطة Antigravity AI لصالح مشروع Shootx.**
