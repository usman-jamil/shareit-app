using Domain.Shares;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class FileConfiguration : IEntityTypeConfiguration<Domain.Files.File>
{
    public void Configure(EntityTypeBuilder<Domain.Files.File> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.CreatedAt).HasConversion(d => DateTime.SpecifyKind(d, DateTimeKind.Utc), v => v);

        builder.Property(t => t.UpdatedAt).HasConversion(d => DateTime.SpecifyKind(d, DateTimeKind.Utc), v => v);

        builder.HasOne<Share>()
            .WithMany()
            .HasForeignKey(t => t.ShareId);
    }
}
