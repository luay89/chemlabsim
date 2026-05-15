using ChemLabSimV3.Infrastructure.Logging;
using UnityEngine;

namespace ChemLabSimV3.Infrastructure.Persistence
{
    /// <summary>Minimal save service using PlayerPrefs.</summary>
    public class PlayerPrefsSaveService
    {
        private readonly UnityLogger _logger;

        public PlayerPrefsSaveService(UnityLogger logger)
        {
            _logger = logger;
        }

        public void SaveString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
            _logger?.Log($"[SaveService] Saved {key}");
        }

        public string LoadString(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }

        public void SaveInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        public int LoadInt(string key, int defaultValue = 0)
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }
    }
}
