# دليل نظام تحريك الشخصيات الجماعي (Multiplayer Character Animation System Guide)
## Unity 6.3 / Netcode for GameObjects (NGO 2.13.3) / Humanoid Rigging & Upper-Body Layers

---

## 1. الهيكل الفعلي لكائن اللاعب (Player Prefab Hierarchy)

تم الحفاظ على البنية الهرمية الأصلية للاعب مع دمج مجسمات الشخصيات الأربعة ونظام تعليق السلاح الديناميكي:

```text
Player (Root) [CharacterController, NetworkObject, NetworkTransform, PlayerController, PlayerShooting, PlayerMelee, PlayerHealth, PlayerDash, PlayerVisualManager, PlayerAnimationController]
├── Visuals
│   ├── ActionFigure (KayKit Humanoid Character - Model 0) [Animator]
│   ├── Driver (KayKit Humanoid Character - Model 1) [Animator]
│   ├── SpaceRanger (KayKit Humanoid Character - Model 2) [Animator]
│   ├── Survivalist (KayKit Humanoid Character - Model 3) [Animator]
│   └── Weapon (Revolver Prefab Instance)
│       ├── SM_Wep_Revolver_Cylinder_01
│       ├── SM_Wep_Revolver_Hammer_01
│       ├── SM_Wep_Revolver_Trigger_01
│       └── FirePoint (Transform لنقطة انطلاق الرصاص وأشعة الليزر)
├── MeleeHitPoint (Transform - مركز فحص ضربات المشاجرة)
├── StatusAnchor (World Canvas - شريط الصحة واسم اللاعب)
├── DashTrail (TrailRenderer - تأثير الاندفاع السريع)
└── CinemachineCamera (Cinemachine Camera + Follow + Rotation Composer)
```

### أ) الكائنات المحفوظة (Retained Objects):
- **`Visuals` ومجسمات الشخصيات الأربعة**: تم الإبقاء عليها كما هي تحت `Visuals`، حيث يتم تفعيل شخصية واحدة فقط عند بداية المباراة أو الإحياء وتعطيل الباقي.
- **`Weapon` و `FirePoint`**: تم الاحتفاظ بكامل بنية مسدس الريفولفر مع كائن `FirePoint`. يتم نقله برمجياً عند التشغيل ليرتبط بعظم اليد اليمنى للشخصية المفعلة.
- **`MeleeHitPoint`, `StatusAnchor`, `DashTrail`, `CinemachineCamera`**: تعمل بكامل كفاءتها دون أي حذف أو إعادة إنشاء.

### ب) الكائنات المربوطة وقت التشغيل (Runtime Attached):
- **`Weapon` (جذر السلاح)**: يتم فصله من كونه طفلاً مباشراً لـ `Visuals` وربطه باليد اليمنى (`handslot.r` أو عظم `HumanBodyBones.RightHand`) للشخصية النشطة، بحيث يتبع حركات اليد تماماً أثناء الركض والتصويب والإطلاق.
- **`FirePoint`**: يتحرك تلقائياً مع السلاح في يد الشخصية، ويتم تحديث مرجعه ديناميكياً داخل `PlayerShooting.SetMuzzlePoint(Transform)` لضمان مطابقة فوهة المسدس مع مسار شعاع الليزر ومعاينة التصويب على الجوال والكمبيوتر.

---

## 2. إدارة وتوزيع مجسمات الشخصيات الأربعة (Character Visuals Management)

### المكون البرمجي: `PlayerVisualManager.cs`

1. **تحديد الشخصيات**:
   - المجسمات المعتمدة: `ActionFigure` (فهرس 0)، `Driver` (فهرس 1)، `SpaceRanger` (فهرس 2)، `Survivalist` (فهرس 3).
   - جميع الشخصيات تتبع معيار KayKit Humanoid وتستخدم نفس الـ Avatar (`Rig_Medium_Avatar`).

