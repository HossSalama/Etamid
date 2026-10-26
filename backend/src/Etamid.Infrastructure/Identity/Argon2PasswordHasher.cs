using System.Security.Cryptography;
using System.Text;
using Etamid.Application.Common.Interfaces;
using Konscious.Security.Cryptography;

namespace Etamid.Infrastructure.Identity;

/// <summary>Argon2id with the OWASP minimum parameters (19 MiB, 2 passes, 1 lane).</summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemoryKb = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;

    private static readonly Lazy<string> DummyHash = new(() => new Argon2PasswordHasher().Hash("dummy-password-for-timing"));

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Compute(password, salt, MemoryKb, Iterations, Parallelism, HashSize);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string hash, string password)
    {
        try
        {
            var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 || parts[0] != "argon2id") return false;

            var settings = parts[2].Split(',')
                .Select(p => p.Split('='))
                .ToDictionary(p => p[0], p => int.Parse(p[1]));
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);

            var actual = Compute(password, salt, settings["m"], settings["t"], settings["p"], expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (Exception ex) when (ex is FormatException or KeyNotFoundException or IndexOutOfRangeException or OverflowException)
        {
            return false;
        }
    }

    public void VerifyDummy(string password) => Verify(DummyHash.Value, password);

    private static byte[] Compute(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int size)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };
        return argon.GetBytes(size);
    }
}
