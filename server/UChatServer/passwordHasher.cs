namespace UChatServer;

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

public class PasswordService
{
    private const int MemorySize = 65536; 
    private const int Iterations = 4;
    private const int Parallelism = 4;
    private const int HashLength = 32;
    public class HashResult
    {
        public string HashBase64 { get; set; }
        public string SaltBase64 { get; set; }
    }

    // Call this when a user Creates an Account
    public HashResult HashPassword(string password)
    {
        var salt = CreateSalt();
        var hash = HashPasswordWithSalt(password, salt);
        return new HashResult
        {
            HashBase64 = Convert.ToBase64String(hash),
            SaltBase64 = Convert.ToBase64String(salt)
        };
    }
    public bool VerifyPassword(string password, string storedHashBase64, string storedSaltBase64)
    {
        var storedHash = Convert.FromBase64String(storedHashBase64);
        var storedSalt = Convert.FromBase64String(storedSaltBase64);
        var newHash = HashPasswordWithSalt(password, storedSalt);
        return CryptographicOperations.FixedTimeEquals(newHash, storedHash);
    }

    private byte[] HashPasswordWithSalt(string password, byte[] salt)
    {
        using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
        {
            argon2.Salt = salt;
            argon2.DegreeOfParallelism = Parallelism;
            argon2.MemorySize = MemorySize;
            argon2.Iterations = Iterations;

            return argon2.GetBytes(HashLength);
        }
    }

    private byte[] CreateSalt()
    {
        var buffer = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }
        return buffer;
    }
}