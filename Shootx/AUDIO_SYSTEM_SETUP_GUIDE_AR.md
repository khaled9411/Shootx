# دليل التشغيل والإعداد الشامل لنظام الصوتيات (Shootx Audio System)

تم بناء وهندسة هذا النظام ليكون **نظاماً صوتياً احترافياً متكاملاً (Production-Ready)** يدعم ألعاب الأونلاين الجماعية (Multiplayer) في بيئة **Unity 6 (6.3)** مع **Netcode for GameObjects (NGO 2.13.3)** وحزم **DOTween Pro** و **Unity Input System**، ومصمم للعمل بكفاءة فائقة على منصات **Android و iOS والحاسوب الشخصي (PC)**.

---

## فهرس الدليل

1. [القسم أ: النظرة المعمارية الشاملة ومخطط النظام (Architecture Overview)](#أ-النظرة-المعمارية-الشاملة)
2. [القسم ب: الملفات والسكربتات المنشأة والمعدلة (Files & Components)](#ب-الملفات-والسكربتات)
3. [القسم ج: طريقة إضافة أو استبدال أي ملف صوتي خطوة بخطوة (Step-by-Step Guide)](#ج-طريقة-إضافة-أو-استبدال-صوت)
4. [القسم د: مواضع وربط الأصوات بدقة على مجسم اللاعب (Player Prefab Audio Placement)](#د-مواضع-الأصوات-على-اللاعب)
5. [القسم هـ: نظام صوتيات واجهات المستخدم والأزرار (UI & Button Audio)](#هـ-صوتيات-واجهات-المستخدم)
6. [القسم و: هيكل مازج الصوتيات ومجموعاته (AudioMixer & Exposed Parameters)](#و-مازج-الصوتيات-audiomixer)
7. [القسم ز: إعدادات المستخدم وحفظ الصوتيات محلياً (Settings & Persistence)](#ز-إعدادات-المستخدم-وحفظ-الصوت)
8. [القسم ح: سياسة المزامنة الصوتية في ألعاب الشبكة (Multiplayer Audio Sync)](#ح-المزامنة-الصوتية-في-الشبكة)
9. [القسم ط: القائمة المرجعية للأصول الصوتية (Audio Asset Checklist)](#ط-القائمة-المرجعية-للأصول-الصوتية)
10. [القسم ي: إعدادات الاستيراد الموصى بها للهواتف (Mobile Import Settings)](#ي-إعدادات-الاستيراد-الموصى-بها)
11. [القسم ك: دليل استكشاف وحل المشكلات الشائعة (Troubleshooting & FAQs)](#ك-استكشاف-الأخطاء-وإصلاحها)

---

## أ. النظرة المعمارية الشاملة

يعتمد النظام الصوتي على مبدأ **فصل المسؤوليات (Separation of Concerns)** والتوجيه المعتمد على البيانات (Data-Driven Architecture) دون وضع أي عبء غير ضروري على شبكة اللعبة أو حشر المنطق في سكربت واحد ضخم:

```text
                               ┌────────────────────────────────┐
                               │       Shootx Audio System      │
                               └────────────────────────────────┘
                                                │
         ┌──────────────────────────────┬───────┴───────────────────────┬──────────────────────────────┐
         ▼                              ▼                               ▼                              ▼
┌───────────────────┐          ┌───────────────────┐          ┌───────────────────┐          ┌───────────────────┐
│   AudioLibrary    │          │   AudioManager    │          │  AudioSourcePool  │          │  MusicController  │
│ (ScriptableObject)│          │ (DontDestroyOnLoad│          │  (Voice Stealing  │          │ (Dual-Source Fader│
│  - SoundCatalog   │◄─────────┤   Service Locator)│─────────►│   48 Voices Max)  │          │   Crossfading)    │
│  - MusicCatalog   │          │  - Volume Settings│          └───────────────────┘          └───────────────────┘
└───────────────────┘          │  - AudioMixer Map │                    ▲
                               └───────────────────┘                    │
                                         │                              │ (Pooled 2D & 3D Playback)
         ┌───────────────────────────────┼──────────────────────────────┴──────────────────────────────┐
         ▼                               ▼                                                              ▼
┌───────────────────┐          ┌───────────────────┐                                          ┌───────────────────┐
│   UI Components   │          │ Gameplay Trigger  │                                          │  Network Players  │
│  - UIButtonAudio  │          │  - DestructibleBox│                                          │ - PlayerAudioCtrl │
│  - SliderFeedback │          │  - AmmoPickup     │                                          │   (Owner Local vs │
│  - AudioSettingsUI│          │  - MatchHUD Audio │                                          │    Remote Spatial)│
└───────────────────┘          └───────────────────┘                                          └───────────────────┘
```

### الركائز الأساسية للنظام:
1. **AudioManager المستمر (`DontDestroyOnLoad`)**:
   - لا يعد `NetworkObject` مطلقاً، لضمان استمراريته عبر كافة المشاهد (`MultiplayerMenu`، `MultiplayerLobby`، `MainMultiplayer`) دون الاعتماد على اتصال الشبكة أو انقطاعه.
   - يدعم التوليد التلقائي الآمن (Self-Bootstrapping)؛ إذا تم استدعاء `AudioManager.Instance` في أي مشهد دون وجود مدير مسبق، فإنه يقوم بتحميل الـ Prefab تلقائياً من مجلد `Resources` وربط المكونات دون حدوث أي خطأ NullReference.
2. **مكتبة الأصوات المركزية (`AudioLibrary`)**:
   - أصل من نوع `ScriptableObject` يحتوي على جميع تعريفات الأصوات (`SoundDefinition`) ومسارات الموسيقى (`MusicTrackDefinition`)، مما يتيح استبدال أو تعديل أي صوت أو رفع مستوى صوته عبر الـ Inspector دون لمس أي سطر كود.
   - يحتوي على جداول بحث برمجية سريعة بنمط `Dictionary<SoundId, SoundDefinition>` توفر استرجاعاً فورياً بزمن $O(1)$.
3. **حوض مصادر الصوت الذكي (`AudioSourcePool`)**:
   - يمنع عمليات التوليد والحذف المستمرة (`Instantiate` و `Destroy`) لتقليل الحمل على Garbage Collector بنسبة 100%.
   - يحدد سقفاً آمناً للأصوات المتزامنة (بحد أقصى 48 صوتاً)، ويطبق خوارزمية استعارة الأصوات الأقدم والأقل أولوية (Voice-Stealing).
   - يعيد تصفير خصائص الصوت (Volume, Pitch, SpatialBlend, Rolloff, Min/Max Distance) تلقائياً عند إرجاع المصدر للحوض.
4. **متحكم الموسيقى المتلاشي (`MusicController`)**:
   - يستخدم مصدرين صوتيين متناوبين لعمل انتقال سلس وانسيابي (Crossfade) بين مسارات الموسيقى أثناء التنقل بين القوائم، اللوبي، ساحة المعركة، وشاشة النتائج.
5. **متحكم صوت اللاعب المستقل (`PlayerAudioController`)**:
   - يحسب إيقاع خطوات الأقدام محلياً على جهاز كل عميل استناداً إلى حركة الشخصية الفيزيائية الفعلية، مما يلغي تماماً الحاجة لإرسال RPCs شبكية للخطوات مع الحفاظ على تزامن كامل.
   - يعزل تغذية المالك الفورية (Zero-Latency Local Feedback) عن الصوت ثلاثي الأبعاد الموجه لبقية اللاعبين (Remote 3D Audio).

---

## ب. الملفات والسكربتات المنشأة والمعدلة

### 1. مجلد الصوتيات الأساسي (`Assets/_Multiplayer/Audio/`)

| المسار والملف | النوع | المسؤولية والوظيفة |
| :--- | :--- | :--- |
| `Scripts/Core/SoundId.cs` | C# Enum | يحوي معرفات جميع أصوات اللعبة (`SoundId`)، تصنيفات المازج (`SoundCategory`)، ومسارات الموسيقى (`MusicTrackId`). |
| `Scripts/Core/SoundDefinition.cs` | C# Class | فئة قابلة للتسلسل تعرّف الصوت: المقاطع البديلة، مستوى الصوت، عشوائية النبرة (Pitch)، الأبعاد (2D/3D)، مسافات التلاشي، وسقف التكرار. |
| `Scripts/Core/AudioLibrary.cs` | ScriptableObject | الكتالوج المركزي الحاوي لجميع الأصوات والموسيقى مع دوال الفحص والتحقق (`ValidateLibrary`). |
| `Scripts/Core/AudioSourcePool.cs` | MonoBehaviour | حوض تشغيل الصوتيات عالي الأداء مع إدارة القنوات الصوتية وتتبع الأهداف المتحركة. |
| `Scripts/Core/MusicController.cs` | MonoBehaviour | إدارة الموسيقى التصويرية وتطبيق التلاشي التدريجي (Crossfade) بين المشاهد وحالات المباراة. |
| `Scripts/Core/AudioManager.cs` | MonoBehaviour | المدير المركزي للنظام؛ يربط المازج، يحفظ ويفعّل إعدادات المستخدم، ويضمن وجود `AudioListener` واحد فقط. |
| `Scripts/UI/UIButtonAudio.cs` | MonoBehaviour | تشغيل أصوات النقر والتفاعل على الأزرار دون التأثير على وظائف `Button.onClick` القائمة. |
| `Scripts/UI/SliderAudioFeedback.cs` | MonoBehaviour | تشغيل صوت نبضات خفيف ومضبوط زمنياً (Throttled) عند تحريك أشرطة التمرير. |
| `Scripts/UI/AudioSettingsUI.cs` | MonoBehaviour | لوحة إعدادات الصوت الكاملة (Master, Music, SFX, UI, Announcer, Mute) مع رسوم DOTween. |
| `Scripts/Gameplay/PlayerAudioController.cs` | NetworkBehaviour | ملحق بمجسم اللاعب للتحكم بخطوات الأقدام، إطلاق النار، الاشتباك اليدوي، الاندفاع، الضرر، والموت. |
| `Scripts/Gameplay/BoxAudio.cs` | MonoBehaviour | ربط أصوات اصطدام الرصاص وتكسر الصناديق المتزامنة. |
| `Scripts/Gameplay/AmmoPickupAudio.cs` | MonoBehaviour | تشغيل صوت التقاط الذخيرة ثلاثي الأبعاد في العالم وتغذية الملتقط المباشرة. |
| `Scripts/Gameplay/MatchAudioController.cs` | MonoBehaviour | ربط أصوات عداد البداية، شارة الانطلاق (GO)، تحذير الـ 30 ثانية، وموسيقى النصر والهزيمة. |
| `Scripts/Editor/AudioSystemSetupUtility.cs` | Editor Script | أداة المحرر الذكية في القائمة العلوية (`Tools/Shootx/Audio/...`) لبناء النظام وربط الأصول تلقائياً. |
| `Mixers/GameAudioMixer.mixer` | AudioMixer | مازج الصوتيات الموحد المحتوي على مجموعات الصوت والمتغيرات البرمجية المجهزة. |

### 2. السكربتات القائمة التي تم ربط الصوتيات بها بمرونة:
- **`PlayerShooting.cs`**: تم تزويده بتغذية صوتية فورية للنقر الجاف عند نفاد الذخيرة (`DryFire`)، صوت الإطلاق التنبؤي للمالك، أصوات الارتداد عن الجدران (`Ricochet`)، وصوت رنين استرداد الذخيرة (`FairyBell`).
- **`PlayerAnimationController.cs`**: تشغيل صوت الإطلاق ثلاثي الأبعاد للعملاء البعيدين عند استقبال `PlayShootClientRpc` مع تجاهل المالك لتفادي التكرار.
- **`PlayerMelee.cs`**: تشغيل صوت تلويح السلاح اليدوي ثلاثي الأبعاد للأطراف البعيدة عبر `MeleeVisualsClientRpc`.
- **`PlayerDash.cs`**: تشغيل صوت الهواء والاندفاع للأطراف البعيدة عبر `DashVisualClientRpc`، وتشغيل صوت ارتطام الجدار عند الاصطدام بعائق.
- **`DestructibleBox.cs`**: تشغيل صوت إصابة الرصاصة في الصندوق عند `TakeDamageWithHitInfo`، وصوت الانفجار والتكسر في مرحلة التدمير.
- **`AmmoPickup.cs`**: تشغيل صوت التقاط العملة/الذخيرة في الموقع ثلاثي الأبعاد عبر `PlayCollectionFeedbackClientRpc`.
- **`MobileDashButton.cs`**: تشغيل طنين الرفض الخفيف (`UI_ActionFailed`) عند نقر زر الاندفاع أثناء وجوده في فترة التهدئة (Cooldown).
- **`MatchHUD.cs`**: إضافة `MatchAudioController` تلقائياً، وربط صوت تأكيد القتل (`KillConfirmed`) وبدء/انتهاء درع الحماية المؤقت (`SpawnProtection`).
- **`MultiplayerMenuUI.cs` و `MultiplayerLobbyUI.cs`**: تسجيل جميع أزرار القوائم تلقائياً عبر `UIButtonAudio.RegisterHierarchy(transform)`.

---

## ج. طريقة إضافة أو استبدال صوت خطوة بخطوة

لإضافة صوت جديد أو استبدال صوت موجود دون لمس أي سطر برمجي، اتبع الخطوات التالية:

### 1. استيراد الملف الصوتي إلى Unity
1. اسحب ملف الصوت (WAV أو MP3 أو OGG) إلى المجلد المناسب داخل المشروع:
   - المؤثرات الصوتية العامة: `Assets/_Multiplayer/Audio/SFX/`
   - الموسيقى الخلفية: `Assets/_Multiplayer/Audio/Music/`
2. اضبط إعدادات الاستيراد (Import Settings) في نافذة Inspector وفقاً للجدول الموضح في [القسم ي](#ي-إعدادات-الاستيراد-الموصى-بها).

### 2. فتح مكتبة الصوتيات وتحديد الإدخال
1. انتقل في نافذة المشروع (Project Window) إلى:
   `Assets/_Multiplayer/Audio/Libraries/GameAudioLibrary.asset`
2. في الـ Inspector، ستجد قائمتين:
   - **Sound Definitions**: تحتوي على جميع أصوات المؤثرات واللعب والقوائم.
   - **Background Music Tracks**: تحتوي على مقاطع الموسيقى للمشاهد المختلفة.
3. ابحث عن معرف الصوت المطلوب (مثل `Weapon_Revolver_Shoot` أو `Movement_Footstep`).

### 3. إسناد الملف وضبط الخصائص
1. في حقل **Clips**:
   - يمكنك سحب ملف صوتي واحد أو زيادة الحجم (Size) وإضافة عدة تنويعات صوتية (Variants). سيقوم النظام بالاختيار العشوائي الذكي بينها دون تكرار نفس الصوت مرتين متتاليتين.
2. اضبط **Volume** من 0.0 إلى 1.0 (يوصى بالبدء بـ 0.8 إلى 0.9).
3. فعّل **Randomize Pitch** للمؤثرات المتكررة (مثل خطوات الأقدام واصطدام الرصاص)، واضبط النطاق:
   - `Min Pitch`: **0.95**
   - `Max Pitch`: **1.05**
4. اضبط الأبعاد الصوتية:
   - لأصوات الواجهات والأزرار والإشعارات: اجعل `Is 3D = False` و `Spatial Blend = 0`.
   - لأصوات ساحة المعركة (إطلاق، مشي، اندفاع، صناديق): اجعل `Is 3D = True` و `Spatial Blend = 1.0`.
   - اضبط مسافات الصوت ثلاثي الأبعاد: `Min Distance` (افتراضياً 2m) و `Max Distance` (افتراضياً 30m - 40m).
5. حدد مجموعة المازج (`Category`): يتم توجيه الصوت تلقائياً إلى المجموعة المطابقة في الـ AudioMixer (`Weapons`, `Melee`, `Movement`, `Dash`, `Impacts`, `Pickups`, `UI`, `Announcer`).

### 4. حفظ الأصول واختبار الصوت
1. اضغط `Ctrl + S` لحفظ التغييرات على الـ ScriptableObject.
2. يمكنك استخدام أداة الفحص السريع عبر القائمة العلوية:
   `Tools -> Shootx -> Audio -> Audit Audio Assets & Missing Clips`
   ستظهر لك رسالة تفصيلية في الـ Console تؤكد سلامة الإسنادات وتوضح إن كانت هناك أصول ناقصة.
3. ادخل في وضع اللعب (Play Mode) لتجربة الصوت مباشرة.

---

## د. مواضع الأصوات على مجسم اللاعب (Player Prefab)

تم ربط الصوتيات بهيكل مجسم اللاعب القائم (`Assets/_Multiplayer/Players/Prefabs/Player.prefab`) دون إحداث أي تغيير هيكلي:

```text
Player (Root) ────────────────────────── [PlayerAudioController] ◄── المكون الرئيسي
│                                        ├── CharacterController
│                                        ├── PlayerShooting
│                                        ├── PlayerMelee
│                                        ├── PlayerDash
│                                        └── PlayerHealth
├── Visuals
│   ├── ActionFigure
│   ├── Driver
│   ├── SpaceRanger
│   ├── Survivalist
│   └── Weapon
│       ├── SM_Wep_Revolver_Cylinder_01
│       ├── SM_Wep_Revolver_Hammer_01
│       ├── SM_Wep_Revolver_Trigger_01
│       └── FirePoint ─────────────────── [نقطة صدور صوت إطلاق النار ووميض الفوهة]
├── MeleeHitPoint ─────────────────────── [نقطة صدور صوت ارتطام السلاح اليدوي]
├── StatusAnchor
├── DashTrail ─────────────────────────── [نقطة صدور صوت اندفاع الهواء]
└── CinemachineCamera ─────────────────── [كاميرا اللعبة - يُفعّل AudioListener للمالك فقط]
```

### تفاصيل تموضع المؤثرات الصوتية:

1. **خطوات الأقدام (`Movement_Footstep`)**:
   - يتم تشغيلها عند موقع أقدام اللاعب (`transform.position`).
   - بالنسبة للمالك (Local Player): يتم تشغيلها بتوزيع ثلاثي الأبعاد هجين مع الحفاظ على وضوح التغذية الراجعة.
   - بالنسبة للاعبين الآخرين (Remote Players): يتم تشغيلها ثلاثية الأبعاد بالكامل في موقع كل لاعب.
   - **ملاحظة**: لا تتطلب AudioSource ملتصقاً، بل تُستدعى من الحوض (`AudioSourcePool`) عند كل خطوة.
2. **صوت إطلاق مسدس الريفولفر (`Weapon_Revolver_Shoot`)**:
   - يتم تشغيله بدقة عند نقطة الفوهة (`FirePoint`) التابعة للشخصية المفعلة حالياً.
   - في حال تغيير الشخصية البصرية عبر `PlayerVisualManager`، يتم تحديث مرجع الفوهة تلقائياً عبر `SetMuzzlePoint(Transform)`.
3. **صوت النقر الجاف عند نفاد الذخيرة (`Weapon_DryFire`)**:
   - يُشغّل عند فوهة السلاح عند محاولة الإطلاق برصيد ذخيرة يساوي صفراً.
4. **صوت استرداد الذخيرة الأساسية (`Weapon_BasicAmmoRefunded`)**:
   - صوت نغمة ثنائية الأبعاد (2D) خاصة بالمالك تؤكد استرجاع رصاصة بنجاح بعد إصابة لاعب عدو.
5. **صوت الاندفاع (`Dash_Whoosh` و `Dash_Impact`)**:
   - يبدأ صوت انطلاق الاندفاع لحظة بدء الحركة الفعلية.
   - صوت الارتطام بالجدران يُشغّل عند نقطة التماس الفيزيائية عند إيقاف الاندفاع قبل موعده بسبب عائق.
6. **أصوات الاشتباك اليدوي (`Melee_Swing` و `Melee_Hit_Player`)**:
   - صوت التلويح يصدر من موقع المهاجم.
   - صوت الضربة الناجحة يصدر بدقة عند نقطة الاصطدام (`hitPoint`).
7. **أصوات الضرر والموت والولادة (`Hurt`, `Death`, `Respawn`)**:
   - صوت الضرر (`Player_Hurt`): صوت 2D للمالك لتأكيد التعرض لإصابة، وثلاثي الأبعاد 3D للخصوم في موقع اللاعب المتضرر.
   - صوت الموت (`Player_Death`): يُشغّل مرة واحدة فقط عند نفاد الصحة (محمي بـ Idempotency Guard ضد التكرار).
   - صوت إعادة الولادة (`Player_Respawn`): يُشغّل عند ظهور اللاعب في نقطة الـ Spawn.
   - درع الحماية (`SpawnProtection_Start` / `End`): يُشغّل فور تفعيل الحماية المؤقتة وعند زوالها.

---

## هـ. صوتيات واجهات المستخدم (UI & Button Audio)

### 1. التسجيل التلقائي للأزرار (`UIButtonAudio`)
لتوفير أقصى قدر من الراحة والإنتاجية، تم تزويد النظام بآلية تسجيل تلقائي:
- في `MultiplayerMenuUI` و `MultiplayerLobbyUI`، يتم استدعاء الدالة التالية في `Start()`:
  ```csharp
  Shootx.Audio.UIButtonAudio.RegisterHierarchy(transform);
  ```
  تقوم هذه الدالة بفحص شجرة الواجهة بالكامل وإضافة مكون `UIButtonAudio` إلى كل زر موجود لا يمتلك المكون مسبقاً، مع توجيهه افتراضياً لصوت النقر (`UI_ButtonClick`).

### 2. تخصيص زر يدوي في الـ Inspector
إذا أردت تخصيص صوت محدد لزر معين (مثل زر العودة، أو زر الإلغاء، أو زر بدء المباراة):
1. حدد الزر في نافذة التسلسل الهرمي (Hierarchy).
2. أضف له المكون `UIButtonAudio` عبر `Add Component -> Shootx -> Audio -> UI Button Audio`.
3. اختر نوع الصوت من القائمة المنسدلة في حقل **Click Sound**:
   - `UI_ButtonClick`: للنقر الافتراضي على خيارات القوائم.
   - `UI_ButtonBack`: لأزرار العودة وإغلاق اللوحات.
   - `UI_LobbyReady`: لزر تأكيد الجاهزية في اللوبي.
   - `UI_LobbyUnready`: لزر إلغاء الجاهزية.
   - `UI_MatchStartButton`: لزر بدء المعركة الخاص بالمضيف.
4. **تنبيه هام**: لا يقوم هذا المكون بمسح أو تعديل أي ربط برمجي في قائمة `Button.onClick` الأصلية للعبة؛ فهو يعمل كطبقة مراقبة موازية تضمن سلامة المنطق تماماً.

### 3. أشرطة التمرير ومربعات الاختيار (`Sliders & Toggles`)
- أشرطة التمرير مزودة بمكون `SliderAudioFeedback` الذي يمنع صدور صوت مع كل فريم أثناء السحب (Throttling)، ويصدر نقرات ناعمة ومتزنة عند قطع مسافة معينة في شريط التمرير.

---

## و. مازج الصوتيات (AudioMixer)

تم إنشاء أصل المازج الموحد داخل المسار:
`Assets/_Multiplayer/Audio/Mixers/GameAudioMixer.mixer`

### هيكل المجموعات (Group Hierarchy):

```text
Master (Group)
├── Music (Group)
├── SFX (Group)
│   ├── Weapons (Sub-group)
│   ├── Melee (Sub-group)
│   ├── Movement (Sub-group)
│   ├── Dash (Sub-group)
│   ├── Impacts (Sub-group)
│   ├── Pickups (Sub-group)
│   └── Environment (Sub-group)
├── UI (Group)
└── Announcer (Group)
```

### المتغيرات البرمجية المكشوفة (Exposed Parameters):

| اسم المتغير البرمجي (Exact Name) | المجموعة المستهدفة | النطاق بالديسيبل (dB) | القيمة الافتراضية |
| :--- | :--- | :--- | :--- |
| `MasterVolume` | Master | من -80 dB إلى 0 dB | 0 dB (مستوى 1.0) |
| `MusicVolume` | Music | من -80 dB إلى 0 dB | -2 dB (مستوى 0.8) |
| `SFXVolume` | SFX (يشمل كافة المجموعات الفرعية) | من -80 dB إلى 0 dB | 0 dB (مستوى 1.0) |
| `UIVolume` | UI | من -80 dB إلى 0 dB | 0 dB (مستوى 1.0) |
| `AnnouncerVolume` | Announcer | من -80 dB إلى 0 dB | 0 dB (مستوى 1.0) |

### صيغة التحويل الرياضية (Linear to Decibels):
أشرطة التمرير في الواجهات تعمل بنظام القيمة الخطية من 0.0 إلى 1.0، بينما مازج Unity يعمل بمقياس اللوغاريتم العشري بالديسيبل. يتم التحويل داخل `AudioManager` بالمعادلة الدقيقة التالية لتفادي قيم اللانهاية السالبة:
$$\text{dB} = \begin{cases} 20 \times \log_{10}(\text{linear}) & \text{إذا كانت القيمة } > 0.0001 \\ -80 & \text{في حالة الصفر (كتم تام)} \end{cases}$$

---

## ز. إعدادات المستخدم وحفظ الصوت (Settings & Persistence)

### مفاتيح الحفظ المحلي (`PlayerPrefs Keys`):
تُحفظ إعدادات الصوت محلياً على جهاز المستخدم لتبقى ثابتة بعد إعادة تشغيل اللعبة، ولا تُرسل عبر الشبكة نهائياً:
- `Shootx_Audio_MasterVolume` (افتراضياً: 1.0)
- `Shootx_Audio_MusicVolume` (افتراضياً: 0.8)
- `Shootx_Audio_SFXVolume` (افتراضياً: 1.0)
- `Shootx_Audio_UIVolume` (افتراضياً: 1.0)
- `Shootx_Audio_AnnouncerVolume` (افتراضياً: 1.0)
- `Shootx_Audio_Muted` (افتراضياً: 0 = غير مكتوم)

### لوحة الإعدادات (`AudioSettingsUI`):
- يمكن إضافة الـ Prefab أو ربط المكون بلوحة الإعدادات في أي مشهد.
- يقوم المكون بقراءة القيم المحفوظة تلقائياً في `Start()` و `OnEnable()` وضبط مواضع أشرطة التمرير بدقة.
- تحريك أي شريط يقوم بتحديث قناة الـ AudioMixer المطابقة فورياً دون التأثير على القنوات الأخرى.
- زر **Reset Defaults** يعيد جميع القيم للوضع الافتراضي بسلاسة.

---

## ح. المزامنة الصوتية في الشبكة (Multiplayer Audio Sync)

هذا الجزء جوهري لمنع استهلاك الباندويث ومنع تكرار الأصوات:

### 1. جدول تصنيف الأصوات (Local vs Remote):

| الحدث الصوتي | المالك المحلي (Owner / Local) | اللاعبون البعيدون (Remote Clients) | آلية المزامنة المتبعة |
| :--- | :--- | :--- | :--- |
| **نقرات الأزرار والقوائم** | ثنائي الأبعاد محلي (Local 2D) | **صامت تماماً** (لا يُرسل) | محلي بحت (Zero Network Traffic). |
| **إطلاق النار بالريفولفر** | تشغيل فوري تنبؤي (Local Predicted) | صوت 3D عند استلام `PlayShootClientRpc` | المالك يتجاهل الـ RPC (`if (IsOwner) return;`) لمنع تكرار الطلقة. |
| **خطوات الأقدام** | صوت محلي عند القدمين | صوت 3D موجه في موقع الشخصية | **بدون أي RPCs!** يُحسب محلياً من تغير الموقع الأفقي (`delta.magnitude`). |
| **الاندفاع (Dash)** | تشغيل فوري لحظة بدء الاندفاع | صوت 3D عند استلام `DashVisualClientRpc` | المالك يتجاهل الـ RPC؛ ولا يُشغّل الصوت إذا كانت المهارة في فترة التهدئة. |
| **ضربة اليد (Melee)** | تشغيل فوري للتلويح محلياً | صوت 3D عند استلام `MeleeVisualsClientRpc` | المالك يتجاهل الـ RPC؛ وصوت الارتطام يُشغّل فقط عند نجاح الضربة (`landedHit`). |
| **إصابة وتدمير الصناديق** | صوت 3D عند نقطة الارتطام | صوت 3D عند نقطة الارتطام والتدمير | تأكيد خادمي موثوق عبر `MatchManager` ودوال DOTween المتزامنة. |
| **التقاط الذخيرة** | رنين محلي لطيف للملتقط (2D) | صوت 3D في موقع صندوق الذخيرة | الخادم يعتمد الالتقاط ويوزع إشعار العرض لجميع العملاء. |
| **إعلانات المباراة والنتائج** | صوت موحد ثنائي الأبعاد (2D) | صوت موحد ثنائي الأبعاد (2D) | متزامن مع أحداث الـ MatchManager الرسمية. |

---

## ط. القائمة المرجعية للأصول الصوتية (Audio Asset Checklist)

تم إجراء جرد شامل لكافة الملفات الصوتية داخل المشروع وربط جميع الأصول الموجودة فعلياً:

| معرف الصوت (SoundId) | الملف المربوط من المشروع حالياً | الأبعاد | الحالة |
| :--- | :--- | :--- | :--- |
| `UI_ButtonClick` | `Buttons, Slides, Dings and Drops/.../Click01.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_ButtonBack` | `Buttons, Slides, Dings and Drops/.../ButtonNoise04.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_TabSelect` | `Buttons, Slides, Dings and Drops/.../ButtonNoise03.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_PanelOpen` | `Buttons, Slides, Dings and Drops/.../Chord04.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_PanelClose` | `GUIPackCartoon/Demo/Sounds/Close.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_ActionSuccess` | `Buttons, Slides, Dings and Drops/.../Chord01.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_ActionFailed` | `Buttons, Slides, Dings and Drops/.../Alert01.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_LobbyReady` | `Buttons, Slides, Dings and Drops/.../Chord05.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_LobbyUnready` | `Buttons, Slides, Dings and Drops/.../ButtonNoise06.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_MatchStartButton` | `Buttons, Slides, Dings and Drops/.../Chord08.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_SliderTick` | `Buttons, Slides, Dings and Drops/.../Chirp01.wav` | 2D | ✅ **جاهز ومربوط** |
| `UI_Toggle` | `Buttons, Slides, Dings and Drops/.../Stacc01.wav` | 2D | ✅ **جاهز ومربوط** |
| `Weapon_Revolver_Shoot` | `bulletshot.mp3` + تنويعات `Vefects_SFX_GunShot_01/02.wav` | 3D | ✅ **جاهز ومربوط (3 بدائل)** |
| `Weapon_DryFire` | `Buttons, Slides, Dings and Drops/.../Press01.wav` | 3D | ✅ **جاهز ومربوط** |
| `Weapon_BasicAmmoRefunded` | `Buttons, Slides, Dings and Drops/.../FairyBell_01.wav` | 2D | ✅ **جاهز ومربوط** |
| `Impact_Wall` | `bullet-hit.mp3` + `Vefects_SFX_Impact_01.wav` | 3D | ✅ **جاهز ومربوط** |
| `Impact_Ricochet` | `Buttons, Slides, Dings and Drops/.../Scrape01.wav` | 3D | ✅ **جاهز ومربوط** |
| `Impact_DestructibleBox` | `Vefects_SFX_Shrapnel_01.wav` | 3D | ✅ **جاهز ومربوط** |
| `Impact_PlayerHit` | `Vefects_SFX_Impact_Blood_01.wav` | 3D | ✅ **جاهز ومربوط** |
| `Melee_Swing` | `Vefects_SFX_Slash_Classic.wav` | 3D | ✅ **جاهز ومربوط** |
| `Melee_Hit_Player` | `Vefects_SFX_Impact_Blood_01.wav` | 3D | ✅ **جاهز ومربوط** |
| `Dash_Whoosh` | `SFX_SCIFI_ENERGY_Skill_Whoosh_Electrified_1.wav` | 3D | ✅ **جاهز ومربوط** |
| `Box_Destroyed` | `Vefects_SFX_Explosion_Classic.wav` | 3D | ✅ **جاهز ومربوط** |
| `Pickup_Ammo_World` | `GUIPackCartoon/Demo/Sounds/Coins.wav` | 3D | ✅ **جاهز ومربوط** |
| `Pickup_Ammo_Local` | `Buttons, Slides, Dings and Drops/.../Chord02.wav` | 2D | ✅ **جاهز ومربوط** |
| `Match_CountdownTick` | `Buttons, Slides, Dings and Drops/.../Button Noise1.wav` | 2D | ✅ **جاهز ومربوط** |
| `Match_CountdownFinal` | `Buttons, Slides, Dings and Drops/.../Chord03.wav` | 2D | ✅ **جاهز ومربوط** |
| `Match_Go` | `GUIPackCartoon/Demo/Sounds/Magical.wav` | 2D | ✅ **جاهز ومربوط** |
| `Match_TimerWarning` | `Buttons, Slides, Dings and Drops/.../Alert02.wav` | 2D | ✅ **جاهز ومربوط** |
| `Match_KillConfirmed` | `Buttons, Slides, Dings and Drops/.../Harmonic01.wav` | 2D | ✅ **جاهز ومربوط** |
| `Match_Victory` | `GUIPackCartoon/Demo/Sounds/Win.wav` | 2D | ✅ **جاهز ومربوط** |
| `Match_Defeat` | `GUIPackCartoon/Demo/Sounds/Lose - Thunder.wav` | 2D | ✅ **جاهز ومربوط** |
| `Match_Draw` | `Buttons, Slides, Dings and Drops/.../Chord07.wav` | 2D | ✅ **جاهز ومربوط** |
| `MusicTrack: MainMenu` | `Burgundy - All Night (freetouse.com).mp3` | 2D | ✅ **جاهز ومربوط** |
| `MusicTrack: Lobby` | `Background Music.wav` | 2D | ✅ **جاهز ومربوط** |
| `MusicTrack: Gameplay` | `Aetheric - Edge of Motion (freetouse.com).mp3` | 2D | ✅ **جاهز ومربوط** |
| `MusicTrack: Results` | `Dagored - Wall Of Sound.mp3` | 2D | ✅ **جاهز ومربوط** |

### ⚠️ الأصول الصوتية الإضافية المطلوب تزويدها لاحقاً (Optional / Missing):
الأصوات التالية تم تجهيز هياكلها وقنواتها بالكامل في الكود ومكتبة الأصوات وتعمل بصمت دون أي أخطاء، وينقصها فقط تزويد الملف الصوتي حسب رغبتك الفنية:
1. **`Movement_Footstep`** (صوت خطوات الأقدام العادية): يُقترح ملف بصيغة WAV مدته 0.25 ثانية لصوت خطوة على أرضية صلبة.
2. **`Movement_Footstep_Dirt`** و **`Movement_Footstep_Metal`** (خطوات ترابية أو معدنية إضافية).
3. **`Dash_Impact`** (صوت اصطدام الاندفاع بجدار): يُقترح ملف صوت ارتطام ثقيل مدته 0.3 ثانية.
4. **`Player_Hurt`** (صوت تأوه الشخصية عند تلقي الضرر): صوت بشري قصير 0.3 ثانية.
5. **`Player_Death`** (صوت سقوط وموت الشخصية): صوت سقوط أو تلاشي 0.6 ثانية.
6. **`Player_Respawn`** (صوت إعادة الولادة وظهور اللاعب): صوت شعاع طاقة أو إعادة تجسد 0.8 ثانية.
7. **`Player_SpawnProtection_Start`** و **`Player_SpawnProtection_End`** (درع الحماية المؤقت): صوت طنين الدرع واختفائه.
8. **`Melee_Hit_Environment`** (اصطدام السلاح اليدوي بالحائط): صوت اصطدام نصل بحجر أو حديد.

---

## ي. إعدادات الاستيراد الموصى بها (Mobile Import Settings)

لتحقيق التوازن المثالي بين الأداء، استهلاك الذاكرة العشوائية (RAM)، وحجم ملف التثبيت على الهواتف:

| نوع الصوت | القنوات (Channels) | نوع التحميل (Load Type) | صيغة الضغط (Compression Format) | جودة الصوت (Quality) | معدل العينة (Sample Rate) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **المؤثرات القصيرة وسريعة التكرار (طلقات، خطوات، اصطدامات)** | **Force To Mono** | **Decompress On Load** | **ADPCM** أو **PCM** | 100% | Optimize Sample Rate (22,050 Hz) |
| **أصوات الواجهات والأزرار (UI Buttons)** | Stereo أو Mono | **Decompress On Load** | **ADPCM** | 100% | 44,100 Hz |
| **المؤثرات المتوسطة (درع الحماية، الانفجارات)** | Force To Mono | **Compressed In Memory** | **Vorbis** | 70% | Optimize Sample Rate |
| **الموسيقى الخلفية الطويلة (BGM)** | Stereo | **Streaming** | **Vorbis** | 60% - 70% | 44,100 Hz |
| **الأصوات البيئية الحلقية (Ambience)** | Stereo | **Compressed In Memory** | **Vorbis** | 65% | 44,100 Hz |

> **نصيحة ذهبية للهواتف الذكية:**
> لا تجعل المؤثرات الصوتية ثلاثية الأبعاد (3D Sounds) بصيغة Stereo أبداً! فعّل خيار **Force To Mono** دائماً، لأن محرك Unity لا يستطيع حساب التموضع الفراغي ثلاثي الأبعاد الحقيقي إلا على ملفات الصوت أحادية القناة (Mono).

---

## ك. استكشاف الأخطاء وإصلاحها (Troubleshooting)

### 1. لا يصدر أي صوت رغم نجاح الحركة أو الحدث البرمجي
- افتح نافذة الـ Console وتأكد من عدم وجود تحذير يوضح أن الصوت غير مسند.
- افتح `AudioLibrary.asset` وتأكد من وجود مقطع صوتي في مصفوفة `Clips` الخاص بهذا الـ `SoundId`.
- تأكد أن شريط مستوى الصوت في لوحة الإعدادات أو الـ AudioMixer ليس مسحوباً لأقصى اليسار (-80 dB).

### 2. صوت الإطلاق أو الاندفاع يتكرر مرتين لدى اللاعب صاحب الشخصية
- تحقق من أن الدالة البرمجية في الـ ClientRpc تحتوي على شرط استثناء المالك:
  ```csharp
  if (IsOwner) return;
  ```
  حيث يقوم المالك بتشغيل الصوت فورياً عبر التنبؤ المحلي (Local Prediction)، بينما يستمع باقي العملاء للصوت ثلاثي الأبعاد عبر الـ RPC.

### 3. الصوت ثلاثي الأبعاد يبدو وكأنه صوت واجهة ثنائي الأبعاد مسطح (2D)
- افتح `AudioLibrary.asset` وحدد الصوت المعني.
- تأكد من تفعيل خيار **Is 3D** وجعل قيمة **Spatial Blend = 1.0**.
- تأكد أن ملف الصوت الأصلي مفعل عليه **Force To Mono** في إعدادات الاستيراد.

### 4. ظهور تحذير "There are 2 AudioListeners in the scene"
- يقوم سكربت `AudioManager` تلقائياً عبر دالة `ValidateSingleAudioListener()` عند تحميل أي مشهد بالبحث عن كافة الـ Listeners وتعطيل الإضافي والإبقاء على الـ Listener الموجود على الكاميرا الرئيسية (`MainCamera`).

### 5. خطوات الأقدام تستمر بالعمل أثناء الوقوف أو في الهواء
- يتحقق سكربت `PlayerAudioController` في كل إطار من:
  1. أن سرعة الحركة الأفقية تتجاوز `minVelocityThreshold` (افتراضياً 0.5 م/ث).
  2. أن الشخصية تلامس الأرض فيزيائياً (`CharacterController.isGrounded`).
  3. أن اللاعب ليس في حالة اندفاع (`!isDashing`) وليس ميتاً (`!isDead`).

### 6. انقطاع أو إعادة تشغيل الموسيقى بشكل مفاجئ عند تبديل المشاهد
- تم تصميم `MusicController` ليفحص اسم المقطع الحالي قبل البدء؛ فإذا كان المشهد التالي يتطلب نفس المقطع (مثل الانتقال بين لوحات متقاربة)، يستمر المقطع بالعزف دون انقطاع، وإذا كان المقطع مختلفاً يتم تطبيق التلاشي التدريجي السلس (Crossfade).

---
**تم إعداد هذا الدليل وتطبيقه برمجياً وهندسياً ليكون المرجع الشامل والدائم لنظام صوتيات Shootx.**
