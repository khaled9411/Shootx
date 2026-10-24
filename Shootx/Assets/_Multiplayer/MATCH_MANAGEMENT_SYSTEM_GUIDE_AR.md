# دليل نظام إدارة المباريات الجماعية (Match Management System Guide)
## Unity 6.3 / Netcode for GameObjects (NGO 2.13.3) / DOTween Pro

---

## 1. ملخص المشاكل التي تم إصلاحها (Bugs Fixed)

### أ) أخطاء التجميع في `MatchManager.cs`:
- **المشكلة**: كان يوجد قوس إغلاق زائد `}` في السطر 1182 أدى إلى إغلاق الـ class `MatchManager` قبل نهايته، مما جعل الدوال والخصائص اللاحقة تظهر كأنها Top-level statements خارج أي namespace أو class، متسببة في أخطاء:
  - `error CS0106: The modifier 'public' is not valid for this item`
  - `error CS8803: Top-level statements must precede namespace and type declarations`
  - `error CS1022: Type or namespace definition, or end-of-file expected`
  بالإضافة لتكرار تعريف بعض الخصائص (`CurrentState`, `CountdownValue`, إلخ).
- **الحل**: تم حذف القوس الزائد والخصائص المكررة، وإغلاق الـ class بشكل صحيح.

### ب) خطأ `IEnumerator` في `PlayerHealth.cs`:
- **المشكلة**: ظهور `error CS0246: The type or namespace name 'IEnumerator' could not be found` بسبب فقدان `using System.Collections;`.
- **الحل**: تمت إضافة `using System.Collections;`.

### ج) تحذيرات إخفاء الأعضاء والـ Obsolete RPCs:
- **`OnDestroy`**: تم تحويل `OnDestroy` في كل من `MatchManager.cs` و `AmmoPickup.cs` إلى `public override void OnDestroy()` مع استدعاء `base.OnDestroy()` بما يتوافق مع بنية `NetworkBehaviour`.
- **`[ServerRpc(RequireOwnership = false)]`**: تم تحديثه إلى صيغة NGO 2.0 الحديثة `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]`.
- **تحذيرات الحقول غير المستخدمة**: تم دمج `countdownFadeDuration` في تسلسل DOTween لـ `MatchHUD`، وتأمين حقل `forceMobileInEditor` في `MobileControlsHUD`.

---

## 2. ما تم إكماله وتطويره في النظام

### 1. مزامنة إحصائيات اللاعبين (`PlayerMatchStats`) وربطها بالـ Prefab:
- تم ربط مكون `PlayerMatchStats` مباشرة داخل `Player.prefab`.
- يحتوي على NetworkVariables مسؤولة عن مزامنة:
  - عدد القتلى (`Kills`).
  - عدد الوفيات (`Deaths`).
  - سلسلة القتل الحالية والقصوى (`KillStreak` و `BestKillStreak`).
  - حالة حياة اللاعب الرسمية (`PlayerLifeState`: Alive, Dead, Respawning, SpawnProtected).
- جميع التعديلات تتم حصراً على السيرفر (Server-Authoritative).

### 2. قفل التحكم ومنع الاشتباك خارج وقت اللعب الفعلي:
- **الحركة والدوران (`PlayerController.cs`)**:
  - دالة `SetInputLocked(bool)` تقفل الحركة وتصفر مدخلات الجوال والكمبيوتر وتمنع أي انزلاق.
  - تقفل تلقائياً لجميع اللاعبين أثناء العد التنازلي التمهيدي (PreMatchCountdown) وأثناء الموت وعند انتهاء المباراة.
- **إطلاق النار (`PlayerShooting.cs`)**:
  - تم وضع فحص صارم في `TryFire` (الكمبيوتر)، و `RequestMobileFire` (الجوال)، ومعاينة الليزر `UpdateAimPreview`، وعلى السيرفر في `FireServerRpc`.
  - يمنع تماماً أي إطلاق للنار أو إلحاق ضرر إلا عندما تكون حالة المباراة `MatchState.Active`.