2. **آلية الاختيار والتوزيع الحصري على السيرفر (Server-Authoritative)**:
   - يمتلك السيرفر وحده سلطة تحديد مظهر كل لاعب عبر الخاصية المتزامنة:
     ```csharp
     private readonly NetworkVariable<int> selectedCharacterIndex = new NetworkVariable<int>(
         -1,
         NetworkVariableReadPermission.Everyone,
         NetworkVariableWritePermission.Server
     );
     ```
   - عند انضمام لاعب جديد، تفحص دالة `AssignDistinctCharacterIndex()` في السيرفر جميع كائنات `PlayerVisualManager` المتصلة وتجمع الفهارس المحجوزة بالفعل، ثم تعين أول فهرس متاح (من 0 إلى 3) للاعب الجديد.
   - في حال كان عدد اللاعبين في المباراة يصل إلى 4، يضمن النظام حصول كل لاعب على مجسم فريد مختلف عن باقي زملائه.

3. **تطبيق المظهر وإلغاء تنشيط البقية**:
   - تستدعي دالة `ApplyCharacterVisual(int index)` على جميع الأجهزة:
     - تفعيل `visualRoot` للشخصية المختارة فقط وتفعيل الـ `Animator` الخاص بها.
     - إيقاف `visualRoot` للشخصيات الثلاث الأخرى وتعطيل الـ `Animator` الخاص بكل منها كلياً لمنع استهلاك المعالج أو حدوث تعارضات.
     - ربط الـ `Animator` النشط بمكون التحكم بالحركة `PlayerAnimationController.BindAnimator(animator)`.
     - ربط جذر السلاح `weaponRoot` بمقبض اليد اليمنى للشخصية المحددة.

4. **استدامة الشخصية بعد الموت وإعادة الإحياء (Persistence Across Respawn)**:
   - يبقى المتغير الشبكي `selectedCharacterIndex` محتفظاً بقيمته على السيرفر والعملاء طوال المباراة.
   - عند استدعاء حدث `playerHealth.OnRespawn`، تستدعي الدالة `HandleRespawn()` تلقائياً `ApplyCharacterVisual(selectedCharacterIndex.Value)` لإعادة توثيق تفعيل نفس المجسم وإعادة ربط السلاح بيده اليمنى دون أي تغيير في هوية الشخصية.

---

## 3. تكوين وحدة التحكم بالحركة (Animator Controller & Layer Configuration)

الملف الفعلي في المشروع:
`Assets/_Multiplayer/Players/Animation/MultiplayerPlayer.controller`

قناع الجزء العلوي:
`Assets/_Multiplayer/Players/Animation/Upper.mask`

### أ) الطبقة الأساسية (Base Layer):
- **وضع المزج**: Override.
- **الوزن**: 1.0 (افتراضي ودائم).
- **الحالات**:
  1. `Idle`: تشغل كليب الوقوف `Idle_A.anim`.
  2. `Running`: شجرة مزج ثنائية الأبعاد (2D Freeform Directional Blend Tree) بالمعاملين `MoveX` و `MoveZ`.
  - **ملاحظة هامة**: لا يتم تشغيل أو استخدام حالة `Reload` على الإطلاق، حيث لا توجد آلية إعادة تلقيم في اللعبة، والحالة غير مربوطة بأي مسار برمجي جديد.

### ب) طبقة التصويب والإطلاق العلوية (`Aimming`):
- **الاسم الفعلي في الملف**: `Aimming` (مع الحفاظ الدقيق على التهجئة الموجودة في الأصل البرمجي).
- **وضع المزج**: `Override`.
- **الوزن الافتراضي**: `0.0` (يتم رفعه تدريجياً وبسلاسة إلى `1.0` عند التصويب أو الإطلاق، وخفضه إلى `0.0` عند التوقف).
- **قناع الأفاتار (`Upper.mask`)**:
  - تم تعديل القناع لعزل الجزء السفلي كلياً:
    - **مفعل (Included = 1)**: Body, Head, Left Arm, Right Arm, Left Hand, Right Hand, HandIK.
    - **معطل (Excluded = 0)**: Root, Left Leg, Right Leg, FeetIK.
  - هذا التكوين يضمن استمرار حركة الأرجل والجري بكامل حريتها تحت طبقة التصويب دون أن تتجمد الأرجل أثناء المشي وإطلاق النار معاً.
