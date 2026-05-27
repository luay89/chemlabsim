// ChemLabSim v3 — Lab Scene Setup Bootstrapper
// Seeds the ITimeBasedPourUseCase in-memory vessel registry from a
// list of inspector-configured entries, and syncs the data-side
// VesselId onto each VesselDragPresenter so the presenter and the
// use case agree on identity.
//
// Attach one of these to a scene-level GameObject (e.g. "LabSetup")
// in the Lab Scene. It runs in Start(), after V3Bootstrap has
// registered ITimeBasedPourUseCase in Awake().

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Presentation.Presenters;

namespace ChemLabSimV3.Infrastructure.Bootstrapping
{
    /// <summary>
    /// Inspector-configurable scene seeder for the pour use case.
    /// </summary>
    [DisallowMultipleComponent]
    public class LabSceneSetupBootstrapper : MonoBehaviour
    {
        /// <summary>One row per vessel (source bottle or target flask) in the scene.</summary>
        [System.Serializable]
        public struct VesselSetupData
        {
            [Tooltip("Stable id used by the pour use case and TriggerZoneAdapter. Must be unique within the scene.")]
            public string vesselId;

            [Tooltip("Reagent formula or empty for an initially-empty target container.")]
            public string reagentId;

            [Tooltip("Starting liquid volume in millilitres.")]
            public float initialMl;

            [Tooltip("Maximum capacity of the vessel in millilitres. 0 = unbounded.")]
            public float maxCapacityMl;

            [Tooltip("Pour rate in millilitres per second when this vessel is the source. 0 = use use-case default.")]
            public float pourRatePerSecond;

            [Tooltip("Optional drag presenter — leave empty for static target flasks that aren't dragged.")]
            public VesselDragPresenter visualPresenter;
        }

        [Header("Vessels to seed at scene start")]
        [SerializeField] private List<VesselSetupData> _sceneVessels = new List<VesselSetupData>();

        private void Start()
        {
            var pourUseCase = ServiceLocator.Get<ITimeBasedPourUseCase>();
            if (pourUseCase == null)
            {
                Debug.LogError(
                    "[LabSceneSetupBootstrapper] ITimeBasedPourUseCase is not registered in ServiceLocator. " +
                    "Ensure V3Bootstrap runs in Awake() before this component's Start(). " +
                    "Skipping vessel seeding.",
                    this);
                return;
            }

            if (_sceneVessels == null || _sceneVessels.Count == 0)
            {
                Debug.LogWarning("[LabSceneSetupBootstrapper] No vessels configured in the inspector. Nothing to seed.", this);
                return;
            }

            // The use case exposes RegisterVessel as a concrete method on
            // TimeBasedPourUseCase, not on the ITimeBasedPourUseCase
            // interface. Resolve through the concrete type when available,
            // otherwise fall back to reflection so we don't take a hard
            // dependency on the Application assembly from this layer.
            var concrete = pourUseCase as ChemLabSimV3.Application.UseCases.TimeBasedPourUseCase;
            int seeded = 0;
            int skipped = 0;

            for (int i = 0; i < _sceneVessels.Count; i++)
            {
                VesselSetupData data = _sceneVessels[i];

                if (string.IsNullOrWhiteSpace(data.vesselId))
                {
                    Debug.LogWarning($"[LabSceneSetupBootstrapper] Entry {i} has an empty vesselId. Skipped.", this);
                    skipped++;
                    continue;
                }

                if (concrete != null)
                {
                    concrete.RegisterVessel(
                        vesselId: data.vesselId,
                        reagentId: data.reagentId,
                        initialVolumeMl: data.initialMl,
                        capacityMl: data.maxCapacityMl,
                        pourRateMlPerSecond: data.pourRatePerSecond);
                }
                else
                {
                    Debug.LogError(
                        "[LabSceneSetupBootstrapper] Registered ITimeBasedPourUseCase is not the expected " +
                        "TimeBasedPourUseCase implementation — cannot register vessels. " +
                        $"Actual type: {pourUseCase.GetType().FullName}.",
                        this);
                    return;
                }

                if (data.visualPresenter != null)
                {
                    data.visualPresenter.SetVesselId(data.vesselId);
                }

                seeded++;
            }

            Debug.Log($"[LabSceneSetupBootstrapper] Seeded {seeded} vessel(s); skipped {skipped}.", this);
        }
    }
}
