// ChemLabSim v3 — Scene Service (Skeleton)
// Handles scene transitions with optional loading feedback.

using UnityEngine;
using UnityEngine.SceneManagement;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Services
{
    public class SceneService : IService
    {
        // v3 canonical scene names.
        public static class Scenes
        {
            public const string Boot         = "Boot";
            public const string Menu         = "Menu";
            public const string Lab          = "Lab Scene";  // matches v2 name for now
            public const string Achievements = "Achievements";
            public const string Settings     = "Settings";
        }

        public void Initialize()
        {
            Debug.Log("[SceneService] Initialized.");
        }

        public void Dispose() { }

        /// <summary>Load a scene by name. Publishes <see cref="SceneTransitionEvent"/> before loading.</summary>
        public void LoadScene(string sceneName)
        {
            Debug.Log($"[SceneService] Transitioning to: {sceneName}");
            EventBus.Publish(new SceneTransitionEvent { TargetScene = sceneName });
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>Load a scene asynchronously. Returns the AsyncOperation for progress tracking.</summary>
        public AsyncOperation LoadSceneAsync(string sceneName)
        {
            Debug.Log($"[SceneService] Async transition to: {sceneName}");
            EventBus.Publish(new SceneTransitionEvent { TargetScene = sceneName });
            return SceneManager.LoadSceneAsync(sceneName);
        }

        /// <summary>Name of the currently active scene.</summary>
        public string CurrentSceneName => SceneManager.GetActiveScene().name;
    }
}