- **الحالات**:
  1. `Empty` (الحالة الافتراضية للطبقة): حالة فارغة بوزن 0 تسمح بمرور حركة الطبقة الأساسية كاملة.
  2. `Aiming`: تشغل كليب وضع التصويب بسلاح اليد الواحدة `Ranged_1H_Aiming.anim`.
  3. `Shoot`: تشغل كليب إطلاق النار `Ranged_1H_Shoot.anim` (كليب غير متكرر مدته 1.0667 ثانية).

### ج) استدعاء الحالات بأسمائها الصريحة (Explicit State Names):
بدلاً من تعقيد الـ Animator بعشرات الـ Triggers، يستخدم `PlayerAnimationController` مسارات الحالات الصريحة والكاش الرقمي لها:
- `Base Layer.Idle` -> `Animator.StringToHash("Base Layer.Idle")`
- `Base Layer.Running` -> `Animator.StringToHash("Base Layer.Running")`
- `Aimming.Empty` -> `Animator.StringToHash("Aimming.Empty")`
- `Aimming.Aiming` -> `Animator.StringToHash("Aimming.Aiming")`
- `Aimming.Shoot` -> `Animator.StringToHash("Aimming.Shoot")`

ويتم الانتقال إليها برمجياً باستخدام `Animator.CrossFadeInFixedTime` بأزمنة خفوت سريعة ومحسوبة لمنع وميض الحالات (State Flicker).

---

## 4. الحركة الاتجاهية أثناء التصويب (Directional Locomotion & Strafing)

### أ) الكليبات المستخدمة في شجرة المزج (2D Blend Tree):
تم فحص أصول KayKit المضمنة في المشروع، وتم العثور على حزمة كاملة من كليبات الحركة المتوافقة مع هيكل `Rig_Medium`:
1. **للأمام (Forward)**: `Running_A.anim` عند الإحداثي `(0, 1)`.
2. **للخلف (Backward)**: `Walking_Backwards.anim` عند الإحداثي `(0, -1)`.
3. **المناورة يساراً (Left Strafe)**: `Running_Strafe_Left.anim` عند الإحداثي `(-1, 0)`.
4. **المناورة يميناً (Right Strafe)**: `Running_Strafe_Right.anim` عند الإحداثي `(1, 0)`.

### ب) حساب الاتجاه النسبي لزاوية توجيه الشخصية:
عندما يقوم اللاعب بالتصويب نحو نقطة معينة، تظل الشخصية موجهة نحو هدف التصويب، بينما قد يتحرك اللاعب في أي اتجاه.
يقوم الكود بحساب الاتجاه المحلي للسرعة بالنسبة لتوجيه الشخصية الحالي عبر التحويل المعكوس:

```csharp
Vector3 localDir = transform.InverseTransformDirection(worldVelocity);
float targetX = Mathf.Clamp(localDir.x / referenceMoveSpeed, -1f, 1f);
float targetZ = Mathf.Clamp(localDir.z / referenceMoveSpeed, -1f, 1f);
```

- **التحرك للأمام أثناء التصويب للأمام**: `targetZ = +1, targetX = 0` -> تشغيل كليب الجري للأمام.
- **التحرك للخلف أثناء التصويب للأمام**: `targetZ = -1, targetX = 0` -> تشغيل كليب المشي للخلف (`Walking_Backwards`).
- **المناورة يساراً**: `targetZ = 0, targetX = -1` -> تشغيل كليب المناورة لليسار (`Running_Strafe_Left`).
- **المناورة يميناً**: `targetZ = 0, targetX = +1` -> تشغيل كليب المناورة لليمين (`Running_Strafe_Right`).
- **الحركة القطرية (Diagonal)**: يمزج الـ Blend Tree بسلاسة بين الكليبات الاتجاهية.

---

## 5. منطق وانتقالات التصويب وإطلاق النار (Aiming & Shooting Transitions)

المكون المسؤول: `PlayerAnimationController.cs`

