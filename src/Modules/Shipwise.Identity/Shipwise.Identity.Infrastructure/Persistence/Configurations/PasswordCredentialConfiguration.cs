using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;

namespace Shipwise.Identity.Infrastructure.Persistence.Configurations;

public sealed class PasswordCredentialConfiguration : IEntityTypeConfiguration<PasswordCredential>
{
    public void Configure(EntityTypeBuilder<PasswordCredential> builder)
    {
        builder.ToTable("PasswordCredentials", IdentityDbContext.Schema);
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id).ValueGeneratedNever();
        builder.Property(credential => credential.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(credential => credential.CreatedBy).HasMaxLength(64);
        builder.Property(credential => credential.UpdatedBy).HasMaxLength(64);
        builder.HasIndex(credential => credential.UserId).IsUnique().HasDatabaseName("UX_PasswordCredentials_UserId");
        builder.HasOne<User>().WithOne().HasForeignKey<PasswordCredential>(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
