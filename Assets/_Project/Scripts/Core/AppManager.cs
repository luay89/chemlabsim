using UnityEngine;
using UnityEngine.SceneManagement;

public class AppManager : MonoBehaviour
{
    public static AppManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private SecureReactionLoader loader;

    public ReactionDB ReactionDatabase { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeDatabase();
    }

    private void Start()
    {
        if (!InitializeDatabase())
            return;

        // Only auto-advance when bootstrapping from Boot scene.
        // This allows opening Lab Scene directly without being redirected.
        if (string.Equals(SceneManager.GetActiveScene().name, "Boot", System.StringComparison.Ordinal))
        {
            SceneManager.LoadScene("Menu");
        }
    }

    private bool InitializeDatabase()
    {
        if (ReactionDatabase != null)
            return true;

        if (loader == null)
        {
            loader = FindObjectOfType<SecureReactionLoader>();
            if (loader == null)
            {
                Debug.LogError("[AppManager] loader reference missing. Drag SecureReactionLoader onto AppManager.");
                return false;
            }
        }

        try
        {
            ReactionDatabase = loader.Load();
            return ValidateLoadedDatabase(ReactionDatabase);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[AppManager] Initialization failed: " + ex);
            return false;
        }
    }

    /// <summary>
    /// Public entry point used by controllers (e.g. LabInputController) that need to
    /// guarantee the encrypted reactions blob has been decrypted and validated
    /// before they bind UI dropdowns. Safe to call repeatedly; returns true if a
    /// valid <see cref="ReactionDatabase"/> is available after the call.
    /// </summary>
    public bool EnsureDatabaseLoaded()
    {
        if (ReactionDatabase != null)
            return true;

        return InitializeDatabase();
    }

    private static int GetReactionCountSafe(ReactionDB db)
    {
        if (db == null) return 0;

        // Supports two cases:
        // 1) List => Count
        // 2) Array => Length
        // Without depending on a specific compile-time type
        var field = db.GetType().GetField("reactions");
        if (field == null) return 0;

        var value = field.GetValue(db);
        if (value == null) return 0;

        if (value is System.Collections.ICollection col)
            return col.Count;

        if (value is System.Array arr)
            return arr.Length;

        return 0;
    }

    private bool ValidateLoadedDatabase(ReactionDB db)
    {
        if (db == null)
        {
            Debug.LogError("[AppManager] Database load failed. SecureReactionLoader returned null.");
            return false;
        }

        int count = GetReactionCountSafe(db);
        if (count <= 0)
        {
            Debug.LogError("[AppManager] Database Empty: reactions count is 0. Check JSON source, encryption output, and assigned reactions.bytes.");
            return false;
        }

        // Scan every entry (do not stop at the first problem) so all issues are reported.
        int invalidCount = 0;
        var singleReactantIds = new System.Collections.Generic.List<string>();

        for (int i = 0; i < db.reactions.Count; i++)
        {
            ReactionEntry rx = db.reactions[i];
            if (rx == null)
            {
                Debug.LogError($"[AppManager] Invalid reaction at index {i}: entry is null.");
                invalidCount++;
                continue;
            }

            int reactantCount = rx.GetReactantFormulas().Count;
            int productCount = rx.GetProductFormulas().Count;
            if (reactantCount < 1 || productCount < 1)
            {
                Debug.LogError(
                    $"[AppManager] Invalid reaction at index {i} ('{rx.id}'): reactants/products are missing. " +
                    "Data schema does not match runtime model."
                );
                invalidCount++;
                continue;
            }

            // Single-reactant entries (decompositions, electrolysis, dissociation) are valid data,
            // but the mix flow currently requires at least two reactants, so they cannot be triggered yet.
            if (reactantCount == 1)
                singleReactantIds.Add($"{rx.id}@{i}");
        }

        if (singleReactantIds.Count > 0)
        {
            Debug.LogWarning(
                $"[AppManager] {singleReactantIds.Count} single-reactant reaction(s) loaded but not reachable " +
                $"from the two-reactant mix flow: {string.Join(", ", singleReactantIds)}"
            );
        }

        if (invalidCount > 0)
        {
            Debug.LogError($"[AppManager] Reactions loaded with {invalidCount} invalid entr(y/ies) out of {count}.");
            return false;
        }

        Debug.Log($"[AppManager] Reactions loaded and validated: {count}");
        return true;
    }
}
