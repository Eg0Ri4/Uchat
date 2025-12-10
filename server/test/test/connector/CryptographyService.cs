using System.Security.Cryptography;
using System.Text;

namespace connector; // Ensure this matches the namespace in Program.cs

public class CryptographyService
{

    public static (string PublicKey, string PrivateKey) GenerateKeys(int size = 2048)
    {
        using (var rsa = RSA.Create(size))
        {
            return (
                Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()),
                Convert.ToBase64String(rsa.ExportPkcs8PrivateKey())
            );
        }
    }

    // Encrypts the AES Session Key so only the recipient can read it
    public static string EncryptSessionKey(byte[] sessionKey, string publicKeyBase64)
    {
        using (var rsa = RSA.Create())
        {
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            byte[] encryptedBytes = rsa.Encrypt(sessionKey, RSAEncryptionPadding.OaepSHA256);
            return Convert.ToBase64String(encryptedBytes);
        }
    }

    // Decrypts the AES Session Key using your Private Key
    public static byte[] DecryptSessionKey(string encryptedKeyBase64, string privateKeyBase64)
    {
        using (var rsa = RSA.Create())
        {
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
            return rsa.Decrypt(Convert.FromBase64String(encryptedKeyBase64), RSAEncryptionPadding.OaepSHA256);
        }
    }
    
    public static byte[] GenerateSessionKey()
    {
        using (var aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.GenerateKey();
            return aes.Key;
        }
    }

    public static (string CipherText, string IV) EncryptMessage(string plainText, byte[] sessionKey)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = sessionKey;
            aes.GenerateIV();
            
            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                return (Convert.ToBase64String(encryptedBytes), Convert.ToBase64String(aes.IV));
            }
        }
    }
    
    public static string DecryptMessage(string cipherText, string iv, byte[] sessionKey)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = sessionKey;
            aes.IV = Convert.FromBase64String(iv);
            
            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
        }
    }
}