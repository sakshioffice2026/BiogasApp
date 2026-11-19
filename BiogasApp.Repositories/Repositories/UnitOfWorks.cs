using BiogasApp.Database;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore.Storage;

namespace BiogasApp.Repositories.Repositories;

/// <summary>
/// Scoped unit of work over the request's ApplicationDbContext.
/// The DbContext itself is owned (and disposed) by the DI container, not by this class.
/// </summary>
public class UnitOfWorks : IUnitOfWork, IAsyncDisposable
{
    private readonly ApplicationDbContext _db;
    private IDbContextTransaction? _transaction;

    public UnitOfWorks(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already in progress.");

        _transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>Saves pending changes, then commits. Rolls back and rethrows if either step fails.</summary>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No transaction is in progress. Call BeginTransactionAsync first.");

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackInternalAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return; // nothing to roll back

        try
        {
            await RollbackInternalAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // A transaction left open at the end of the request is rolled back by disposing it.
        await DisposeTransactionAsync();
        GC.SuppressFinalize(this);
    }

    private async Task RollbackInternalAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return;
        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            // Connection may already be broken; the original exception matters more.
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction is null) return;
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}