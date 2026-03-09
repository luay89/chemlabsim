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

    public static byte[] DeriveKey32()
    {
        string ikmStr =
            Application.identifier + "|" +
            Application.companyName + "|" +
            Application.productName + "|" +
            Secret();

        byte[] ikm = CryptoUtil.Sha256Bytes(ikmStr);
        byte[] salt = CryptoUtil.Sha256Bytes("ChemLabSim|reaction-key-salt|v2");
        byte[] info = CryptoUtil.Sha256Bytes("reactions-db-v1");
        return CryptoUtil.HKDF(ikm, salt, info, 32);
    }

    public static byte[] Aad()
    {
        return CryptoUtil.Sha256Bytes(Application.identifier + "|reactions-db-v1");
    }
}
