#if UNITY_EDITOR
/*
 * Purpose: Editor-only utility that encrypts Assets/_Project/DataSrc/reactions.json into a runtime-safe blob.
 * Path: Assets/_Project/Scripts/Security/Editor/ReactionPacker.cs
 * How to use: From the Unity toolbar choose Tools/Security/Encrypt Reactions JSON -> bytes before building.
 */
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class ReactionPacker
{
    private const string SrcPath = "Assets/_Project/DataSrc/reactions.json";
    private const string OutPath = "Assets/_Project/DataSecure/reactions.bytes";

    [MenuItem("Tools/Security/Encrypt Reactions JSON -> bytes")]
    public static void Encrypt()
    {
        if (!File.Exists(SrcPath))
        {
            Debug.LogError($"[ReactionPacker] Missing source file: {SrcPath}");
            return;
        }

        try
        {
            string outDir = Path.GetDirectoryName(OutPath);
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            byte[] plaintext = File.ReadAllBytes(SrcPath);
            byte[] masterKey = KeyMaterial.DeriveKey32();
            byte[] aad = KeyMaterial.Aad();

            byte[] blob = CryptoUtil.EncryptAesCbcHmac(masterKey, plaintext, aad);

            File.WriteAllBytes(OutPath, blob);
            WriteManifestNextToBytes(OutPath, blob);

            AssetDatabase.ImportAsset(OutPath);
            AssetDatabase.ImportAsset("Assets/_Project/DataSecure/reactions.manifest.json");
            AssetDatabase.Refresh();

            Debug.Log($"[ReactionPacker] Encrypted reactions saved: {OutPath} (len={blob.Length})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ReactionPacker] Encryption failed: {ex.Message}");
        }
    }

    [Serializable]
    private class ReactionManifest
    {
        public int schemaVersion;
        public string dataVersion;
        public string bytesFileName;
        public long bytesLength;
        public string sha256Hex;
        public string generatedAtUtc;
    }

    private static void WriteManifestNextToBytes(string bytesAssetPath, byte[] bytesPayload)
    {
        var manifest = new ReactionManifest
        {
            schemaVersion = 1,
            dataVersion = "reactions-db-v1",
            bytesFileName = Path.GetFileName(bytesAssetPath),
            bytesLength = bytesPayload != null ? bytesPayload.LongLength : 0,
            sha256Hex = ComputeSha256Hex(bytesPayload),
            generatedAtUtc = DateTime.UtcNow.ToString("o")
        };

        string dir = Path.GetDirectoryName(bytesAssetPath) ?? "Assets/_Project/DataSecure";
        string manifestPath = Path.Combine(dir, "reactions.manifest.json");

        string json = JsonUtility.ToJson(manifest, true);
        File.WriteAllText(manifestPath, json);

        Debug.Log($"[ReactionPacker] Manifest saved: {manifestPath}");
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        data ??= Array.Empty<byte>();

        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data);

        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
#endif