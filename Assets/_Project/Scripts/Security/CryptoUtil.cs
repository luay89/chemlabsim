/*
 * Purpose: Provides deterministic HKDF derivations and AES-256-CBC + HMAC-SHA256 helpers for protecting reaction data.
 * Path: Assets/_Project/Scripts/Security/CryptoUtil.cs
 * How to use: Pass master keys from KeyMaterial into EncryptAesCbcHmac/DecryptAesCbcHmac before loading reactions at runtime.
 */
using System;
using System.Security.Cryptography;
using System.Text;

public static class CryptoUtil
{
    // HKDF-SHA256
    public static byte[] HKDF(byte[] ikm, byte[] salt, byte[] info, int length)
    {
        using var hmac = new HMACSHA256(salt);
        var prk = hmac.ComputeHash(ikm);

        var okm = new byte[length];
        byte[] t = Array.Empty<byte>();
        int offset = 0;
        byte counter = 1;

        using var hmac2 = new HMACSHA256(prk);
        while (offset < length)
        {
            var input = new byte[t.Length + info.Length + 1];
            Buffer.BlockCopy(t, 0, input, 0, t.Length);
            Buffer.BlockCopy(info, 0, input, t.Length, info.Length);
            input[^1] = counter;

            t = hmac2.ComputeHash(input);
            int toCopy = Math.Min(t.Length, length - offset);
            Buffer.BlockCopy(t, 0, okm, offset, toCopy);

            offset += toCopy;
            counter++;
        }
        return okm;
    }

    public static byte[] Sha256Bytes(string s)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(s));
    }

    // AES-256-CBC + HMAC-SHA256 (Encrypt-then-MAC)
    // File format: [iv(16)][cipher(n)][hmac(32)]
    public static byte[] EncryptAesCbcHmac(byte[] masterKey32, byte[] plaintext, byte[] aad)
    {
        // split keys via HKDF
        byte[] encKey = HKDF(masterKey32, Sha256Bytes("enc_salt"), Sha256Bytes("enc_info"), 32);
        byte[] macKey = HKDF(masterKey32, Sha256Bytes("mac_salt"), Sha256Bytes("mac_info"), 32);

        byte[] iv = new byte[16];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(iv);

        byte[] cipher;
        using (var aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = encKey;
            aes.IV = iv;

            using var enc = aes.CreateEncryptor();
            cipher = enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }

        // HMAC over (aad || iv || cipher)
        byte[] mac;
        using (var hmac = new HMACSHA256(macKey))
        {
            hmac.TransformBlock(aad, 0, aad.Length, null, 0);
            hmac.TransformBlock(iv, 0, iv.Length, null, 0);
            hmac.TransformFinalBlock(cipher, 0, cipher.Length);
            mac = hmac.Hash!;
        }

        var output = new byte[16 + cipher.Length + 32];
        Buffer.BlockCopy(iv, 0, output, 0, 16);
        Buffer.BlockCopy(cipher, 0, output, 16, cipher.Length);
        Buffer.BlockCopy(mac, 0, output, 16 + cipher.Length, 32);
        return output;
    }

    public static byte[] DecryptAesCbcHmac(byte[] masterKey32, byte[] blob, byte[] aad)
    {
        if (blob == null || blob.Length < 16 + 32 + 1)
            throw new CryptographicException("Invalid blob");

        byte[] encKey = HKDF(masterKey32, Sha256Bytes("enc_salt"), Sha256Bytes("enc_info"), 32);
        byte[] macKey = HKDF(masterKey32, Sha256Bytes("mac_salt"), Sha256Bytes("mac_info"), 32);

        int cipherLen = blob.Length - 16 - 32;
        byte[] iv = new byte[16];
        byte[] cipher = new byte[cipherLen];
        byte[] mac = new byte[32];

        Buffer.BlockCopy(blob, 0, iv, 0, 16);
        Buffer.BlockCopy(blob, 16, cipher, 0, cipherLen);
        Buffer.BlockCopy(blob, 16 + cipherLen, mac, 0, 32);

        // verify HMAC
        byte[] mac2;
        using (var hmac = new HMACSHA256(macKey))
        {
            hmac.TransformBlock(aad, 0, aad.Length, null, 0);
            hmac.TransformBlock(iv, 0, iv.Length, null, 0);
            hmac.TransformFinalBlock(cipher, 0, cipher.Length);
            mac2 = hmac.Hash!;
        }

        if (!FixedTimeEquals(mac, mac2))
            throw new CryptographicException("HMAC check failed (tampered or wrong key)");

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = encKey;
        aes.IV = iv;

        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}