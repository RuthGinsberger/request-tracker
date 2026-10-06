using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestTracker.Domain;

namespace RequestTracker.Infrastructure.Persistence.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("Requests", t =>
        {
            t.HasCheckConstraint("CK_Requests_Status", "[Status] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_Requests_Priority", "[Priority] BETWEEN 1 AND 3");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd();

        builder.Property(r => r.Title).HasMaxLength(Request.TitleMaxLength).IsRequired();
        builder.Property(r => r.OrganizationName).HasMaxLength(Request.OrganizationNameMaxLength).IsRequired();
        builder.Property(r => r.Status).HasColumnType("tinyint").IsRequired();
        builder.Property(r => r.Priority).HasColumnType("tinyint").IsRequired();
        builder.Property(r => r.AssignedTo).HasMaxLength(Request.AssignedToMaxLength);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();
        builder.Property(r => r.RowVersion).IsRowVersion();

        // Index rationale and the measurements behind it: docs/PERFORMANCE.md (initial plan: docs/ARCHITECTURE.md section 4).
        // IX_Requests_CreatedAt covers the list projection, so every list ordered by createdAt (the default) is read from
        // this index in order with no key lookups, whatever filter is applied to the included columns.
        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_Requests_CreatedAt")
            .IncludeProperties(r => new { r.Title, r.OrganizationName, r.Status, r.Priority, r.AssignedTo, r.UpdatedAt, r.RowVersion });
        builder.HasIndex(r => new { r.Status, r.CreatedAt }).HasDatabaseName("IX_Requests_Status_CreatedAt");
        builder.HasIndex(r => new { r.OrganizationName, r.CreatedAt }).HasDatabaseName("IX_Requests_OrganizationName_CreatedAt");
        builder.HasIndex(r => new { r.AssignedTo, r.CreatedAt }).HasDatabaseName("IX_Requests_AssignedTo_CreatedAt");
        builder.HasIndex(r => r.Title).HasDatabaseName("IX_Requests_Title");
        builder.HasIndex(r => new { r.Status, r.Priority }).HasDatabaseName("IX_Requests_Status_Priority");
        // Single-column indexes that exist only to serve the sortable columns that no other index orders (the clustered key
        // Id is appended to each, which is the tie-breaker of every list sort): without them a sort scans and sorts all rows.
        builder.HasIndex(r => r.UpdatedAt).HasDatabaseName("IX_Requests_UpdatedAt");
        builder.HasIndex(r => r.Priority).HasDatabaseName("IX_Requests_Priority");
        builder.HasIndex(r => r.Status).HasDatabaseName("IX_Requests_Status");
    }
}
