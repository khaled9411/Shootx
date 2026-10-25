# دليل نظام تركيب وتجهيز الأسلحة للاعب الفردي (Single-Player Weapon Attachment Guide)

تم إعداد هذا التوثيق الشامل والمفصل لتوضيح البنية الكاملة لنظام تجهيز الأسلحة وتبديل الشخصيات في طور اللعب الفردي (Single Player) لمشروع **ShootX** في محرك Unity 6.3.

---

## 1. الهيكلية الفعلية لبريفاب اللاعب (Player Prefab Hierarchy)

يقع البريفاب الفعلي للاعب الفردي في المسارين المتطابقين:
- `Assets/Prefabs/Player.prefab`
- `Assets/Resources/Prefabs/Player.prefab`

الهيكلية الفعلية الدقيقة للبريفاب:

```text
Player (Root GameObject - يحتوي على Rigidbody, CapsuleCollider, PlayerMovement, ShootingSystem, PlayerEquipmentApplier)
├── Models (Transform الحاوي لجميع نماذج الشخصيات الـ 16)
│   ├── MainCharacter (النموذج الافتراضي الأصلي - هيكل Humanoid مخصص)
│   ├── RangerCharacter (نموذج KayKit Adventurers)
│   ├── Barbarian (نموذج KayKit Adventurers)
│   ├── Druid (نموذج KayKit Adventurers)
│   ├── Knight (نموذج KayKit Adventurers)
│   ├── Rogue_Hooded (نموذج KayKit Adventurers)
│   ├── Animatronic_Creepy (نموذج KayKit Spook)
│   ├── Animatronic_Normal (نموذج KayKit Spook)
│   ├── Clown (نموذج KayKit Spook)
│   ├── Monster (نموذج KayKit Spook)
│   ├── MonsterCostume (نموذج KayKit Spook)
│   ├── Paladin (نموذج KayKit Adventurers)
│   ├── Paladin_with_Helmet (نموذج KayKit Adventurers)
│   ├── Survivalist (نموذج KayKit Adventurers - الزي الافتراضي المملوك)
│   ├── Werewolf_Man (نموذج KayKit Spook)
│   └── Werewolf_Wolf (نموذج KayKit Spook)
├── FirePoint (Transform الاحتياطي الساكن لإطلاق النار خارج Models - تم الحفاظ عليه)
├── Model (نموذج بصري تجريبي قديم غير مفعّل IsActive: 0 - تم الحفاظ عليه)
├── Line (يحتوي على LineRenderer لنظام التصويب الليزري في ShootingSystem - تم الحفاظ عليه)
└── Move Smoke VFX (نظام الجسيمات ParticleSystem لغبار حركة اللاعب في PlayerMovement - تم الحفاظ عليه)
```

> **ملاحظة حول الكائنات الخارجية:**
> - تم الحفاظ على الكائنات `FirePoint` و `Model` و `Line` و `Move Smoke VFX` بدون تعديل أماكنها أو حذفها لتفادي كسر أي مرجع خارجي، حيث يتم تحديث `WeaponFirePoint` برمجياً في `ShootingSystem` ليشير إلى الفوهة الحقيقية للسلاح المجهز ديناميكياً.

---

## 2. جدول نماذج الشخصيات الفعلية (All 16 Models under Models/)

