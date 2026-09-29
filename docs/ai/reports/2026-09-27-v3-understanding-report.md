# ChemLabSim V3: تقرير مرحلة الفهم (قراءة فقط)

التاريخ: 2026-09-27
النطاق: قراءة فقط. لم يُعدَّل أي ملف أو مشهد أو asmdef أو namespace. (فكّ تشفير `reactions.bytes` تم بنسخة Python في مجلد مؤقت خارج المشروع للتحقق من محتواه فقط.)

---

## 0. الفروقات بين الوثيقة التاريخية والواقع الفعلي (الأهم أولاً)

| # | ما تقوله الوثيقة | ما وجدته فعلياً | الدليل | الخطورة |
|---|---|---|---|---|
| D1 | قاعدة التفاعلات فيها ~147 تفاعل وهي المستخدمة | **وقت التشغيل يحمّل 12 تفاعلاً فقط.** `reactions.bytes` قديم (آخر تعديل 2026-04-04، آخر commit له 2026-04-18) ويحتوي 12 تفاعلاً (rxn_001..rxn_012). ملف `reactions.json` (147 سجل) لم يُعَد تشفيره بعد التوسعة | فكّ التشفير: `hmac ok True`, `12` سجل؛ `reactions.manifest.json` بطول 15280 بايت بتاريخ 2026-04-04؛ `Player.log`: `[AppManager] Reactions loaded and validated: 12` | عالية |
| D2 | ملف `reactions.json` هو مصدر قابل للتحميل | **`reactions.json` مصفوفة JSON في المستوى الأعلى (`[ {...} ]`)** بينما `ReactionDB` يتوقع كائناً `{ "reactions": [...] }`. `JsonUtility.FromJson<ReactionDB>` لا يقبل مصفوفة في المستوى الأعلى | `head reactions.json` يبدأ بـ `[`؛ `ReactionModels.cs:5-8`؛ `SecureReactionLoader.cs` (مسار المحرر `TryLoadSourceJsonIfPreferred`) | عالية |
| D3 | — | نتيجة D2: في المحرر، لأن الـ JSON أحدث من الـ bytes، يحاول المحمّل قراءة الـ JSON أولاً، **يفشل (متوقع: `JSON must represent an object type`)** فيطبع تحذيراً ويرجع إلى الـ bytes القديمة (12). وإذا شغّل أحد `Tools/Security/Encrypt Reactions JSON -> bytes` الآن فسيُشفَّر ملف المصفوفة كما هو، **وسيفشل التحليل وقت التشغيل ويعود خطأ "Reaction database is unavailable."** | `ReactionPacker.cs:35` يشفّر البايتات كما هي بلا تغليف | عالية (فخّ) |
| D4 | — | خطوة CI "Validate reaction data integrity" ستفشل: تستدعي `data.get(...)` على قائمة | `.github/workflows/build-and-test.yml` (معدّل وغير ملتزم) | متوسطة |
| D5 | إصلاح P0 عدّل `LabV3.unity` فقط ولم يُعدَّل أي ملف C# | ربط AppRoot موجود فعلاً وصحيح (انظر C). لكن شجرة العمل فيها **تعديلات C# غير ملتزمة** في `ReactionController.cs` و`ReactionEngine.cs` و`TimeBasedPourUseCase.cs` و`MolecularLabDisplay.cs` و`MolecularRendererView.cs` و`LabV3SceneSetup.cs` واختبارات، **وملفات جديدة غير متتبعة**: `ReactionEngineResult.cs`, `ReactionEvaluationAdapter.cs`, `ReactionEvaluationAdapterTests.cs`, `WebGLBuilder.cs`. تواريخها 2026-06/07، أي قبل تعديل المشهد (2026-08-23)، فهي على الأغلب عمل سابق لا جزء من P0 | `git status`, `git diff --stat` | متوسطة (غير ملتزمة = قابلة للضياع) |
| D6 | فرق `LabV3.unity` = إضافة AppRoot فقط | الفرق يضيف AppRoot، لكنه يتضمن أيضاً إعادة تسلسل ~61 RectTransform وتغييرات TMP وإعادة ترتيب `safetyNotesText` | `git diff LabV3.unity` | منخفضة (للمراجعة قبل الالتزام) |
| D7 | Core 18 / Engine 11 / Controllers 17 / Views 29 / Production 1 / Data 10 / Domain فارغ / Infrastructure فارغ | Core **14**، Engine **39** (مع Chemistry وSimulationV1)، Controllers 17، Views **41**، Production 1، Data **12**، **Domain 6** (MolecularSimulation, GuidedExperiments, Events)، **Infrastructure 2**، وأيضاً Application 1، Presentation 2، Services 6، Events 2، Debug 1، Editor 8. المجموع 161 ملف C# في `_ProjectV3` + 15 في `_Project` | `find ... -name '*.cs'` | معلوماتية |
| D8 | مالك قاعدة البيانات الوحيد: AppManager | **يوجد مالك ثانٍ فعلي:** `V3Bootstrap.InitializeServices()` يستدعي `reactionLoader.Load()` بنفسه وينشئ `ReactionDB` منفصلاً لـ `ChemistryEngine` (المستخدم في `TimeBasedPourUseCase`) | `V3Bootstrap.cs:~125-145` | متوسطة (مخالفة Rule 15) |
| D9 | ProductionBootstrapper منطقة تركيب مهمة وعالية الخطورة | **لا يُستدعى من أي مكان.** صنف عادي (ليس MonoBehaviour) وبلا أي `new ProductionBootstrapper(` في المشروع، والإشارة الوحيدة تعليق في `VesselDragPresenter.cs:11`. أي أنه حالياً مسار تركيب ميت، وليس جزءاً من تشغيل LabV3 | grep كامل | معلوماتية (لا تحذفه بعد) |
| D10 | ConditionPipeline المؤقت في `Application.Implementation.UseCases` | الملف فعلياً في `Scripts/Core/ConditionPipeline.cs`، وصنف فارغ (`CreateDefault()` فقط) **وغير مستخدم من أي مكان**. حتى `ProductionBootstrapper` يستخدم الاسم المؤهل `ChemLabSimV3.Engine.ConditionPipeline` صراحةً | grep | منخفضة |
| D11 | — | ملفات في مجلد `Core/` تعلن namespaces مثل `ChemLabSimV3.Infrastructure.Logging/Audio/Persistence/EventBus` و`Application.Implementation.*`. المجلد لا يطابق الـ namespace | grep `^namespace` | منخفضة (تشويش معماري) |
| D12 | — | يوجد مشهدا Boot: `Assets/Boot.unity` (المستخدم في Build Settings) و`Assets/_Project/Scenes/Boot.unity`، وأيضاً `Assets/_Project/Scenes/Lab Scene/Menu.unity` كنسخة ثانية من Menu | `git ls-files`, `EditorBuildSettings.asset` | منخفضة |
| D13 | `.sln` يشير إلى csproj مفقودة (~50) | مؤكّد: 50 إشارة و50 مفقودة و0 ملفات csproj في الجذر | فحص المسارات | منخفضة (ليست مشكلة Unity) |
| D14 | Unity compile = 0 errors | **لا أستطيع التحقق منه الآن**: آخر `Editor.log` على الجهاز يخص مشروعاً آخر (TimeRush)، ولا يوجد سجل محرر حديث لـ ChemLabSim. لا أنفي النتيجة، لكنها غير مؤكدة في هذه الجلسة | `~/.config/unity3d/Editor.log` | معلوماتية |
| D15 | — | `docs/ai/` كله **غير متتبع في git** (untracked)، و`copilot-instructions.md` موجود في مسار غريب `.github/workflows/.github/` | `git status` | منخفضة |
| D16 | README | README قديم: Unity 2023 LTS بدل 2022.3.62f3، ويعرض `Lab Scene.unity` و`LabController` كمسار رئيسي، ولا يذكر V3/LabV3 | `README.md` | منخفضة (انظر القسم H) |

