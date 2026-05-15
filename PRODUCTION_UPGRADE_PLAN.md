# ChemLabSim Production-Ready Architecture Upgrade Plan

## Current State (Before)
```
Views (15+ files)
  ↓
UIController (God Object) — مسؤول عن:
  - بناء ViewModels
  - ربط Events
  - تحديث Views
  ↓
ReactionController (MonoBehaviour) — مرتبط بـ:
  - Unity Engine
  - GameObjects
  - SimulationStepper
  ↓
ReactionEngine (Pure C#) ✓
  ↓
Services (SaveService, AudioService, etc.)
```

**المشاكل:**
- ReactionController = God Object (غير قابل للاختبار)
- UIController = God Object (كل العمل في مكان واحد)
- لا توجد Interfaces واضحة
- تسرب اعتماديات (Coupling)
- Events غير منظمة
- لا توجد Separation of Concerns

---

## Target State (After)
```
┌─────────────────────────────────────────────┐
│            PRESENTATION LAYER               │
│  Views + Presenters (UI Binding Layer)      │
├─────────────────────────────────────────────┤
│         APPLICATION LAYER                   │
│  Use Cases (Business Operations)            │
│  - EvaluateReactionUseCase                  │
│  - ApplyConditionsUseCase                   │
│  - CalculateYieldUseCase                    │
│  - etc.                                     │
├─────────────────────────────────────────────┤
│           DOMAIN LAYER (Pure C#)            │
│  - ReactionEngine                           │
│  - ChemistryEngine                          │
│  - ConditionPipeline                        │
│  - (NO Unity dependencies)                  │
├─────────────────────────────────────────────┤
│        INFRASTRUCTURE LAYER                 │
│  Interfaces:                                │
│  - IReactionRepository                      │
│  - ISaveService                             │
│  - IAudioService                            │
│  - IDomainEventBus                          │
│  - ILogger (Debug Mode)                     │
└─────────────────────────────────────────────┘
```

---

## Phase 1: Extract Domain Layer (Pure C# — NO Unity)

