namespace ChemLabSimV3.Infrastructure.Logging
{
    /// <summary>Minimal ILogger implementation for Unity.</summary>
    public class UnityLogger
    {
        public bool DebugMode { get; set; }

        public void Log(string message)
        {
            if (DebugMode)
                UnityEngine.Debug.Log($"[UnityLogger] {message}");
        }

        public void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[UnityLogger] {message}");
        }

        public void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[UnityLogger] {message}");
        }
    }
}