- **الضربات القريبة (`PlayerMelee.cs`)**:
  - محمي محلياً وعلى السيرفر بفحص `MatchState.Active`.
- **حماية البداية ونظام الصحة (`PlayerHealth.cs`)**:
  - يستشير `MatchManager.Instance.IsDamageAllowed(OwnerClientId)` قبل قبول أي ضرر، مما يمنع إلحاق الضرر باللاعبين ذوي الحماية المؤقتة (Spawn Protection) أو عند توقف المباراة.

### 3. إعدادات المباراة المركزية (`DefaultMatchConfig.asset`):
- تم إنشاء الأصل البرمجي `Assets/_Multiplayer/Scripts/Match/DefaultMatchConfig.asset` بناءً على `MatchConfig.cs`.
- القيم الافتراضية:
  - الحد الأقصى للاعبين: 4
  - هدف القتل للفوز: 10
  - مدة المباراة: 180 ثانية (3 دقائق)
  - العد التنازلي التمهيدي: 3 ثوانٍ
  - مهلة إعادة الإحياء: 3 ثوانٍ
  - مدة حماية البداية: 2.5 ثانية
  - مدة عرض النتائج: 8 ثوانٍ
  - أدنى مسافة تفضيلية للإحياء بعيداً عن الخصوم: 8 أمتار

### 4. حزمة الـ Prefab والتسجيل الشبكي (`MatchManager.prefab`):
- تم إنشاء `Assets/_Multiplayer/Prefabs/MatchManager.prefab` محتوياً على `MatchManager` و `NetworkObject` والإعدادات ومراجع خرائط الـ PVP الأربعة.
- تم تسجيل الـ Prefab تلقائياً في قائمة `Assets/DefaultNetworkPrefabs.asset` التابعة لـ NGO لضمان إمكانية توليده ومزامنتة عبر الشبكة دون مشاكل.

### 5. التوليد التلقائي عبر `MultiplayerSessionManager`:
- عند تحميل مشهد اللعب `MainMultiplayer`، يتحقق السيرفر (المستضيف) تلقائياً من وجود `MatchManager`. إذا لم يكن موجوداً في المشهد، يقوم السيرفر بتوليده فوراً واستدعاء `Spawn()` عبر NGO لتعميمه على جميع العملاء المتصلين.

### 6. شاشة النتائج وسجلات اللاعبين المحلية:
- **`MatchHUD`**: يعرض مؤقت المباراة التنازلي مع وميض تحذيري عند اقتراب النهاية، عد تنازلي ثلاثي الأبعاد مع تأثير Punch Scale، مؤشر حماية البداية، وإشعارات القتل.
- **`MatchResultsUI`**: يعرض لوحة متصدرين كاملة بالترتيب، البطاقة الاحتفالية للفائز، مؤقت الـ 8 ثوانٍ للعودة، وزر "متابعة" يرسل RPC للسيرفر للانتقال المبكر.
- **`LocalPlayerRecords`**: يحفظ تاريخ ونتائج كل مباراة محلياً في `Application.persistentDataPath` بصيغة JSON مع منع التكرار بواسطة `matchId`.

### 7. العودة السلسة للوبي (`OnMatchEnded`):
- لا يتم تدمير الجلسة أو قطع الاتصال، بل تتم استعادة حالة `InLobby`، إعادة تشغيل الـ Heartbeat و Polling، وإعادة فتح الغرفة للاعبين الجدد وتصفير حالات الـ Ready تمهيداً للجولة القادمة.

---

