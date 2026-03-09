#if UNITY_EDITOR
/*
 * Purpose: Editor-only utility that encrypts Assets/_Project/DataSrc/reactions.json into a runtime-safe blob.
 * Path: Assets/_Project/Scripts/Security/Editor/ReactionPacker.cs
 * How to use: From the Unity toolbar choose Tools/Security/Encrypt Reactions JSON -> bytes before building.
 */
using System;
using System.IO;
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
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath)!);

            byte[] plaintext = File.ReadAllBytes(SrcPath);
            byte[] masterKey = KeyMaterial.DeriveKey32();
            byte[] aad = KeyMaterial.Aad();

            byte[] blob = CryptoUtil.EncryptAesCbcHmac(masterKey, plaintext, aad);

            File.WriteAllBytes(OutPath, blob);
            AssetDatabase.ImportAsset(OutPath);

            Debug.Log($"[ReactionPacker] Encrypted reactions saved: {OutPath} (len={blob.Length})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ReactionPacker] Encryption failed: {ex.Message}");
        }
    }
}
#endif