| # | اسم النموذج (GameObject Name) | نوع الهيكل (Rig Type) | مقبس اليد اليمنى (Right Hand Socket) | معرّف الزي المتجر (Store Outfit ID) | الرقم التسلسلي (Numeric ID) |
|---|-----------------------------|----------------------|-----------------------------------|-----------------------------------|----------------------------|
| 1 | `MainCharacter` | Custom Humanoid | `Wrist_R` | الافتراضي الأساسي (Default) | - |
| 2 | `RangerCharacter` (أو `Ranger`) | KayKit Adventurers | `handslot.r` | `outfit_ranger` | 3 |
| 3 | `Barbarian` | KayKit Adventurers | `handslot.r` | متاح للتجهيز بالاسم | - |
| 4 | `Druid` | KayKit Adventurers | `handslot.r` | `outfit_druid` | 5 |
| 5 | `Knight` | KayKit Adventurers | `handslot.r` | `outfit_knight` | 6 |
| 6 | `Rogue_Hooded` | KayKit Adventurers | `handslot.r` | `outfit_rogue_hooded` | 4 |
| 7 | `Animatronic_Creepy` | KayKit Spook | `handslot.r` | `outfit_animatronic_creepy` | 12 |
| 8 | `Animatronic_Normal` | KayKit Spook | `handslot.r` | `outfit_animatronic_normal` | 9 |
| 9 | `Clown` | KayKit Spook | `handslot.r` | `outfit_clown` | 2 |
| 10 | `Monster` | KayKit Spook | `handslot.r` | `outfit_monster` | 7 |
| 11 | `MonsterCostume` | KayKit Spook | `handslot.r` | `outfit_monster_costume` | 1 |
| 12 | `Paladin` | KayKit Adventurers | `handslot.r` | `outfit_paladin` | 8 |
| 13 | `Paladin_with_Helmet` | KayKit Adventurers | `handslot.r` | `outfit_paladin_with_helmet` | 11 |
| 14 | `Survivalist` | KayKit Adventurers | `handslot.r` | `outfit_survivalist` | 0 (افتراضي مملوك) |
| 15 | `Werewolf_Man` | KayKit Spook | `handslot.r` | متاح للتجهيز بالاسم | - |
| 16 | `Werewolf_Wolf` | KayKit Spook | `handslot.r` | `outfit_werewolf_wolf` | 10 |

---

## 3. أصول وقائمة الأسلحة في المتجر (`Store/Weapons`)

تتطابق قاعدة بيانات المتجر `StoreCatalogDatabase` مع الأصول المتواجدة في مجلد `Assets/Store/Weapons` و `Assets/Data/Store/Weapons`:

| الرقم (ID) | معرّف السلاح (Item ID) | الاسم الظاهر (Display Name) | أصل البريفاب (Prefab Asset) | إزاحة الفوهة الافتراضية (Muzzle Offset) |
|-----------|------------------------|-----------------------------|----------------------------|-----------------------------------------|
| 0 | `wep_pistol_01` | Standard Pistol | `SM_Wep_Pistol_01.prefab` | `(0, 0.08, 0.28)` |
| 1 | `wep_revolver_01` | Heavy Revolver | `SM_Wep_Revolver_01.prefab` | `(0, 0.09, 0.32)` |
| 2 | `wep_machine_pistol_gen1` | Machine Pistol Gen 1 | `SM_Wep_MachinePistol_Gen1.prefab` | `(0, 0.08, 0.35)` |
| 3 | `wep_machine_pistol_gen2` | Machine Pistol Gen 2 | `SM_Wep_MachinePistol_Gen2.prefab` | `(0, 0.08, 0.35)` |
| 4 | `wep_rifle_small_01` | Tactical Compact Rifle | `SM_Wep_Rifle_Small_01.prefab` | `(0, 0.08, 0.50)` |
| 5 | `wep_smg_01` | Rapid SMG | `SM_Wep_SMG_01.prefab` | `(0, 0.08, 0.42)` |
| 6 | `wep_rifle_base_01` | Assault Rifle | `SM_Wep_Rifle_Base_01.prefab` | `(0, 0.08, 0.70)` |
| 7 | `wep_syringe_gun_01` | Bio Syringe Gun | `SM_Wep_SyringeGun_01.prefab` | `(0, 0.08, 0.38)` |
| 8 | `wep_sniper_01` | Marksman Sniper Rifle | `SM_Wep_Sniper_01.prefab` | `(0, 0.09, 0.98)` |
| 9 | `wep_rifle_laser_01` | High-Tech Laser Rifle | `SM_Wep_Rifle_Laser_01.prefab` | `(0, 0.08, 0.65)` |
| 10 | `wep_rifle_plasma_01` | Plasma Rifle Alpha | `SM_Wep_Rifle_Plasma_01.prefab` | `(0, 0.08, 0.68)` |
| 11 | `wep_rifle_plasma_02` | Plasma Rifle Beta | `SM_Wep_Rifle_Plasma_02.prefab` | `(0, 0.08, 0.70)` |
| 12 | `wep_rifle_plasma_03` | Plasma Rifle Omega | `SM_Wep_Rifle_Plasma_03.prefab` | `(0, 0.08, 0.72)` |
| 13 | `wep_sniper_plasma_02` | Heavy Plasma Sniper | `SM_Wep_Sniper_Plasma_02.prefab` | `(0, 0.09, 0.95)` |

---

## 4. كيفية تحديد وتفعيل الشخصية النشطة (Active Character Resolution)