### د) مشكلة تحميل الخريطة عند السيرفر فقط وعدم ظهورها عند الـ Clients:
- **سبب المشكلة**:
  1. في `MatchManager.cs` داخل دالة `SelectAndLoadMapCoroutine`، كان السيرفر يقوم فقط باستدعاء `Instantiate(mapPrefabs[selectedIndex])` محلياً عنده دون أن يتم استدعاء التوليد عند العملاء.
  2. كانت دالة الاستقبال على العملاء `NotifyMapSelectedClientRpc` وكذلك دالة `HandleMapIndexChanged` تحتويان على مجرد سطر طباعة `Debug.Log` دون أي كود فعلي لإنشاء نسخة من الـ Prefab محلياً عند العميل (`Instantiate`).
  3. في `MatchManagerBootstrapper.cs` كان الكود يخرج مبكراً إذا لم يكن الجهاز هو السيرفر (`!IsServer`)، وبالتالي لم يكن يتم تمرير مصفوفة `mapPrefabs` لمدير المباراة على أجهزة الـ Clients في حال عدم وجودها.
  4. بقاء مكونات `NetworkTransform` و `NetworkObject` غير الموّلدة شبكياً على مجسمات الخريطة الثابتة مما كان يسبب تضارباً مع نظام NGO.
- **الحل الجذري**:
  1. **تفعيل التحميل الذاتي للعملاء (`LoadMapClient`)**: تم تنفيذ دالة توليد الخريطة محلياً على جميع الـ Clients بمجرد استقبال رقم الخريطة المحددة سواء عبر الـ RPC أو عبر مزامنة `NetworkVariable<int> _selectedMapIndex` أو فور دخول العميل إلى المشهد (`OnNetworkSpawn`).
  2. **ضمان وصول مراجع الخرائط (`EnsureMapPrefabsLoaded`)**: أضيف في `MatchManagerBootstrapper.cs` و `MatchManager.cs` نظام حقن واحتياطي ذكي يضمن تزويد الـ Clients بمصفوفة `mapPrefabs` من الـ Bootstrapper فوراً.
  3. **تنظيف المكونات الزائدة (`CleanUpLocalMapNetworkComponents`)**: تعطيل وحذف مكونات الـ `NetworkObject` و `NetworkTransform` من مجسمات الخريطة بعد توليدها لتفادي أي استهلاك لحزم البيانات أو تحذيرات NGO ولضمان أعلى أداء فيزيائي وبيئي.
  4. **إلغاء التحميل النظيف (`UnloadActiveMap`)**: تفريغ الخريطة السابقة تلقائياً عند تغيير الجولة أو عند العودة إلى اللوبي على السيرفر وجميع العملاء.

### هـ) مشكلة تعطل الأنظمة وعدم ظهور الخرائط والرسائل عند بدء جولة ثانية بعد العودة للوبي:
- **الأعراض والرسائل الظاهرة**:
  1. `[Bootstrapper] MatchManager already exists.`
  2. `[AmmoSpawner] Failed to find a valid ground position after max placement attempts. Skipping spawn.`
  3. `[MPSpawn] Client 1 already has PlayerObject (Player(Clone)).`
  4. عدم توليد الخريطة وبقاء أجساد اللاعبين مقفولة المدخلات وتعطل المؤقت والأنظمة.
- **سبب المشكلة الجذري**:
  1. **بقاء مدير المباراة القديم (`MatchManager`) حياً عبر المشاهد**: عند استدعاء `netObj.Spawn()` دون تحديد `destroyWithScene: true`، يقوم NGO تلقائياً بنقل الكائن إلى `DontDestroyOnLoad`. وعند العودة إلى مشهد اللوبي ثم الدخول لماتش جديد، لا يتم إنشاء كائن جديد لأن القديم موجود بالفعل بحالة منتهية (`ReturningToLobby`) وكوروتين المباراة متوقف، مما يمنع اختيار وتوليد الخريطة وإطلاق العد التنازلي.
  2. **بقاء أجساد اللاعبين القديمة (`PlayerObject`) في الذاكرة**: في NGO تبقى كائنات اللاعبين عبر المشاهد بشكل افتراضي ما لم يتم استدعاء `Despawn(true)`. عند الرجوع للوبي بقيت الأجساد، وعند بدء المباراة الثانية رأى `MultiplayerSpawnManager` أن العميل لديه جسد بالفعل فتخطى التوليد وتركه بجسده القديم المقفول.
  3. **فشل مُوَلّد الذخيرة (`AmmoPickupSpawner`)**: كان يبدأ فوراً بعد إطار واحد بالبحث عن أرضية بواسطة الـ Raycast، ونظراً لعدم توليد الخريطة فإن الأشعة تسقط في الفراغ وتفشل المحاولات.
