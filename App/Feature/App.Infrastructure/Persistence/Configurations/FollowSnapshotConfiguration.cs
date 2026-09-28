using App.Domain.GitHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

internal sealed class FollowSnapshotConfiguration : IEntityTypeConfiguration<FollowSnapshot>
{
    public void Configure(EntityTypeBuilder<FollowSnapshot> builder)
    {
        builder.ToTable("follow_snapshots");

        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();

        builder.Property(snapshot => snapshot.Username)
            .HasMaxLength(FollowSnapshot.MaxUsernameLength)
            .IsRequired();

        // Serves the history query: filter by username, newest first.
        builder.HasIndex(snapshot => new { snapshot.Username, snapshot.CapturedAt })
            .IsDescending(false, true);
    }
}