تتم إدارة الشخصية النشطة عبر كلاس `PlayerEquipmentApplier.cs` من خلال الدالة `ApplyOutfit(int outfitId)`:

1. **الاستعلام عن الزي**: يتم جلب تعريف الزي من `StoreCatalogDatabase.Instance.GetOutfitByNumericId(outfitId)`.
2. **مطابقة النموذج (Matching Hierarchy)**:
   - **الاستراتيجية الأولى**: مطابقة اسم البريفاب الأصلي `outfitDef.itemPrefab.name` مع أبناء `Models/`.
   - **الاستراتيجية الثانية**: مطابقة المعرّف النصي بعد إزالة البادئات (مثل تحويل `outfit_ranger` إلى `ranger` ومطابقته مع `RangerCharacter`).
   - **الاستراتيجية الثالثة**: مطابقة الاسم الظاهر `displayName`.
   - **الاستراتيجية الرابعة**: استخدام الشخصية الافتراضية للرقم 0 وهو `Survivalist`، وفي حال عدم العثور عليها يتم الانتقال إلى `MainCharacter`.
3. **تفعيل فردي حصري (Exclusive Activation)**: يتم تفعيل GameObject الشخصية المحددة فقط، ويتم **تعطيل جميع الأبناء الـ 15 الآخرين فوراً** داخل حلقة واحدة لضمان عدم وجود أكثر من شخصية ظاهرة في نفس الوقت.
4. **تحديث الـ Animator**: يتم استخراج مكوّن `Animator` من الشخصية النشطة وتعيينه تلقائياً لكل من:
   - `ShootingSystem.PlayerAnimator`
   - `PlayerMovement.PlayerAnimator`
   - تشغيل أنيميشن `"Idle"`.

---

## 5. تحديد عظمة أو مقبس اليد اليمنى (Right-Hand Socket Assignment)

تقوم الدالة `ResolveHandSocket(Transform characterModel, string characterName, string socketOverride)` بالبحث وفق تسلسل أولويات دقيق:

1. **مقبس مخصص من الزي (Custom Socket Override)**: إذا تم تحديد `customSocketName` داخل بطاقة الزي `StoreItemDefinition`.
2. **إعدادات البروفايل (Attachment Profile)**: قراءة المقبس المسجل للشخصية في الأصل `SinglePlayerWeaponAttachmentProfile.asset`.
3. **مقبس KayKit الأصلي (`handslot.r`)**: وهو المقبس المتخصص المدمج في هياكل جميع شخصيات حزمة KayKit الـ 15، وهو معد تماماً لتركيب الأسلحة.
4. **عظمة Humanoid الأصلية**: استدعاء `Animator.GetBoneTransform(HumanBodyBones.RightHand)` (كما في `MainCharacter`).
5. **البحث الاحتياطي المتقدم**: البحث عن عظام تحمل أسماء شائعة (`Wrist_R`, `hand.r`, `RightHand`).

---

## 6. ضبط إزاحات الأسلحة والشخصيات (Attachment Offsets Configuration)

تم إنشاء نظام بروفايل بياني كامل قابل للضبط من محرر Unity بدون الحاجة لكتابة كود:
- **الملف البرمجي**: `Assets/Scripts/Store/SinglePlayerWeaponAttachmentProfile.cs`
- **الأصل في Resources**: `Assets/Resources/SinglePlayerWeaponAttachmentProfile.asset`

### إمكانيات الضبط:
1. **إعدادات الشخصية (`CharacterAttachmentSetting`)**:
   - `characterName`: اسم الشخصية (مثل `MainCharacter` أو `Knight`).
   - `socketName`: اسم العظمة أو المقبس (مثل `Wrist_R` أو `handslot.r`).
   - `positionOffset`, `rotationOffset`, `scale`.
2. **إعدادات السلاح (`WeaponAttachmentSetting`)**:
   - `weaponId`: معرّف السلاح (مثل `wep_pistol_01` أو `wep_rifle_base_01`).
   - `characterName`: اسم الشخصية المخصصة أو `"All"` لتطبيقه على كل الشخصيات.
   - `positionOffset`, `rotationOffset`, `scale`, `muzzleOffset`.
3. **التجاوز المباشر على مستوى السلاح (`StoreItemDefinition`)**:
   - حقول إضافية في مفتش Unity (Inspector):
     - `attachmentPositionOffset`
     - `attachmentRotationOffset`
     - `attachmentScale`
     - `muzzleOffset`