- **الحل الجذري المطبق**:
  1. **التنظيف الكامل والشامل عند الرجوع للوبي (`TriggerReturnToLobby`)**:
     - استدعاء `DespawnAllPlayers()` لحذف وإلغاء توليد كائنات جميع اللاعبين شبكياً على السيرفر والعملاء.
     - استدعاء `DespawnAllPickups()` لحذف أي ذخائر أو عناصر متبقية في الساحة.
     - تفريغ الخريطة واستدعاء `MatchManager.NetworkObject.Despawn(true)` قبل الانتقال للوبي.
  2. **توليد نظيف لكل مباراة مع `destroyWithScene: true`**:
     - ضبط توليد `MatchManager` بـ `destroyWithScene: true` في كل من `MatchManagerBootstrapper` و `MultiplayerSessionManager`.
     - منع التضارب بين `EnsureMatchManagerSpawned` والـ Bootstrapper بحيث يتولى الـ Bootstrapper التهيئة الحصرية بكافة مراجع المشهد والخرائط.
  3. **تصفير وإحياء اللاعبين الدفاعي في `MultiplayerSpawnManager`**:
     - إذا وجد السيرفر أي كائن `PlayerObject` قديم عند توليد اللاعب، يقوم بحذفه فوراً وإنشاء جسد جديد تماماً بنقاط التوليد الخاصة بالخريطة الجديدة وبإحصائيات مصفرة وتحكم نشط.
  4. **مزامنة موَلّد الذخيرة مع جاهزية الخريطة**:
     - تم تعديل `AmmoPickupSpawner.InitialSpawnCoroutine` لينتظر تأكيد وجود الخريطة وتجهيز المصادمات الفيزيائية (`ActiveMapInstance != null`) قبل إجراء أي Raycast، مما يضمن نجاح توليد الذخائر بنسبة 100%.

---

### و) مشكلة بقاء اللاعب في شاشة الموت اللانهائية (Death Freeze) وتحذير `[Deferred OnSpawn]`:
- **الأعراض والرسائل الظاهرة**:
  1. عند موت اللاعب، تظل شاشة الموت (`deathOverlay`) نشطة ومقفولة المدخلات ولا يعود اللاعب للحياة إطلاقاً (Infinite Respawn Freeze).
  2. ظهور تحذير من Netcode مرتين متتاليتين:
     `[Netcode] [Deferred OnSpawn] Messages were received for a trigger of type OnSpawn with key [X], but that trigger was not received within 1 second(s).`
- **سبب المشكلة الجذري**:
  1. **سبب تجمد اللاعب في شاشة الموت**:
     - في `MatchLifecycleCoroutine` كان يتم استدعاء كوروتين تسجيل اللاعبين `RegisterPlayersCoroutine()` **قبل** استدعاء توليد اللاعبين `SpawnAllPlayers()`.
     - بما أن أجساد اللاعبين لم تكن قد تولدت بعد، كان الكوروتين ينتظر 5 ثوانٍ ثم ينتهي وقوائم اللاعبين (`_playerStatsMap` و `_playerHealthMap`) فارغة تماماً.
     - عند موت اللاعب، كانت دالة `HandlePlayerDeath` تفحص:
       `if (!_playerStatsMap.TryGetValue(victimClientId, out var victimStats)) return;`
       ولأن القائمة فارغة، كانت الدالة تخرج فوراً بصمت وتلغي استدعاء كوروتين الإحياء `RespawnPlayerCoroutine`، فيظل اللاعب ميتاً وشاشته سوداء ومدخلاته مقفولة للأبد.
  2. **سبب رسالة `[Deferred OnSpawn]` المزدوجة**:
     - في NGO، عندما يستلم العميل رسائل شبكية (RPCs أو مزامنة متغيرات) لكائن شبكي معين لم ينتهِ توليده محلياً بعد، يقوم NGO بوضع هذه الرسائل في قائمة الانتظار المؤجلة (`Deferred Queue`) لمدة ثانية واحدة في انتظار اكتمال التوليد `OnSpawn`.
     - قبل التعديل، كان كود `SpawnPlayerForClient` يقوم تلقائياً بحذف كائن اللاعب `Despawn(true)` وإعادة توليده مجدداً إذا استُدعي التوليد مرتين (مرة من حدث تحميل المشهد `LoadEventCompleted` ومرة من `MatchManager.SpawnAllPlayers`).
     - هذا الحذف الفوري للكائن الأول بينما كانت حزم الشبكة في طريقها للعملاء تسبب في أن العميل استلم رسائل لكائن تم تدميره على السيرفر فوراً، فانتظر NGO لثانية كاملة ثم أطلق التحذير لعدم وصول حدث التوليد لهذا الـ Key.