### ملاحظات جودة بيانات في `reactions.json` (147 سجل)
- **9 معرّفات مكررة** (`rxn_107`..`rxn_115`).
- **16 تصادماً في مفتاح المتفاعلات** (مثل `H2O|Na`, `CuSO4|Fe`, `CH4|O2`): `ReactionRegistry` يحتفظ بالأول فقط، والباقي لن يُعثر عليه أبداً.
- **20 سجلاً بمتفاعل واحد** (تحلل، مثل `rxn_028 NaHCO3`, `rxn_101 H2O2`). `AppManager.ValidateLoadedDatabase` يرفض أي سجل بأقل من متفاعلين، وستفشل المصادقة لو حُمّلت الـ 147. **ولكن** `ReactionDatabase` يُسنَد قبل المصادقة (`AppManager.cs: ReactionDatabase = loader.Load(); return Validate...`)، فتبقى قاعدة "غير صالحة" مكشوفة للمستهلكين مع رسالة خطأ فقط.

---

## A. فهم المعمارية الحالية

- **مساران متعايشان:**
  - `_Project` (Legacy، assembly `ChemLabSim.Core`): `AppManager`, `SecureReactionLoader`, `ReactionDB/ReactionEntry` (في `ReactionModels.cs`، بلا namespace)، `ReactionEvaluator`, `LabController` (ضخم)، `CryptoUtil/KeyMaterial`.
  - `_ProjectV3` (assembly `ChemLabSimV3`، يعتمد على `ChemLabSim.Core`): Controllers / Views / Engine / Engine.Chemistry / Engine.SimulationV1 / Services / Core (ServiceLocator, EventBus, DomainEventBus) / Domain / Presentation / Application / Production.
