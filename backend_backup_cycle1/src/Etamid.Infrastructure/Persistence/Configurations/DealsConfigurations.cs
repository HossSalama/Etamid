using Etamid.Domain.Admin;
using Etamid.Domain.Config;
using Etamid.Domain.Deals;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Etamid.Infrastructure.Persistence.Configurations;

public sealed class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> b)
    {
        b.Property(x => x.Number).HasDefaultValueSql("NEXT VALUE FOR DealNumbers");
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(200);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.ShippingAddressSnapshot).HasMaxLength(400);
        b.Property(x => x.PaymentMethodCode).HasMaxLength(30);
        b.Property(x => x.RejectNote).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => new { x.BuyerId, x.Status });
        b.HasIndex(x => new { x.SellerId, x.Status });
        b.HasIndex(x => new { x.Status, x.ExpiresAt });

        b.ToTable(t => t.HasCheckConstraint("CK_Deals_DifferentParties", "[BuyerId] <> [SellerId]"));

        b.ForeignKey<Deal, User>(x => x.BuyerId);
        b.ForeignKey<Deal, User>(x => x.SellerId);
        b.ForeignKey<Deal, ShippingCompany>(x => x.ShippingCompanyId);
        b.ForeignKey<Deal, Address>(x => x.ShippingAddressId);
        b.ForeignKey<Deal, StoredFile>(x => x.ImageFileId);
        b.ForeignKey<Deal, Lookup>(x => x.RejectReasonId);
    }
}

public sealed class DealEditRequestConfiguration : IEntityTypeConfiguration<DealEditRequest>
{
    public void Configure(EntityTypeBuilder<DealEditRequest> b)
    {
        b.Property(x => x.PrevDurationUnit).HasMaxLength(10);
        b.Property(x => x.PrevDescription).HasMaxLength(200);
        b.Property(x => x.NewDurationUnit).HasMaxLength(10);
        b.Property(x => x.NewDescription).HasMaxLength(200);

        // Only one pending edit per deal.
        b.HasIndex(x => x.DealId).IsUnique().HasFilter("[Status] = 'Pending'");

        b.ForeignKey<DealEditRequest, Deal>(x => x.DealId);
        b.ForeignKey<DealEditRequest, User>(x => x.ProposedById);
        b.ForeignKey<DealEditRequest, User>(x => x.RespondedById);
    }
}

public sealed class DealStatusHistoryConfiguration : IEntityTypeConfiguration<DealStatusHistory>
{
    public void Configure(EntityTypeBuilder<DealStatusHistory> b)
    {
        b.Property(x => x.FromStatus).HasMaxLength(30);
        b.Property(x => x.ToStatus).HasMaxLength(30);
        b.Property(x => x.Note).HasMaxLength(300);
        b.HasIndex(x => new { x.DealId, x.CreatedAt });
        b.ForeignKey<DealStatusHistory, Deal>(x => x.DealId);
        b.ForeignKey<DealStatusHistory, User>(x => x.ActorUserId);
    }
}

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.Property(x => x.TrackingNumber).HasMaxLength(60);
        b.Property(x => x.ProviderPayload).HasColumnType("nvarchar(max)");
        b.HasIndex(x => x.DealId).IsUnique();
        b.ForeignKey<Shipment, Deal>(x => x.DealId);
        b.ForeignKey<Shipment, ShippingCompany>(x => x.ShippingCompanyId);
        b.ForeignKey<Shipment, StoredFile>(x => x.WaybillFileId);
    }
}

public sealed class ShipmentEventConfiguration : IEntityTypeConfiguration<ShipmentEvent>
{
    public void Configure(EntityTypeBuilder<ShipmentEvent> b)
    {
        b.Property(x => x.Code).HasMaxLength(40);
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Location).HasMaxLength(100);
        b.HasIndex(x => new { x.ShipmentId, x.EventAt, x.Code }).IsUnique();
        b.ForeignKey<ShipmentEvent, Shipment>(x => x.ShipmentId);
    }
}

public sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> b)
    {
        b.Property(x => x.Comment).HasMaxLength(300);
        b.HasIndex(x => x.DealId).IsUnique();
        b.HasIndex(x => new { x.SellerId, x.CreatedAt });
        b.ToTable(t => t.HasCheckConstraint("CK_Ratings_Stars", "[Stars] BETWEEN 1 AND 5"));
        b.ForeignKey<Rating, Deal>(x => x.DealId);
        b.ForeignKey<Rating, User>(x => x.RaterId);
        b.ForeignKey<Rating, User>(x => x.SellerId);
    }
}