### متى تصبح حالة التصويب نشطة؟
- **على أجهزة الكمبيوتر (Desktop)**: عند الضغط باستمرار على زر الفأرة الأيمن (Right Mouse Button) أو زناد التحكم الأيسر (Left Trigger).
- **على الجوال (Mobile)**: عند سحب أو تجهيز عصا التصويب والإطلاق `AimFireJoystick` (عبر `inputReader.IsMobileAiming || inputReader.IsMobileArmed`).
- يتم إرسال حالة التصويب عبر متغير شبكي `isAimingNetwork` ليعلم بها السيرفر وجميع اللاعبين الآخرين.

### الحالات الأربعة لانتقال إطلاق النار (Part G Cases):

#### الحالة الأولى (Case A): استمرار اللاعب بالتصويب أثناء إطلاق النار:
1. طبقة `Aimming` في حالة `Aiming` بوزن 1.0.
2. يتم قبول طلقة صالحة من نظام السلاح.
3. تشغل الدالة `PlayShootAnimationInternal()` حالة `Aimming.Shoot` فوراً.
4. يكتمل كليب الإطلاق (عند وصول `normalizedTime >= 0.90` أو انتهاء مدة الكليب 1.0667 ثانية).
5. نظراً لأن اللاعب ما زال مصوباً (`isAiming == true`)، تستدعي الدالة `activeAnimator.CrossFadeInFixedTime("Aimming.Aiming", 0.12f, upperLayerIndex)`.
6. يعود اللاعب لوضعية التصويب بسلاسة وتظل الطبقة بوزن 1.0.

#### الحالة الثانية (Case B): إفلات اللاعب لزر التصويب أثناء تشغيل كليب الإطلاق:
1. تم إطلاق رصاصة وبدأ كليب `Aimming.Shoot`.
2. أفلت اللاعب زر التصويب أثناء تشغيل كليب الإطلاق وقبل انتهائه.
3. يُسمح لكليب الإطلاق القصير بإنهاء حركته السريعة.
4. عند انتهاء الكليب وفحص حالة التصويب يتبين أنها توقفت (`isAiming == false`).
5. يتم خفض وزن الطبقة العلوية بسلاسة إلى `0.0` ونقل الطبقة إلى `Aimming.Empty`.
6. تنكشف حركة الطبقة الأساسية بالكامل بحسب حركة اللاعب الحالية.

#### الحالة الثالثة (Case C): اللاعب متوقف عن الحركة ولا يصوب:
- الطبقة الأساسية تشغل `Base Layer.Idle`.
- الطبقة العلوية في `Aimming.Empty` بوزن 0.0.

#### الحالة الرابعة (Case D): اللاعب يجري ولا يصوب:
- الطبقة الأساسية تشغل `Base Layer.Running` (شجرة الحركة).
- الطبقة العلوية بوزن 0.0.
- أرجل وجسد اللاعب يواصلان الجري الطبيعي دون أي إجبار على حالة الوقوف `Idle`.

---

## 6. تثبيت السلاح في اليد اليمنى (Weapon Attachment & Offsets)

### أ) نقطة التثبيت (Socket Hierarchy):
تحتوي شخصيات KayKit على عظم مخصص للمقبض في اليد اليمنى اسمه:
`handslot.r` متفرع من `hand.r` -> `lowerarm.r` -> `upperarm.r`.

تبحث دالة `FindHandSocket()` بالتسلسل التالي:
1. المقبس المخصص المسجل في `customHandSocket` (إن وجد).
2. البحث التلقائي عن عظم بالاسم `handslot.r`.
3. استعلام Humanoid الرسمي `animator.GetBoneTransform(HumanBodyBones.RightHand)`.
4. البحث التلقائي عن عظم بالاسم `hand.r`.

### ب) ضبط الإزاحات لكل شخصية (Per-Character Offsets):
تم تضمين هيكل `CharacterVisualConfig` داخل `PlayerVisualManager` في `Player.prefab`:
- `ActionFigure`:
  - Position Offset: `(0, 0, 0)`
  - Rotation Offset: `(0, 0, 0)`
  - Scale Offset: `(1, 1, 1)`
