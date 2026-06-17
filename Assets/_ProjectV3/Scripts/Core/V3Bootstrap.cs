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

        // Chemistry engine dependencies
        [Header("Chemistry Engine (optional)")]
        [SerializeField] private SecureReactionLoader reactionLoader;

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
            var pourParticleService = gameObject.GetComponent<UnityPourParticleService>()
                                      ?? gameObject.AddComponent<UnityPourParticleService>();
            ServiceLocator.Register<IPourParticleService>(pourParticleService);

            // Domain event bus
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


            // ChemistryEngine: resolve from ServiceLocator or instantiate if missing
            ChemLabSimV3.Engine.Chemistry.ChemistryEngine chemistryEngine = null;
            if (ServiceLocator.Has<ChemLabSimV3.Engine.Chemistry.ChemistryEngine>())
            {
                chemistryEngine = ServiceLocator.Get<ChemLabSimV3.Engine.Chemistry.ChemistryEngine>();
            }
            else
            {
                try
                {
                    if (reactionLoader == null)
                    {
                        reactionLoader = GetComponent<SecureReactionLoader>() ?? FindObjectOfType<SecureReactionLoader>();
                    }
                    if (reactionLoader != null)
                    {
                        var db = reactionLoader.Load();
                        if (db != null)
                        {
                            chemistryEngine = new ChemLabSimV3.Engine.Chemistry.ChemistryEngine(db);
                            ServiceLocator.Register(chemistryEngine);
                        }
                        else
                        {
                            Debug.LogWarning("[V3Bootstrap] SecureReactionLoader returned null ReactionDB. ChemistryEngine will be unavailable.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[V3Bootstrap] No SecureReactionLoader found. ChemistryEngine will be unavailable.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[V3Bootstrap] ChemistryEngine initialization failed: {ex.Message}");
                }
            }

            // Application use case: time-based pour. Inject resolved chemistryEngine.
            var pourUseCase = new ChemLabSimV3.Application.UseCases.TimeBasedPourUseCase(chemistryEngine, eventBus);
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
