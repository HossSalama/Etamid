using Etamid.Domain.Config;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Etamid.Domain.Money;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Common.Interfaces;

/// <summary>
/// Persistence abstraction used by handlers. The sets most use cases need are exposed directly;
/// any other table is reachable through <see cref="Set{TEntity}"/>.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<CompanyProfile> CompanyProfiles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Device> Devices { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<Bank> Banks { get; }
    DbSet<TermsVersion> TermsVersions { get; }
    DbSet<TermsAcceptance> TermsAcceptances { get; }
    DbSet<AuthAuditLog> AuthAuditLogs { get; }
    DbSet<StoredFile> Files { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<AppSetting> AppSettings { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
