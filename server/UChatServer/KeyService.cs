namespace UChatServer;
using System.Security.Cryptography;

public class KeyService
{
    // Generates a new pair of keys (Public and Private)
    // Returns them as Base64 strings
    public (string PublicKey, string PrivateKey) GenerateKeys(int keySize = 2048)
    {
        using (var rsa = RSA.Create(keySize))
        {
            // 1. Export Public Key (Share this with everyone)
            // 'SubjectPublicKeyInfo' is the standard X.509 format for public keys
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            string publicKey = Convert.ToBase64String(publicKeyBytes);

            // 2. Export Private Key (KEEP THIS SECRET!)
            // 'Pkcs8' is the standard format for private keys
            var privateKeyBytes = rsa.ExportPkcs8PrivateKey();
            string privateKey = Convert.ToBase64String(privateKeyBytes);

            return (publicKey, privateKey);
        }
    }
}
