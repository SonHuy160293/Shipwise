using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;

namespace Shipwise.Identity.Infrastructure.Persistence.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions", IdentityDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_Sessions_RevocationReason", """ "RevocationReason" IS NULL OR "RevocationReason" IN (1, 2, 3) """);
            table.HasCheckConstraint("CK_Sessions_Revocation", """ ("RevokedAt" IS NULL) = ("RevocationReason" IS NULL) """);
        });
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.RefreshTokenHash).HasMaxLength(64).IsRequired();
        builder.Property(session => session.PreviousRefreshTokenHash).HasMaxLength(64);
        builder.Property(session => session.RevocationReason).HasConversion<short>();
        builder.Property(session => session.Version).IsRowVersion();
        builder.Property(session => session.CreatedBy).HasMaxLength(64);
        builder.Property(session => session.UpdatedBy).HasMaxLength(64);
        builder.HasIndex(session => session.UserId).HasDatabaseName("IX_Sessions_UserId");
        builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
