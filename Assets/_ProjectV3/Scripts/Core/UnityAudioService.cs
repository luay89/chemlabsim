using ChemLabSimV3.Infrastructure.Logging;

namespace ChemLabSimV3.Infrastructure.Audio
{
    /// <summary>Minimal audio service for V3.</summary>
    public class UnityAudioService
    {
        private readonly UnityLogger _logger;

        public UnityAudioService(UnityLogger logger)
        {
            _logger = logger;
        }

        public void PlayClip(string clipName)
        {
            _logger?.Log($"[AudioService] Play: {clipName}");
        }

        public void StopAll()
        {
            _logger?.Log("[AudioService] Stop all");
        }
    }
}