- `Driver`:
  - Position Offset: `(0, 0, 0)`
  - Rotation Offset: `(0, 0, 0)`
  - Scale Offset: `(1, 1, 1)`
- `SpaceRanger`:
  - Position Offset: `(0, 0, 0)`
  - Rotation Offset: `(0, 0, 0)`
  - Scale Offset: `(1, 1, 1)`
- `Survivalist`:
  - Position Offset: `(0, 0, 0)`
  - Rotation Offset: `(0, 0, 0)`
  - Scale Offset: `(1, 1, 1)`

*ملاحظة للمطور*: يمكن تعديل هذه القيم بدقة متناهية من نافذة الـ Inspector على الـ Prefab إذا رغبت في إمالة المسدس أو تقديمه بمقدار ملليمترات إضافية.

### ج) موضع الـ FirePoint وشعاع الليزر:
- كائن `FirePoint` هو ابن مباشر لجذر السلاح `Weapon`.
- عندما يتم نقل السلاح إلى مقبس يد الشخصية، يظل `FirePoint` محافظاً على موضعه المحلي عند فوهة المسدس (`localPosition: (0, 0.128, 0.584)`).
- تستدعي دالة التثبيت `playerShooting.SetMuzzlePoint(firePoint)` لتحديث مرجع فوهة الإطلاق في نظام الرصاص والليزر فورياً.

---

## 7. المزامنة الشبكية بين اللاعبين (Multiplayer Synchronization)

### أ) مزامنة مجسم الشخصية:
- حقل `selectedCharacterIndex` عبارة عن `NetworkVariable<int>`.
- عند تغير القيمة، يلتقط جميع العملاء الحدث `OnValueChanged` ويستدعون `ApplyCharacterVisual(newIndex)`.

### ب) مزامنة التصويب:
- حقل `isAimingNetwork` عبارة عن `NetworkVariable<bool>`.
- يتحكم المالك بقيمته بناءً على مدخلاته المحلية.
- يقرأ جميع اللاعبين الآخرين هذه القيمة في دوال `UpdateUpperBody()` لتفعيل حركة التصويب العلوية للاعب البعيد بنفس السلاسة.

### ج) مزامنة حركة الأقدام للاعبين الآخرين (Remote Locomotion):
- نظراً لأن اللاعبين البعيدين لا يمتلكون `CharacterController` نشط محلياً، يقوم `PlayerAnimationController` بحساب فرق الإزاحة الحركية `positionDelta = transform.position - lastWorldPosition` الناتجة عن مزامنة `NetworkTransform`.
- يتم تنعيم السرعة عبر `Vector3.Lerp` وتطبيق `transform.InverseTransformDirection`، مما يجعل جميع العملاء يرون اللاعبين الآخرين يجرون ويناورون اتجاهياً بدقة وانسيابية تامة.

### د) مزامنة إطلاق النار ومنع التكرار (Zero-Latency Prediction & No Double-Play):
1. **المالك (Local Owner)**:
   - عند الضغط على زر الإطلاق وقبول الطلقة محلياً، يستدعي كود `PlayerShooting`:
     ```csharp
     animationController.TriggerLocalShoot();
     ```
   - يبدأ كليب الإطلاق فوراً دون انتظار استجابة السيرفر لمنح اللاعب شعوراً فورياً بالاستجابة.
2. **السيرفر (Server Authoritative)**:
   - عند استلام `FireServerRpc` والتأكد من صحة الطلقة، يستدعي السيرفر:
     ```csharp
     animationController.PlayShootClientRpc();
     ```
3. **العملاء البعيدون (Remote Clients)**:
   - تحتوي الدالة `PlayShootClientRpc()` على فحص ذكي:
     ```csharp
     if (IsOwner) return; // المالك قام بتشغيل الكليب مسبقاً!
     PlayShootAnimationInternal();
     ```
   - هذا يمنع تشغيل كليب الإطلاق مرتين على جهاز المالك، بينما يضمن رؤية جميع الخصوم للطلقة وحركتها في نفس اللحظة.

