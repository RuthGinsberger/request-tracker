using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RequestTracker.Domain;

namespace RequestTracker.Application.Abstractions;

/// <summary>The application's view of the database; implemented by the Infrastructure DbContext.</summary>
public interface IAppDbContext
{
    DbSet<Request> Requests { get; }

    DbSet<RequestStatusHistory> StatusHistory { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
