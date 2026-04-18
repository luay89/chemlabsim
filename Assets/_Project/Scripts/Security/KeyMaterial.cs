/*
 * Purpose: Centralizes HKDF key derivation material and associated data (AAD) for securing reaction blobs.
 * Path: Assets/_Project/Scripts/Security/KeyMaterial.cs
 * How to use: Call KeyMaterial.DeriveKey32() and KeyMaterial.Aad() before invoking CryptoUtil encryption/decryption helpers.
 */
using UnityEngine;

public static class KeyMaterial
{
    private static string S1() => "KX9q-";
    private static string S2() => "1pTz#";
    private static string S3() => "a_7!";
    private static string Secret() => S1() + S2() + S3();

    // Use constant identity strings to ensure key consistency across Editor, batch mode, and runtime builds.
    // Application.identifier/companyName/productName can return different values in different contexts.
    private const string AppId = "com.chemlabsim.app";
    private const string Company = "ChemLabSim";
    private const string Product = "ChemLabSim";

    public static byte[] DeriveKey32()
    {
        string ikmStr =
            AppId + "|" +
            Company + "|" +
            Product + "|" +
            Secret();

        byte[] ikm = CryptoUtil.Sha256Bytes(ikmStr);
        byte[] salt = CryptoUtil.Sha256Bytes("ChemLabSim|reaction-key-salt|v2");
        byte[] info = CryptoUtil.Sha256Bytes("reactions-db-v1");
        return CryptoUtil.HKDF(ikm, salt, info, 32);
    }

    public static byte[] Aad()
    {
        return CryptoUtil.Sha256Bytes(AppId + "|reactions-db-v1");
    }
}
