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
            Debug.LogError("[AppManager] loader reference missing. Drag SecureReactionLoader onto AppManager.");
            return false;
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

    private static int GetReactionCountSafe(ReactionDB db)
    {
        if (db == null) return 0;

        // يدعم حالتين:
        // 1) List => Count
        // 2) Array => Length
        // بدون الاعتماد على نوع محدد وقت الكومبايل
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

        for (int i = 0; i < db.reactions.Count; i++)
        {
            ReactionEntry rx = db.reactions[i];
            if (rx == null)
            {
                Debug.LogError($"[AppManager] Invalid reaction at index {i}: entry is null.");
                return false;
            }

            string a = rx.GetReactantA();
            string b = rx.GetReactantB();
            string product = rx.GetPrimaryProduct();
            if (string.IsNullOrWhiteSpace(a) ||
                string.IsNullOrWhiteSpace(b) ||
                string.IsNullOrWhiteSpace(product))
            {
                Debug.LogError(
                    $"[AppManager] Invalid reaction at index {i}: reactants/products are missing. " +
                    "Data schema does not match runtime model."
                );
                return false;
            }
        }

        Debug.Log($"[AppManager] Reactions loaded and validated: {count}");
        return true;
    }
}
