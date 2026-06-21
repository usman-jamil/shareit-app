using Domain.Shares;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class ShareConfiguration : IEntityTypeConfiguration<Share>
{
    public void Configure(EntityTypeBuilder<Share> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.CreatedAt).HasConversion(d => DateTime.SpecifyKind(d, DateTimeKind.Utc), v => v);

        builder.Property(t => t.UpdatedAt).HasConversion(d => DateTime.SpecifyKind(d, DateTimeKind.Utc), v => v);

        builder.Property(t => t.ExpiresAt).HasConversion(d => DateTime.SpecifyKind(d, DateTimeKind.Utc), v => v);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.OwnerUserId);

        builder.HasMany<Domain.Files.File>()
            .WithOne()
            .HasForeignKey(t => t.ShareId);
        
        builder.HasIndex(t => t.ExpiresAt)
            .HasDatabaseName("ix_shares_expires_at");
        
        builder.HasIndex(t => t.OwnerUserId)
            .HasDatabaseName("ix_shares_owner_user_id");
    }
}
