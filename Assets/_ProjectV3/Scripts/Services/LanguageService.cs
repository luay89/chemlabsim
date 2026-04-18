// ChemLabSim v3 — Language Service
// English-only build. Service retained for compatibility with existing event subscribers
// and SaveService language index storage.

using UnityEngine;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Services
{
    public enum AppLanguage
    {
        English = 0
    }

    public class LanguageService : IService
    {
        public AppLanguage CurrentLanguage => AppLanguage.English;

        public void Initialize()
        {
            Debug.Log("[LanguageService] Initialized (English-only build).");
        }

        public void Dispose() { }

        /// <summary>Inline localization helper — always returns English.</summary>
        public string Localize(string english, string arabic)
        {
            return english;
        }
    }
}
