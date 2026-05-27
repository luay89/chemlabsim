// ChemLabSim v3 — V3Bootstrap
// Initializes all v3 services and controllers. Attach to a GameObject in the Boot scene
// (or any scene during development) to spin up the v3 infrastructure.
//
// This component is independent of v2's AppManager — both can coexist.

using UnityEngine;
using ChemLabSimV3.Application.UseCases;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Domain.Events;
using ChemLabSimV3.Events;
using ChemLabSimV3.Infrastructure.EventBus;
using ChemLabSimV3.Infrastructure.Logging;
using ChemLabSimV3.Infrastructure.Services;
using ChemLabSimV3.Presentation.Presenters;
using ChemLabSimV3.Services;

namespace ChemLabSimV3.Core
{
    public class V3Bootstrap : MonoBehaviour
    {
        public static V3Bootstrap Instance { get; private set; }

        // -- Services (created at boot, live for app lifetime) ----------
        private SaveService saveService;
        private AudioService audioService;
        private LanguageService languageService;
        private SceneService sceneService;

        // -- Controllers (scene-level, discovered in current scene) -----
        [Header("Lab Controllers (optional — auto-discovered if null)")]
        [SerializeField] private ReactionController reactionController;
        [SerializeField] private UIController uiController;
        [SerializeField] private ProgressController progressController;
        [SerializeField] private AchievementController achievementController;
        [SerializeField] private ChallengeController challengeController;
        [SerializeField] private ObjectiveController objectiveController;
        [SerializeField] private QuizController quizController;
        [SerializeField] private FXController fxController;
        [SerializeField] private GuidanceController guidanceController;
        [SerializeField] private NotebookController notebookController;

        #pragma warning disable CS0414
        private bool initialized;
        #pragma warning restore CS0414

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeServices();
        }

        private void Start()
        {
            DiscoverAndInitControllers();
            initialized = true;
            Debug.Log("[V3Bootstrap] Infrastructure ready.");
        }

        private void OnDestroy()
        {
            TeardownServices();
            EventBus.Clear();
            ServiceLocator.Clear();
            if (Instance == this) Instance = null;
        }

        // -- Service Lifecycle ------------------------------------------

        private void InitializeServices()
        {
            saveService = new SaveService();
            audioService = new AudioService();
            languageService = new LanguageService();
            sceneService = new SceneService();

            saveService.Initialize();
            audioService.Initialize();
            languageService.Initialize();
            sceneService.Initialize();

            ServiceLocator.Register(saveService);
            ServiceLocator.Register(audioService);
            ServiceLocator.Register(languageService);
            ServiceLocator.Register(sceneService);

            // Presentation-layer infrastructure: liquid pour VFX.
            // Hosted on this bootstrap GameObject so it survives scene loads
            // alongside the rest of the v3 infrastructure (DontDestroyOnLoad).
            var pourParticleService = gameObject.GetComponent<UnityPourParticleService>()
                                      ?? gameObject.AddComponent<UnityPourParticleService>();
            ServiceLocator.Register<IPourParticleService>(pourParticleService);

            // Domain event bus — reused by use cases that need to notify
            // listeners (UI counters, fluid shaders, notebook entries, …).
            // Register once and only once; if another bootstrap (e.g.
            // ProductionBootstrapper) already provided one, prefer that.
            IDomainEventBus eventBus;
            if (ServiceLocator.Has<IDomainEventBus>())
            {
                eventBus = ServiceLocator.Get<IDomainEventBus>();
            }
            else
            {
                eventBus = new DomainEventBus(new UnityLogger());
                ServiceLocator.Register(eventBus);
            }

            // Application use case: time-based pour. Drives per-frame mass
            // transfer when a vessel is dragged into a target trigger zone.
            // ChemistryEngine is intentionally optional — passing null
            // disables live recompute but pour transfer still works.
            var pourUseCase = new TimeBasedPourUseCase(chemistryEngine: null, eventBus: eventBus);
            ServiceLocator.Register<ITimeBasedPourUseCase>(pourUseCase);

            Debug.Log("[V3Bootstrap] Services initialized and registered.");
        }

        private void TeardownServices()
        {
            saveService?.Dispose();
            audioService?.Dispose();
            languageService?.Dispose();
            sceneService?.Dispose();
        }

        // -- Controller Discovery & Init -------------------------------

        /// <summary>
        /// Finds all V3ControllerBase instances in the scene and calls Init().
        /// Supports both serialized references and runtime auto-discovery.
        /// </summary>
        private void DiscoverAndInitControllers()
        {
            V3ControllerBase[] controllers = FindObjectsOfType<V3ControllerBase>();

            if (controllers.Length == 0)
            {
                Debug.Log("[V3Bootstrap] No v3 controllers found in scene (expected during Boot/Menu).");
                return;
            }

            foreach (V3ControllerBase controller in controllers)
            {
                controller.Init();
            }

            Debug.Log($"[V3Bootstrap] Initialized {controllers.Length} controller(s).");
        }

        /// <summary>
        /// Re-discover controllers after a scene load. Call from scene entry points
        /// or subscribe to SceneManager.sceneLoaded.
        /// </summary>
        public void ReinitControllers()
        {
            DiscoverAndInitControllers();
        }
    }
}
