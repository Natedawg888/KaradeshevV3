using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

public static class EncryptionHelper
{
    // Replace before shipping.
    // Make this long, random, and unique to your game/project.
    private const string MasterSecret = "REPLACE_WITH_YOUR_OWN_LONG_RANDOM_SECRET_64_PLUS_CHARS";

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("KSG2");
    private const byte Version = 1;

    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int MacSize = 32;
    private const int KeySize = 32; // 256-bit AES key
    private const int Pbkdf2Iterations = 100000;

    public static byte[] EncryptToBytes(string plainText)
    {
        if (plainText == null)
            plainText = string.Empty;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] compressedBytes = Compress(plainBytes);

        byte[] salt = new byte[SaltSize];
        byte[] iv = new byte[IvSize];

        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
            rng.GetBytes(iv);
        }

        DeriveKeys(salt, out byte[] encKey, out byte[] macKey);

        byte[] cipherBytes;
        using (Aes aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = encKey;
            aes.IV = iv;

            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            {
                cipherBytes = encryptor.TransformFinalBlock(compressedBytes, 0, compressedBytes.Length);
            }
        }

        byte[] header = BuildHeader(salt, iv, cipherBytes.Length);
        byte[] body = Combine(header, cipherBytes);

        byte[] mac;
        using (HMACSHA256 hmac = new HMACSHA256(macKey))
        {
            mac = hmac.ComputeHash(body);
        }

        return Combine(body, mac);
    }

    public static string DecryptFromBytes(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length == 0)
            return string.Empty;

        using (MemoryStream ms = new MemoryStream(fileBytes))
        using (BinaryReader br = new BinaryReader(ms))
        {
            byte[] magic = br.ReadBytes(Magic.Length);
            if (!ByteArrayEquals(magic, Magic))
                throw new CryptographicException("Invalid save header.");

            byte version = br.ReadByte();
            if (version != Version)
                throw new CryptographicException($"Unsupported save version: {version}");

            byte[] salt = br.ReadBytes(SaltSize);
            if (salt.Length != SaltSize)
                throw new CryptographicException("Save salt missing or corrupt.");

            byte[] iv = br.ReadBytes(IvSize);
            if (iv.Length != IvSize)
                throw new CryptographicException("Save IV missing or corrupt.");

            int cipherLength = br.ReadInt32();
            if (cipherLength <= 0)
                throw new CryptographicException("Invalid cipher length in save.");

            long expectedMinimumRemaining = cipherLength + MacSize;
            if ((ms.Length - ms.Position) < expectedMinimumRemaining)
                throw new CryptographicException("Save data truncated or corrupt.");

            byte[] cipherBytes = br.ReadBytes(cipherLength);
            byte[] sentMac = br.ReadBytes(MacSize);

            DeriveKeys(salt, out byte[] encKey, out byte[] macKey);

            byte[] body = new byte[fileBytes.Length - MacSize];
            Buffer.BlockCopy(fileBytes, 0, body, 0, body.Length);

            byte[] computedMac;
            using (HMACSHA256 hmac = new HMACSHA256(macKey))
            {
                computedMac = hmac.ComputeHash(body);
            }

            if (!FixedTimeEquals(sentMac, computedMac))
                throw new CryptographicException("Save failed integrity check and may have been modified.");

            byte[] plainCompressed;
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encKey;
                aes.IV = iv;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    plainCompressed = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                }
            }

            byte[] plainBytes = Decompress(plainCompressed);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }

    public static bool LooksEncryptedBytes(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length < Magic.Length + 1)
            return false;

        for (int i = 0; i < Magic.Length; i++)
        {
            if (fileBytes[i] != Magic[i])
                return false;
        }

        return true;
    }

    private static byte[] BuildHeader(byte[] salt, byte[] iv, int cipherLength)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Magic);
            bw.Write(Version);
            bw.Write(salt);
            bw.Write(iv);
            bw.Write(cipherLength);
            bw.Flush();
            return ms.ToArray();
        }
    }

    private static void DeriveKeys(byte[] salt, out byte[] encKey, out byte[] macKey)
    {
        using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(
            MasterSecret,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256))
        {
            encKey = kdf.GetBytes(KeySize);
            macKey = kdf.GetBytes(KeySize);
        }
    }

    private static byte[] Compress(byte[] data)
    {
        using (MemoryStream output = new MemoryStream())
        {
            using (GZipStream gzip = new GZipStream(output, CompressionLevel.Optimal, true))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
    }

    private static byte[] Decompress(byte[] data)
    {
        using (MemoryStream input = new MemoryStream(data))
        using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
        using (MemoryStream output = new MemoryStream())
        {
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }

    private static byte[] Combine(params byte[][] arrays)
    {
        int totalLength = 0;
        for (int i = 0; i < arrays.Length; i++)
            totalLength += arrays[i].Length;

        byte[] result = new byte[totalLength];
        int offset = 0;

        for (int i = 0; i < arrays.Length; i++)
        {
            Buffer.BlockCopy(arrays[i], 0, result, offset, arrays[i].Length);
            offset += arrays[i].Length;
        }

        return result;
    }

    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];

        return diff == 0;
    }

    private static bool ByteArrayEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }
}