---

## 7. آلية تبديل الأسلحة (Weapon Switching Mechanism)

عند استدعاء `ApplyWeapon(int gunId)`:
1. يتم جلب تعريف السلاح من قاعدة البيانات والتأكد من وجود البريفاب الخاص به.
2. يتم التأكد من أن الشخصية النشطة ومقبس يدها اليمنى محددين مسبقاً.
3. يتم استدعاء `CleanupSpawnedWeapon()` و `CleanupOrphanedWeaponsOnSocket()` للتخلص الفوري والآمن من أي نسخة سلاح سابقة على المقبس، مما يضمن **عدم وجود أي أسلحة مكررة (Zero Duplicates)**.
4. يتم استنساخ بريفاب السلاح كابن مباشر لمقبس اليد اليمنى للشخصية النشطة:
   ```csharp
   _currentSpawnedWeapon = Instantiate(weaponDef.itemPrefab, _activeHandSocket);
   ```
5. يتم تطبيق الإزاحات (Position, Rotation, Scale) المستخرجة من البروفايل.
6. يتم إنشاء أو تحديث نقطة الفوهة `Muzzle` وربطها بنظام الإطلاق.
7. يتم تحديث المتغيرات الداخلية لمعرّف السلاح الحالي `_currentGunId` و `_activeWeaponDefId`.

---

## 8. آلية تبديل الشخصيات والحفاظ على السلاح الحالي (Character Switching & Weapon Preservation)

عندما يختار اللاعب شخصية جديدة:
1. يتم تفعيل الشخصية الجديدة وتعطيل النماذج الأخرى.
2. يتم تحديد مقبس اليد اليمنى للشخصية الجديدة.
3. **نقل السلاح الحالي بسلاسة (Reparenting)**:
   إذا كان هناك سلاح مجهز ومستنسخ مسبقاً (`_currentSpawnedWeapon != null`)، تقوم الدالة `AttachWeaponToHand` بما يلي:
   ```csharp
   weaponObj.transform.SetParent(newHandSocket, false);
   ApplyTransformOffsets(weaponObj.transform, newCharacterName, activeWeaponId, weaponDef);
   ResolveAndBindMuzzle(weaponObj, activeWeaponId, newCharacterName, weaponDef);
   ```
4. **النتيجة**: ينتقل نفس السلاح المجهز إلى يد الشخصية الجديدة فوراً مع تطبيق الإزاحات المخصصة لها، **دون إعادة تعيين السلاح إلى السلاح الافتراضي ودون حذف السلاح وإعادة استنساخه دون داعٍ**.
5. يتم تحديث مراجع الـ Animator على أنظمة الحركة والإطلاق للشخصية الجديدة.

---

## 9. فصل معاينة المتجر عن التجهيز الفعلي (Store Preview vs Gameplay Loadout)

تم ضبط كلاس `StorePreviewManager.cs` ليعمل في بيئة معزولة تماماً:
- **مسرح المعاينة المعزول (`StorePreviewStage`)**: يتواجد في موضع بعيد جداً عن بيئة اللعب عند النقطة `Vector3(0, -500, 0)`.
- **كاميرا منفصلة وملمس إخراج (RenderTexture)**: تعرض المعاينة داخل نافذة واجهة المستخدم (`PlayerPreview.renderTexture`) دون التأثير على كاميرا اللعبة الرئيسية.
- **تجريد الكائن من المكونات الفيزيائية والحركية**: كائن المعاينة يحتوي على نموذج الشخصية و `PlayerEquipmentApplier` البصري فقط، وتمت إزالة `ShootingSystem`, `Rigidbody`, `Collider`.
- **المعاينة المؤقتة (Preview Only)**:
  - استدعاء `ShowOutfitPreview(outfitId)` أو `ShowWeaponPreview(gunId)` يغير المظهر المرئي لكائن المعاينة فقط.
  - لا يتم خصم عملات، ولا يتم منح ملكية، ولا يتم تعديل حمولة اللعب الفعلية المحفوظة في `GameDataManager`.
- **إغلاق المعاينة (`ClosePreview`)**:
  - عند إغلاق واجهة المتجر، يتم استدعاء `_previewApplier.ApplyCurrentLoadout()` لإعادة كائن المعاينة فوراً إلى الحمولة الفعلية المحفوظة للاعب.
