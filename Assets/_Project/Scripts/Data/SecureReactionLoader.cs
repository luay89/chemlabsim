/*
 * Purpose: Decrypts the secure reactions blob at runtime and exposes the parsed ReactionDB for consumers like BootSmokeTest.
 * Path: Assets/_Project/Scripts/Data/SecureReactionLoader.cs
 * How to use: Assign the encrypted reactions TextAsset (produced via Tools/Security/Encrypt...) then call Load() when you need access to the DB.
 */
using System;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class SecureReactionLoader : MonoBehaviour
{
    private const int MinimumBlobLength = 16 + 32 + 1; // IV + HMAC + at least 1 byte of cipher text
    private const string SourceJsonPath = "Assets/_Project/DataSrc/reactions.json";

    [Tooltip("Encrypted bytes produced by Tools/Security/Encrypt Reactions JSON -> bytes")]
    public TextAsset encryptedBytes;

    public ReactionDB LastLoaded { get; private set; }

    public ReactionDB Load()
    {
        if (!encryptedBytes)
        {
            Debug.LogError("[SecureReactionLoader] Missing encryptedBytes reference. Assign reactions.bytes from DataSecure.", this);
            return null;
        }

#if UNITY_EDITOR
        WarnIfEncryptedBlobOutdated();
#endif

        byte[] blob = encryptedBytes.bytes;
        if (blob == null || blob.Length < MinimumBlobLength)
        {
            Debug.LogError($"[SecureReactionLoader] Blob length {blob?.Length ?? 0} is invalid. Run the ReactionPacker to regenerate reactions.bytes.", this);
            return null;
        }

        byte[] masterKey = KeyMaterial.DeriveKey32();
        byte[] aad = KeyMaterial.Aad();
        byte[] plaintext = null;

        try
        {
            plaintext = CryptoUtil.DecryptAesCbcHmac(masterKey, blob, aad);
            string json = Encoding.UTF8.GetString(plaintext);

            var parsed = JsonUtility.FromJson<ReactionDB>(json);
            if (parsed == null || parsed.reactions == null)
            {
                Debug.LogError("[SecureReactionLoader] Failed to parse ReactionDB JSON. Inspect reactions.json formatting.", this);
                return null;
            }

            if (parsed.reactions.Count == 0)
            {
                Debug.LogError("[SecureReactionLoader] Database Empty: reactions array is empty.", this);
                return null;
            }

            LastLoaded = parsed;
            return parsed;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureReactionLoader] Decryption or parsing failed: {ex.Message}", this);
            return null;
        }
        finally
        {
            if (plaintext != null) Array.Clear(plaintext, 0, plaintext.Length);
            if (masterKey != null) Array.Clear(masterKey, 0, masterKey.Length);
        }
    }

#if UNITY_EDITOR
    private void WarnIfEncryptedBlobOutdated()
    {
        if (!File.Exists(SourceJsonPath))
            return;

        string encryptedAssetPath = AssetDatabase.GetAssetPath(encryptedBytes);
        if (string.IsNullOrWhiteSpace(encryptedAssetPath) || !File.Exists(encryptedAssetPath))
            return;

        DateTime srcTime = File.GetLastWriteTimeUtc(SourceJsonPath);
        DateTime encTime = File.GetLastWriteTimeUtc(encryptedAssetPath);
        if (srcTime <= encTime)
            return;

        Debug.LogWarning(
            "[SecureReactionLoader] reactions.bytes appears older than reactions.json. " +
            "Run Tools/Security/Encrypt Reactions JSON -> bytes to avoid stale encrypted data.",
            this
        );
    }
#endif
}
