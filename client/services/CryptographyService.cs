using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace client.Services // <--- ТУТ ТЕПЕР Services, А НЕ connector
{
    public static class CryptographyService
    {
        // 1. GENERATE SESSION KEY (AES)
        public static byte[] GenerateSessionKey()
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                return aes.Key;
            }
        }

        // 2. ENCRYPT MESSAGE (AES)
        public static (string CipherText, string IV) EncryptMessage(string plainText, byte[] sessionKey)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = sessionKey;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                byte[] encrypted;
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    encrypted = ms.ToArray();
                }

                return (Convert.ToBase64String(encrypted), Convert.ToBase64String(aes.IV));
            }
        }

        // 3. DECRYPT MESSAGE (AES)
        public static string DecryptMessage(string cipherText, string iv, byte[] sessionKey)
        {
            byte[] buffer = Convert.FromBase64String(cipherText);
            byte[] ivBytes = Convert.FromBase64String(iv);

            using (Aes aes = Aes.Create())
            {
                aes.Key = sessionKey;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (var ms = new MemoryStream(buffer))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        // 4. ENCRYPT SESSION KEY (RSA) - Using Public Key
        public static string EncryptSessionKey(byte[] sessionKey, string publicKeyXml)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.FromXmlString(publicKeyXml);
                byte[] encryptedKey = rsa.Encrypt(sessionKey, RSAEncryptionPadding.OaepSHA256);
                return Convert.ToBase64String(encryptedKey);
            }
        }

        // 5. DECRYPT SESSION KEY (RSA) - Using Private Key
        public static byte[] DecryptSessionKey(string encryptedSessionKey, string privateKeyXml)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.FromXmlString(privateKeyXml);
                byte[] keyBytes = Convert.FromBase64String(encryptedSessionKey);
                return rsa.Decrypt(keyBytes, RSAEncryptionPadding.OaepSHA256);
            }
        }
    }
}
