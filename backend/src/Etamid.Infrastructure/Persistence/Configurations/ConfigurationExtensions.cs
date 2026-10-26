using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Etamid.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>
    /// Adds a foreign key without a navigation property. Restrict on delete everywhere:
    /// money, deals and audit rows must never disappear because a parent was removed.
    /// </summary>
    public static void ForeignKey<TDependent, TPrincipal>(
        this EntityTypeBuilder<TDependent> builder,
        Expression<Func<TDependent, object?>> foreignKey,
        DeleteBehavior onDelete = DeleteBehavior.Restrict)
        where TDependent : class
        where TPrincipal : class
        => builder.HasOne<TPrincipal>().WithMany().HasForeignKey(foreignKey).OnDelete(onDelete);

    public static void Text<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, string?>> property) where T : class
        => builder.Property(property).HasColumnType("nvarchar(max)");
}