- **التجهيز الفعلي (Actual Equip)**:
  - يحدث التجهيز فقط عند ضغط اللاعب على زر التجهيز "Equip" في `GunsPanelController` أو `OutfitsPanelController`، والذي يقوم باستدعاء:
    ```csharp
    GameDataManager.Instance.EquipGun(gunId);
    // أو
    GameDataManager.Instance.EquipOutfit(outfitId);
    ```
  - عندئذ يطلق `GameDataManager` الحدث `OnEquipmentChanged` الذي يستمع له كائن اللاعب الحقيقي في المشهد ليطبق التغيير رسمياً ويحفظه.

---

## 10. التحديد الديناميكي لفوهة السلاح ونقطة إطلاق النار (Muzzle / FirePoint Dynamic Resolution)

في طور اللعب الفردي، كان الكائن `FirePoint` ثابتاً في جذر اللاعب، مما يجعله لا يتحرك مع يد اللاعب أو فوهة السلاح عند تشغيل الأنيميشن والتصويب.

تم حل ذلك ديناميكياً عبر `ResolveAndBindMuzzle`:
1. **البحث الداخلي في السلاح**: يبحث أولاً عن أي ابن داخل بريفاب السلاح يحمل اسم `Muzzle` أو `FirePoint` أو `nozzle`.
2. **الإنشاء التلقائي للفوهة**: إذا لم يوجد ابن للفوهة، يقوم الكود بإنشاء كائن ابن جديد باسم `Muzzle` على السلاح المستنسخ ويضعه في موضع الفوهة المناسب (المأخوذ من بروفايل السلاح أو المحسوب تلقائياً من أبعاد الـ Mesh عبر `CalculateMuzzlePositionFromBounds`).
3. **تحديث نظام الإطلاق (`ShootingSystem`)**:
   ```csharp
   shootingSystem.WeaponFirePoint = muzzleTransform;
   ```
4. **تزامن زوايا الإطلاق**: تم تحديث الـ Property في `ShootingSystem.cs` بحيث يقوم تلقائياً بإعادة حساب ومزامنة الزوايا الابتدائية والحالية فور إسناد الفوهة الجديدة:
   ```csharp
   public Transform WeaponFirePoint
   {
       get => weaponFirePoint;
       set
       {
           weaponFirePoint = value;
           if (weaponFirePoint != null)
           {
               initialXRotation = NormalizeAngle(weaponFirePoint.localEulerAngles.x);
               currentXRotation = initialXRotation;
           }
       }
   }
   ```
5. **النتيجة**: خروج الرصاص وشعاع الليزر والمؤثرات الضوئية يكون دقيقاً تماماً من فوهة السلاح الحالية في يد الشخصية النشطة.

---

## 11. دورة حياة حفظ واسترجاع التجهيزات (Persistence & Lifecycle)

1. **عند بدء اللعبة أو تحميل المشهد**:
   - يقوم `PlayerEquipmentApplier.Awake()` بتجهيز كافة المراجع والبحث عن `Models/`.
   - يقوم `PlayerEquipmentApplier.Start()` بقراءة الزي المجهز والسلاح المجهز من `GameDataManager`.
   - يتم تطبيق الشخصية والسلاح فوراً قبل تفعيل نظام إطلاق النار.
2. **عند موت اللاعب أو إعادة البناء (Respawn)**:
   - عند إعادة بناء اللاعب أو إعادة تعيينه، يقوم `OnEnable()` باستدعاء `ApplyCurrentLoadout()` تلقائياً لاسترجاع آخر حمولة محفوظة دون أي تكرار للكائنات.
3. **عند تدمير الكائن (`OnDestroy`)**:
   - يتم إلغاء الاشتراك في الحدث `GameDataManager.OnEquipmentChanged -= HandleEquipmentChanged`.
   - يتم تنظيف وحذف أي أسلحة مستنسخة لتفادي تسريب الذاكرة (Memory Leaks).

---

## 12. ملاحظات حول الهياكل والتوافقية (Rig Compatibility & Manual Configuration)

- **شخصيات KayKit الـ 15**:
  - تم فحصها والتأكد من احتوائها جميعاً على مقبس أصلي باسم `handslot.r`.
  - الأسلحة من حزمة Polygon متطابقة بنسبة 100% مع هذا المقبس بدون أي حاجة لتدخل يدوي.