---

## 8. دليل حل المشاكل الشائعة (Troubleshooting Guide)

| المشكلة | السبب المحتمل | الحل المعتمد في النظام |
| :--- | :--- | :--- |
| **ظهور الشخصيات الأربعة معاً** | عدم تشغيل كود توزيع الشخصيات أو تعطيل الـ Script. | يقوم `PlayerVisualManager.ApplyCharacterVisual` بتعطيل المجسمات الثلاثة الأخرى كلياً فور البداية. |
| **حصول لاعبين على نفس الشخصية** | اختيار المجسم محلياً وعشوائياً على كل جهاز. | تم حصر تعيين المجسم بالسيرفر فقط عبر `AssignDistinctCharacterIndex` مع فحص فهارس اللاعبين الآخرين. |
| **السلاح لا يتبع يد الشخصية** | بقاء كائن `Weapon` طفلاً لـ `Visuals` في المشهد. | يستدعي `PlayerVisualManager` دالة `AttachWeaponToHand` لربط السلاح بمقبس `handslot.r` برمجياً. |
| **حركة الأرجل تتجمد أثناء التصويب** | قناع الـ Avatar يتضمن عظام الحوض والأرجل. | تم تفريغ الأرجل والجذور من قناع `Upper.mask` وتفعيل الجزء العلوي فقط (الصدر، الذراعين، الرأس). |
| **حركة الإطلاق لا تعود لوضع التصويب** | عدم معالجة انتهاء كليب الإطلاق أو وجود Trigger معلق. | تفحص دالة `CheckShootAnimationCompletion` نسبة اكتمال الكليب وتعود إلى `Aiming` إذا كان اللاعب ما زال مصوباً. |
| **استمرار وضعية التصويب بعد إفلات الزر** | قراءة موقع الفأرة كمدخل للتصويب بالخطأ. | تم تخصيص فحص التصويب لزر الفأرة الأيمن فقط أو عصا الجوال المشدودة، ولا يتم الاعتماد على حركة الفأرة العادية. |
| **تكرار كليب الإطلاق مرتين على المالك** | استلام المالك للـ ClientRpc وإعادة تشغيل الكليب. | تم وضع شرط `if (IsOwner) return;` داخل `PlayShootClientRpc` لمنع التكرار التنبؤي. |
| **السلاح يختفي أو ينفصل بعد الإحياء** | إعادة تفعيل كائن اللاعب دون استعادة الروابط. | يشترك `PlayerVisualManager` في حدث `playerHealth.OnRespawn` ويعيد تثبيت السلاح في اليد تلقائياً. |

---

## 9. قائمة الملفات التي تم إنشاؤها أو تعديلها (Implemented Files)

1. `Assets/_Multiplayer/Players/Scripts/PlayerVisualManager.cs` (جديد)
2. `Assets/_Multiplayer/Players/Scripts/PlayerAnimationController.cs` (جديد)
3. `Assets/_Multiplayer/Scripts/Lobby/Editor/MultiplayerAnimationSetupEditor.cs` (جديد - أداة الإعداد التلقائي)
4. `Assets/_Multiplayer/Players/Animation/MultiplayerPlayer.controller` (معدل بالـ 2D Blend Tree وطبقة التصويب العلوية)
5. `Assets/_Multiplayer/Players/Animation/Upper.mask` (معدل لعزل الأرجل وتخصيص الجزء العلوي فقط)
6. `Assets/_Multiplayer/Players/Scripts/Combat/PlayerShooting.cs` (معدل لربط فوهة السلاح وحركات الإطلاق التنبؤية والشبكية)
7. `Assets/_Multiplayer/Players/Scripts/Combat/PlayerMelee.cs` (معدل لربط حركات المشاجرة الشبكية)
8. `Assets/_Multiplayer/Players/Prefabs/Player.prefab` (معدل بدمج المكونات وتوصيل المراجع)
9. `MULTIPLAYER_ANIMATION_SETUP_GUIDE_AR.md` (هذا الدليل الشامل في جذر المشروع)
