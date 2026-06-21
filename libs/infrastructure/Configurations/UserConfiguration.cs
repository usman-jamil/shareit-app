using Domain.ApiKeys;
using Domain.Shares;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(t => t.Id);
        
        builder.Property(t => t.CreatedAt).HasConversion(d => DateTime.SpecifyKind(d, DateTimeKind.Utc), v => v);
        
        builder.HasMany<ApiKey>()
            .WithOne()
            .HasForeignKey(t => t.UserId);
        
        builder.HasMany<Share>()
            .WithOne()
            .HasForeignKey(t => t.OwnerUserId);
    }
}