- **Assemblies:** `ChemLabSim.Core` → `ChemLabSimV3` → (`ChemLabSimV3.Editor`, `ChemLabSimV3.EditModeTests`). الاتجاه سليم، ولا يوجد اعتماد عكسي.
- **جذر التركيب الفعلي في LabV3:** `V3Bootstrap` (MonoBehaviour في المشهد) يسجّل الخدمات في `Awake`، ثم في `Start` يستدعي `Init()` لكل `V3ControllerBase`. `ProductionBootstrapper` غير مستخدم.
- **مسار المزج الحالي (بعد تعديلات C# غير الملتزمة):**
  `LabInputController` → `MixRequest` → `ReactionController.OnMix` → `ReactionEngine.ProcessDetailed` (← `ReactionRegistry.Find` + `ConditionPipeline` في Engine) → `ReactionEvaluationAdapter` يحوّل النتيجة للشكل القديم → `EventBus.Publish(ReactionEvaluatedEvent)` → Views/FX. وإن كانت النتيجة Success/Partial: `SimulationStepper.StartSimulation` + `SimulationBridge` (يُنشآن وقت التشغيل لأنهما غير موجودين في المشهد).
- **نسخة HEAD الملتزمة** من `ReactionController` كانت تستخدم `ReactionEvaluator` القديم مباشرة. الفرق مهم عند أي مقارنة.
- **ReactionState:** `Engine.Chemistry.ReactionState` هو المستخدم فعلياً (Controllers, SimulationStepper, Views, Events). أما `Engine.SimulationV1.ReactionState` فمحصور داخل مجلد SimulationV1 وأداتي محرر (`SimulationV1SetupTool`, `LabPlayableSceneBuilder`). لا يوجد تداخل استخدام حالياً، لكنه خطر تسمية.

## B. الحالة وقت التشغيل (استنتاج من الكود، غير مُجرَّب في Play Mode)

عند فتح LabV3 مباشرة والضغط على Play، المتوقع حسب الكود:
1. `AppManager.Awake` (على AppRoot): يحمّل القاعدة. في المحرر: تحذير فشل قراءة الـ JSON + تحذير أن الـ bytes أقدم من الـ JSON، ثم `[AppManager] Reactions loaded and validated: 12`.
2. `V3Bootstrap.Awake`: يحمّل نسخة ثانية مستقلة لـ ChemistryEngine (نفس التحذيرات مرة ثانية + `[ReactionRegistry] Indexed 12 ...`).
3. `V3Bootstrap.Start` → `ReactionController.OnInit`: `Initialized with 12 reactions`.
4. القوائم المنسدلة تعرض فقط متفاعلات الـ 12 تفاعلاً.
5. **رسالة "Reaction database is unavailable." لا ينبغي أن تظهر** ما دام AppRoot موجوداً والـ bytes سليمة (HMAC سليم تحققت منه).

ترتيب التهيئة آمن: كل `Awake` يسبق كل `Start`، وقراءة المتحكمات للقاعدة تتم في `Start`.

## C. تدفق قاعدة البيانات

```
reactions.json (147, مصفوفة) ──X──> لا يُقرأ (JsonUtility يرفض المصفوفة)
reactions.bytes (12, كائن, AES-CBC+HMAC) ──> SecureReactionLoader.Load()
      │                                             │
      ├── AppManager.Awake → ReactionDatabase (المالك المقصود)
      │       ├── ReactionController → new ReactionEngine(db) → ReactionRegistry
      │       ├── LabInputController (القوائم) / GuidanceController
      │       └── ProductionBootstrapper (ميت)
      └── V3Bootstrap.InitializeServices → ReactionDB ثانٍ → ChemistryEngine → TimeBasedPourUseCase
```

ربط المشهد في LabV3 (تحققت منه في YAML):
- GameObject `AppRoot` جذري (في `SceneRoots`) وفعّال، ويحمل `SecureReactionLoader` + `AppManager`.
- `AppManager.loader` → `{fileID: 2147000103}` = مكوّن `SecureReactionLoader` في نفس الكائن ✔
- `SecureReactionLoader.encryptedBytes` → GUID `9a91dd34...` = `Assets/_Project/DataSecure/reactions.bytes` ✔
- GUIDs السكربتات تطابق `AppManager.cs.meta` و`SecureReactionLoader.cs.meta` ✔

حالة الدخول من Boot → Menu → LabV3: `AppManager` من Boot مستمر (DontDestroyOnLoad)، فيدمّر AppManager في LabV3 **كامل كائن AppRoot** بما فيه الـ loader. الـ Destroy مؤجل لنهاية الإطار، فـ `V3Bootstrap` سيجد غالباً loader، لكن هذا يستحق اختباراً (الاختبار 5).

## D. المخاطر الحالية

1. **القاعدة الفعلية 12 وليست 147**، ومسار إعادة التشفير مكسور بسبب صيغة المصفوفة (D1–D3). هذا أكبر خطر على الرؤية التعليمية، وهو فخّ لأي شخص يضغط "Encrypt" الآن.
2. **مالكان للقاعدة** (AppManager وV3Bootstrap) → نسختان في الذاكرة قد تختلفان مستقبلاً.
3. **المصادقة لا تمنع الكشف:** `ReactionDatabase` يُسنَد قبل التحقق.
4. **تعديلات C# مهمة غير ملتزمة** (ReactionController ينتقل إلى ReactionEngine + SimulationStepper). أي `git checkout`/`clean` خاطئ يضيّعها، ونتيجة "compile clean" مرتبطة بها.
5. **بيانات:** معرّفات مكررة، تصادم مفاتيح، سجلات بمتفاعل واحد لا يدعمها مسار المزج الحالي (يتطلب ≥2).
6. **CI** سيفشل في خطوة التحقق من البيانات.
7. **تشويش namespaces/مجلدات** وأصناف مكررة الاسم (ConditionPipeline، ReactionState) ومسار تركيب ميت (ProductionBootstrapper).
8. **Play Mode لم يُجرَّب**، وcompile غير مُعاد التحقق منه في هذه الجلسة.

## E. ما هو مستقر (بأدلة ثابتة)

- إصدار المحرر `2022.3.62f3` (`ProjectVersion.txt`).
- رسم الـ assemblies سليم وبلا دورات.
- ربط AppRoot في LabV3 صحيح بنيوياً (مراجع وGUIDs).
- `reactions.bytes` سليم تشفيرياً (HMAC صحيح) ويُحلَّل إلى `ReactionDB` صالح بـ 12 تفاعلاً كلها بمتفاعلين أو أكثر.
- `ReactionRegistry`/`ReactionEngine` يقبلان `ReactionDB` واحداً ويفهرسانه، ولا توجد تعريفات مكررة لـ `ReactionDB/ReactionEntry/ReactionRegistry/AppManager/SecureReactionLoader`.

## F. ما لم يُتحقق منه

- Play Mode بالكامل (P0 runtime).
- حالة compile الحالية (لا يوجد Editor.log حديث للمشروع).
- نتيجة EditMode tests.
- رسالة الاستثناء الدقيقة لفشل قراءة الـ JSON المصفوفة (استنتاج من سلوك JsonUtility المعروف، سيظهر في Console).
- مسار Boot → Menu → LabV3.
- جودة VFX/الاحتراق وغيرها (خارج نطاق هذه المرحلة).

## G. الخطوة الهندسية الأكثر أماناً التالية

**تنفيذ اختبار P0 في Play Mode بواسطتك (دون أي تعديل)، وتسجيل رسائل Console.** هذا يثبت أو ينفي نجاح ربط AppRoot، ويؤكد رقم 12 عملياً قبل أي قرار بخصوص الـ 147.

### خطوات الاختبار الدقيقة

التحضير:
1. افتح Unity 2022.3.62f3 على المشروع وانتظر انتهاء الـ compile. تأكد أن Console فيه **0 أخطاء حمراء** (سجّل عدد التحذيرات).
2. افتح `Assets/_ProjectV3/Scenes/LabV3.unity`.
3. في Console: فعّل Clear on Play، واترك Collapse معطلاً.
4. في Hierarchy تأكد من وجود `AppRoot` وأن Inspector يعرض: AppManager → Loader = AppRoot، وSecureReactionLoader → Encrypted Bytes = reactions.

الاختبار 0 (التهيئة):
5. اضغط Play. ابحث في Console عن:
   - `[AppManager] Reactions loaded and validated: N` ← سجّل N (المتوقع 12).
   - `[ReactionController] Initialized with N reactions.`
   - `[V3Bootstrap] Services initialized and registered.` و`Initialized X controller(s).`
   - تحذيرات `[SecureReactionLoader]` (فشل JSON / bytes أقدم): متوقعة، سجّل نصها حرفياً.
   - يجب **ألا** يظهر: `ReactionDB is unavailable at init` أو `loader reference missing`.
6. (اختياري) أثناء Play اختر AppRoot في Hierarchy وتأكد أنه موجود تحت DontDestroyOnLoad.

الاختبار 1 (تفاعل صحيح): A = HCl، B = NaOH، الحرارة الافتراضية، Medium = Neutral، ثم Mix.
   المتوقع: `[ReactionController] Evaluated 'rxn_001' → ...` ونتيجة معروضة، وربما `Live simulation started`.

الاختبار 2 (تفاعل صحيح ثانٍ): A = CaCO3، B = HCl، ثم Mix. المتوقع `Evaluated 'rxn_009' → ...`.
   (بديل: Zn + HCl = rxn_005.)

الاختبار 3 (تركيبة غير صحيحة): A = NaCl، B = O2، ثم Mix.
   المتوقع: رسالة "Unknown Combination" أو "no reaction"، و**ليس** "Reaction database is unavailable."

الاختبار 4 (الاتساق): Stop ثم Play مرة أخرى، وكرّر الاختبار 1. يجب أن يعطي نفس النتيجة ونفس رقم N.

الاختبار 5 (اختياري، مسار الإنتاج): افتح `Assets/Boot.unity` ثم Play وانتقل إلى LabV3 عبر القائمة إن وُجد زر لها، وكرّر الاختبار 1. تحقق من عدم ظهور `No SecureReactionLoader found`.

أرسل لي: عدد الأخطاء/التحذيرات قبل Play، ونص سطور Console أعلاه، ونتيجة كل اختبار (PASS/FAIL).

---

## H. README: الفروقات والتعديلات المقترحة (لم تُطبَّق)

| الحالي في README | المقترح |
|---|---|
| شارة `Unity-2023_LTS` | `Unity-2022.3.62f3` |
| الجدول: `Lab Scene.unity` هو "Main lab UI" | إضافة `Assets/_ProjectV3/Scenes/LabV3.unity` كمشهد المختبر V3، ووصف `Lab Scene.unity` بأنه مسار Legacy |
| `LabController` كمنسق رئيسي | إضافة صفوف V3: `V3Bootstrap` (جذر التركيب)، `ReactionController`، `ReactionEngine`/`ReactionRegistry`، `LabInputController`، `SimulationStepper` |
| `ReactionEvaluator` كمحرك التقييم | "Legacy evaluator؛ V3 يستخدم `ReactionEngine` ومحوّل `ReactionEvaluationAdapter`" (بعد التزام التعديلات الحالية) |
| لا يذكر عدد التفاعلات أو حالة البيانات | ملاحظة: "`reactions.json` هو المصدر المؤلَّف؛ `reactions.bytes` هو ما يُحمَّل وقت التشغيل ويجب إعادة توليده بعد تعديل المصدر" (بدون ادعاء 147 حتى يُحل D2) |
| "Production-safe" و"Strong Educational MVP+" | صياغة أدق: "Compile clean (تم التحقق سابقاً)؛ التحقق من Play Mode لمختبر LabV3 جارٍ" |
| قسم Project Structure لا يذكر `_ProjectV3` وassemblies | إضافة سطر عن `ChemLabSim.Core` و`ChemLabSimV3` وأن المساران متعايشان |
| لا إشارة لوثائق المطورين | رابط إلى `docs/ai/README.md` و`docs/ai/constitution-v3.md` |

---

## اقتراحات (للمراحل القادمة، لا شيء منها ينفَّذ الآن)

1. **P0b (بعد نجاح اختبار Play Mode):** قرار واحد لصيغة البيانات: إما تغليف `reactions.json` بـ `{ "reactions": [...] }` ثم إعادة التشفير، أو جعل `ReactionPacker` يغلّف المصفوفة قبل التشفير. الخيار الثاني يحافظ على ملف المصدر كما هو. قبل ذلك يجب قرار بخصوص الـ 20 سجلاً ذات المتفاعل الواحد، وإلا ستفشل مصادقة AppManager.
2. التزام (commit) التعديلات الحالية غير الملتزمة في فرع منفصل بعد مراجعتها، لحمايتها من الضياع. وفصل تغييرات تخطيط UI في LabV3 عن إضافة AppRoot إن أمكن.
3. توحيد مالك القاعدة: جعل `V3Bootstrap` يأخذ `AppManager.Instance.ReactionDatabase` بدل تحميل نسخة ثانية (P1).
4. نقل إسناد `ReactionDatabase` إلى ما بعد نجاح المصادقة في `AppManager` (P1، تغيير سطر واحد لكنه يغير السلوك).
5. تنظيف البيانات: المعرّفات المكررة وتصادمات المفاتيح (P1/P2).
6. إصلاح خطوة CI لتدعم المصفوفة، وتتبع `docs/ai/` في git.
7. بعدها فقط: قرار موثّق حول `ProductionBootstrapper` الميت و`ConditionPipeline` الفارغ و`SimulationV1`.
