/*
 * Purpose: Quick sanity harness to ensure encrypted reaction content loads without opening the Unity GUI tools.
 * Path: Assets/_Project/Scripts/Core/BootSmokeTest.cs
 * How to use: Attach to an application root object, assign SecureReactionLoader, and read the console for the count log.
 */
using UnityEngine;

public class BootSmokeTest : MonoBehaviour
{
    [Tooltip("SecureReactionLoader component responsible for decrypting reactions.")]
    public SecureReactionLoader loader;

    private void Start()
    {
        if (!loader)
        {
            Debug.LogError("[BootSmokeTest] Missing SecureReactionLoader reference.", this);
            return;
        }

        var db = loader.Load();
        if (db == null || db.reactions == null)
        {
            Debug.LogError("[BootSmokeTest] SecureReactionLoader failed. Check previous errors for details.", this);
            return;
        }

        if (db.reactions.Count == 0)
        {
            Debug.LogError("[BootSmokeTest] Database Empty: reactions count is 0.", this);
            return;
        }

        Debug.Log($"[BootSmokeTest] Loaded reactions count: {db.reactions.Count}", this);
    }
}
