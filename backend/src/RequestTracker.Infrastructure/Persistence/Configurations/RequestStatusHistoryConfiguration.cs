using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestTracker.Domain;

namespace RequestTracker.Infrastructure.Persistence.Configurations;

public class RequestStatusHistoryConfiguration : IEntityTypeConfiguration<RequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<RequestStatusHistory> builder)
    {
        builder.ToTable("RequestStatusHistory", t =>
        {
            t.HasCheckConstraint("CK_RequestStatusHistory_PreviousStatus", "[PreviousStatus] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_RequestStatusHistory_NewStatus", "[NewStatus] BETWEEN 1 AND 4");
        });

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedOnAdd();

        builder.Property(h => h.PreviousStatus).HasColumnType("tinyint").IsRequired();
        builder.Property(h => h.NewStatus).HasColumnType("tinyint").IsRequired();
        builder.Property(h => h.ChangedAt).IsRequired();
        builder.Property(h => h.ChangedBy).HasMaxLength(RequestStatusHistory.ChangedByMaxLength).IsRequired();

        // NO ACTION: nothing is ever deleted, so history must never cascade.
        builder.HasOne<Request>()
            .WithMany()
            .HasForeignKey(h => h.RequestId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(h => new { h.RequestId, h.ChangedAt }).HasDatabaseName("IX_RequestStatusHistory_RequestId_ChangedAt");
    }
}