- **شخصية `MainCharacter`**:
  - تستخدم هيكلاً مخصصاً عظمة اليد اليمنى فيه تسمى `Wrist_R`.
  - تم تكوين إزاحة دقيقة لها في ملف البروفايل:
    - الموضع: `(0.05, -0.04, 0.12)`
    - التدوير: `(-90, 0, 90)`
- **إضافة أي شخصية مستقبلية**:
  - إذا تم إدخال شخصية لا تحتوي على `handslot.r` أو ليست Humanoid، يمكن للمطور ببساطة إدخال اسم عظمة اليد اليمنى في حقل `customSocketName` داخل بطاقة الزي المقابل في المتجر، وسيقوم النظام بربط السلاح بها فوراً وبشكل تلقائي.

---

## 13. أداة التحقق التلقائي في المحرر (Automated Verification Tool)

تم إنشاء أداة فحص واختبار متكاملة يمكن تشغيلها بنقرة واحدة من شريط القوائم في محرر Unity:
- **المسار في القائمة**: `ShootX` -> `Store` -> `Verify Weapon Attachment System`
- **الملف البرمجي**: `Assets/Scripts/Store/Editor/SinglePlayerWeaponAttachmentVerifier.cs`

### العمليات التي تفحصها الأداة:
1. التحقق من سلامة وجود ومطابقة بريفاب اللاعب `Player.prefab`.
2. فحص جميع النماذج الـ 16 تحت `Models/` والتأكد من استجابة كل نموذج وإيجاد مقبس اليد اليمنى الخاص به.
3. فحص الأسلحة الـ 14 في المتجر وتركيب كل سلاح والتحقق من إنشاء فوهة `Muzzle` صالحة له.
4. اختبار تبديل الأسلحة المتكرر والتأكد من أن عدد الأبناء في المقبس يظل ثابتاً (0 أسلحة يتيمة أو مكررة).
5. اختبار تبديل الشخصيات والتأكد من بقاء السلاح نفسه في يد الشخصية الجديدة.
6. اختبار تكامل نظام معاينة المتجر `StorePreviewManager`.

---

## 14. قائمة الملفات التي تم إنشاؤها أو تعديلها (Created / Modified Files)

### أ) ملفات تم إنشاؤها (New Files):
1. `Assets/Scripts/Store/SinglePlayerWeaponAttachmentProfile.cs` (كلاس بيانات البروفايل)
2. `Assets/Resources/SinglePlayerWeaponAttachmentProfile.asset` (أصل الإعدادات الافتراضية)
3. `Assets/Scripts/Store/Editor/SinglePlayerAttachmentProfileGenerator.cs` (أداة توليد البروفايل في المحرر)
4. `Assets/Scripts/Store/Editor/SinglePlayerWeaponAttachmentVerifier.cs` (أداة التحقق الشاملة)
5. `Assets/SINGLEPLAYER_WEAPON_ATTACHMENT_GUIDE_AR.md` (هذا الدليل الشامل)

### ب) ملفات تم تحديثها وتطويرها (Modified Files):
1. `Assets/Scripts/Store/PlayerEquipmentApplier.cs` (إعادة بناء شاملة لإدارة الـ 16 شخصية، مقابس الأيدي، الإزاحات، الفوهات، ومنع التكرار)
2. `Assets/Scripts/Store/StoreItemDefinition.cs` (إضافة حقول تجاوز الإزاحات والمقابس في المفتش)
3. `Assets/Scripts/ShootingSystem/ShootingSystem.cs` (دعم تحديث الفوهة ديناميكياً مع مزامنة الزوايا، وتحديث Animator)
4. `Assets/Scripts/MovementSystem/PlayerMovement.cs` (دعم تحديث Animator الشخصية النشطة)
5. `Assets/Scripts/Store/StorePreviewManager.cs` (عزل المعاينة والحفاظ على السلاح واستعادة الحمولة عند الإغلاق)

### ج) تأكيد عزل الطور الجماعي (Multiplayer Isolation Confirmation):
- **لم يتم لمس أو تعديل أي مشهد أو كود خاص بطور اللعب الجماعي** (`MainMultiplayer`, `MultiplayerLobby`, خرائط اللعب الجماعي الأربعة، بريفابات اللعب الجماعي، وسكربتات `_Multiplayer/`). النظام يعمل حصرياً ومستقلاً بنسبة 100% داخل طور اللعب الفردي.
