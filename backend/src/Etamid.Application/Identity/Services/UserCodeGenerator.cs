using System.Security.Cryptography;
using Etamid.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Services;

/// <summary>Short public code that buyers use to find a seller (no look-alike characters).</summary>
public sealed class UserCodeGenerator(IApplicationDbContext db)
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Length = 8;

    public async Task<string> GenerateAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[Length];
            for (var i = 0; i < Length; i++)
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

            var code = new string(chars);
            if (!await db.Users.AnyAsync(u => u.Code == code, ct))
                return code;
        }

        throw new InvalidOperationException("Could not generate a unique user code.");
    }
}