- **الحل الجذري المطبق**:
  1. **عكس ترتيب دورة حياة المباراة في `MatchLifecycleCoroutine`**:
     - أصبح توليد اللاعبين `SpawnAllPlayers()` يتم أولاً، يليه مباشرة انتظار اكتمال التوليد وتسجيلهم في `RegisterPlayersCoroutine()`.
  2. **تسجيل دفاعي فوري (`EnsurePlayerRegistered`) وواجهة موت مؤكدة (`ReportPlayerDeath`)**:
     - تمت إضافة الدالة العامة `EnsurePlayerRegistered(clientId)` التي تتحقق وتسجل اللاعب فوراً إذا لم يكن مسجلاً، مما يمنع خروج `HandlePlayerDeath` مبكراً.
     - ربط `PlayerHealth.HandleDeath()` مباشرة بالسيرفر عبر `MatchManager.Instance.ReportPlayerDeath(OwnerClientId, killerClientId)` لضمان بدء دورة الإحياء حتى لو لم تكن الاشتراكات مفعلة.
     - ضمان إيقاف أي كوروتين إحياء معلق وبدء كوروتين إحياء مضمون فوراً في `HandlePlayerDeath`.
  3. **استعادة كاملة وموثوقة لكائن اللاعب وحالته الفيزيائية (`ServerRespawnRestore` و `NotifyRespawnClientRpc`)**:
     - عند انتهاء مهلة الإحياء، يقوم السيرفر بتعطيل `CharacterController` مؤقتاً لنقل اللاعب بسلاسة إلى النقطة الآمنة `spawnPos` وإعادة تفعيله.
     - استدعاء `health.ServerRespawnRestore()` لإعادة الصحة إلى `MaxHealth` وإلغاء حالة الموت `isDead.Value = false`.
     - استدعاء `NotifyRespawnClientRpc` لإغلاق شاشة الموت `deathOverlay.SetActive(false)` واستعادة أزرار الجوال والتحكم `SetInputLocked(false)`.
  4. **منع التوليد المزدوج وحذف الكائنات الصالحة في `MultiplayerSpawnManager`**:
     - التحقق بدقة من أن كائن اللاعب ينتمي لمشهد اللعب الفعلي (`playerSceneName == GameplaySceneName`)، وفي هذه الحالة يتم الاحتفاظ به واعتباره مكتملاً دون تدميره أو إعادة توليده، مما أزال رسائل `[Deferred OnSpawn]` تماماً.

---

## 3. التحقق والتجميع (Build Verification)

تم فحص المشروع بواسطة مترجم C# Roslyn الخاص بـ Unity 6 (VS 2022 MSBuild):
- `Assembly-CSharp.dll`: **0 أخطاء (0 Errors, 0 Warnings)** (Clean compilation).
- `Assembly-CSharp-Editor.dll`: **0 أخطاء (0 Errors, 0 Warnings)** (Clean compilation).