### 1.1 Verify ReactionEngine is Pure C#
- ✅ Done: ReactionEngine, ChemistryEngine already pure C#
- ✅ Done: Engine/* files have no UnityEngine references

### 1.2 Extract Condition Evaluation (Already Done)
- ✅ ICondition interface exists
- ✅ ConditionPipeline exists
- Verify no UnityEngine in Engine/

---

## Phase 2: Create Application Layer (Use Cases)

### 2.1 Extract Use Cases from ReactionController

**MixReactionUseCase.cs**
```csharp
public interface IMixReactionUseCase
{
    ReactionOutput Execute(MixRequest request);
}

public class MixReactionUseCase : IMixReactionUseCase
{
    private readonly IReactionRepository _repo;
    private readonly IDomainEventBus _eventBus;
    private readonly ILogger _logger;

    public ReactionOutput Execute(MixRequest request)
    {
        // Pure logic — NO Unity
        // Validate → Engine.Process → Return result
    }
}
```

**ApplyConditionsUseCase.cs**
```csharp
public interface IApplyConditionsUseCase
{
    PipelineResult Execute(ReactionEntry reaction, ConditionInput input);
}

public class ApplyConditionsUseCase : IApplyConditionsUseCase
{
    private readonly IConditionFactory _factory;

    public PipelineResult Execute(ReactionEntry reaction, ConditionInput input)
    {
        // Evaluate conditions → return PipelineResult
    }
}
```

**CalculateYieldUseCase.cs**
- Extract stoichiometry logic
- Return yield percentage

**GenerateQuizUseCase.cs**
- Extract quiz generation from QuizController

**SaveProgressUseCase.cs**
- Extract progress saving logic

### 2.2 Create UseCase Interfaces (Contracts)
```csharp
namespace ChemLabSimV3.Application.UseCases
{
    public interface IUseCase<TInput, TOutput>
    {
        TOutput Execute(TInput input);
    }

    // Markers
    public interface IMixReactionUseCase : IUseCase<MixRequest, ReactionOutput> { }
    public interface IApplyConditionsUseCase : IUseCase<ConditionInput, PipelineResult> { }
    public interface ICalculateYieldUseCase : IUseCase<YieldCalculationInput, YieldResult> { }
}
```

---

## Phase 3: Create Presentation Layer (Presenters)

### 3.1 Extract Presenters from UIController

**ReactionPresenter.cs**
```csharp
public class ReactionPresenter : IDisposable
{
    private readonly IMixReactionUseCase _mixUseCase;
    private readonly IReactionResultViewBinder _viewBinder;
    private readonly IDomainEventBus _eventBus;

    public void OnMixRequested(MixRequest request)
    {
        var result = _mixUseCase.Execute(request);
        
        var vm = MapToViewModel(result);
        _viewBinder.Bind(vm);
        
        _eventBus.Publish(new ReactionProcessedEvent(result));
    }

    private ReactionResultViewModel MapToViewModel(ReactionOutput output)
    {
        // Pure mapping — NO business logic
    }
}
```

**ProgressPresenter.cs**
```csharp
public class ProgressPresenter
{
    private readonly IProgressUseCase _progressUseCase;
    private readonly IProgressViewBinder _viewBinder;

    public void OnProgressUpdated(ProgressUpdatedEvent evt)
    {
        var vm = _progressUseCase.CalculateViewModel(evt);
        _viewBinder.Bind(vm);
    }
}
```

### 3.2 Create View Binders (Presenters)
```csharp
public interface IReactionResultViewBinder
{
    void Bind(ReactionResultViewModel vm);
}

public class ReactionResultViewBinder : IReactionResultViewBinder
{
    private readonly ReactionResultView _view;

    public void Bind(ReactionResultViewModel vm)
    {
        _view.SetHeadline(vm.Headline);
        _view.SetExplanation(vm.Explanation);
        _view.SetSafety(vm.SafetyNote);
    }
}
```

---

## Phase 4: Create Infrastructure Interfaces

### 4.1 Repository Interfaces
```csharp
namespace ChemLabSimV3.Infrastructure
{
    public interface IReactionRepository
    {
        ReactionEntry GetById(string id);
        IEnumerable<ReactionEntry> GetByReagents(List<string> reagentNames);
        int Count { get; }
    }

    public interface IQuizRepository
    {
        QuizQuestion GetForReaction(string reactionId);
        QuizQuestion GetRandom();
    }

    public interface IAchievementRepository
    {
        AchievementDef GetById(string id);
        IEnumerable<AchievementDef> GetAll();
    }
}
```

### 4.2 Service Interfaces
```csharp
public interface ISaveService
{
    void SaveProgress(PlayerProgress progress);
    PlayerProgress LoadProgress();
    void SaveNotebookEntry(NotebookEntry entry);
}

public interface IAudioService
{
    void PlaySound(string soundId);
    void PlayMusic(string musicId);
}

public interface ILogger
{
    void Log(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogReactionSteps(ReactionOutput output);
}
```

### 4.3 Event Bus Interface
```csharp
public interface IDomainEventBus
{
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;
    void Publish<TEvent>(TEvent evt) where TEvent : IDomainEvent;
}

public interface IDomainEvent { }

public struct ReactionEvaluatedEvent : IDomainEvent
{
    public ReactionOutput Output { get; set; }
    public long TimestampMs { get; set; }
}
```

---

## Phase 5: Refactor Controllers to Adapters

### 5.1 ReactionController → ReactionAdapter (MonoBehaviour wrapper)
```csharp
public class ReactionAdapter : V3ControllerBase
{
    private IMixReactionUseCase _mixUseCase;
    private IApplyConditionsUseCase _conditionsUseCase;
    private ReactionPresenter _presenter;
    private IDomainEventBus _eventBus;

    protected override void OnInitialize()
    {
        // Inject use cases & presenters
        _mixUseCase = ServiceLocator.Get<IMixReactionUseCase>();
        _conditionsUseCase = ServiceLocator.Get<IApplyConditionsUseCase>();
        _presenter = new ReactionPresenter(_mixUseCase);
        _eventBus = ServiceLocator.Get<IDomainEventBus>();
    }

    public void RequestMix(MixRequest request)
    {
        _presenter.OnMixRequested(request);
    }
}
```

### 5.2 UIController → UIAdapter (MonoBehaviour wrapper)
```csharp
public class UIAdapter : V3ControllerBase
{
    private ReactionPresenter _reactionPresenter;
    private ProgressPresenter _progressPresenter;
    private QuizPresenter _quizPresenter;
    private IDomainEventBus _eventBus;

    protected override void OnInitialize()
    {
        _eventBus = ServiceLocator.Get<IDomainEventBus>();
        
        _eventBus.Subscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
        _eventBus.Subscribe<ProgressUpdatedEvent>(OnProgressUpdated);
        _eventBus.Subscribe<QuizGeneratedEvent>(OnQuizGenerated);
    }

    private void OnReactionEvaluated(ReactionEvaluatedEvent evt)
    {
        _reactionPresenter.OnReactionEvaluated(evt);
    }
}
```

---

## Phase 6: Null Safety & Debug Mode

### 6.1 Guard Clauses & Fallbacks
- Add null checks to all use cases
- Fallback ViewModels on error

### 6.2 Debug Mode
```csharp
public interface IDebugMode
{
    bool IsEnabled { get; }
    void LogReactionSteps(ReactionOutput output);
    void LogConditionEvaluation(PipelineResult result);
}

// Usage in UseCase:
if (_debugMode.IsEnabled)
{
    _debugMode.LogReactionSteps(output);
}
```

### 6.3 Logger Integration
- Replace Debug.Log with ILogger
- Enable production logging

---

## Phase 7: Testing Strategy

### 7.1 Domain Layer (Pure Unit Tests)
```csharp
[TestFixture]
public class MixReactionUseCaseTests
{
    [Test]
    public void Execute_WithValidRequest_ReturnsReactionOutput()
    {
        // Arrange
        var mockRepo = new MockReactionRepository();
        var useCase = new MixReactionUseCase(mockRepo);
        var request = new MixRequest { /* ... */ };

        // Act
        var result = useCase.Execute(request);

        // Assert
        Assert.IsTrue(result.Found);
    }
}
```

### 7.2 Presenter Tests
```csharp
[TestFixture]
public class ReactionPresenterTests
{
    [Test]
    public void OnMixRequested_CallsViewBinder()
    {
        // Arrange
        var mockUseCase = new MockMixReactionUseCase();
        var mockBinder = new MockViewBinder();
        var presenter = new ReactionPresenter(mockUseCase, mockBinder);

        // Act
        presenter.OnMixRequested(new MixRequest());

        // Assert
        mockBinder.VerifyBindCalled();
    }
}
```

---

## Implementation Timeline

| Phase | Files | Effort | Status |
|-------|-------|--------|--------|
| 1. Verify Domain Layer | 10 | 1h | ⏳ IN PROGRESS |
| 2. Create Use Cases | 15+ | 4h | ⏳ IN PROGRESS |
| 3. Create Presenters | 10+ | 3h | ⏳ IN PROGRESS |
| 4. Infrastructure Interfaces | 8 | 2h | ⏳ IN PROGRESS |
| 5. Refactor Controllers | 15 | 4h | ⏳ IN PROGRESS |
| 6. Null Safety & Debug | 20+ | 3h | ⏳ IN PROGRESS |
| 7. Testing | 30+ | 5h | ⏳ IN PROGRESS |
| **TOTAL** | **108+** | **22h** | ⏳ IN PROGRESS |

---

## Success Criteria

- [ ] All Domain logic is Pure C# (NO UnityEngine)
- [ ] Each UseCase has ONE responsibility
- [ ] All layers communicate via Interfaces
- [ ] UIController split into Presenters
- [ ] Zero coupling between Domain and UI
- [ ] All Unit Tests pass (Domain layer)
- [ ] Debug Mode enabled for production visibility
- [ ] Game runs without breaking existing functionality
- [ ] Code is production-ready for demo/pitch

