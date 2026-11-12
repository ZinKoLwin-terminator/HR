using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apex.Hrms.Infrastructure.Persistence;

internal sealed class EfImportBatchStore(HrmsDbContext context) : IImportBatchStore
{
    public void Add(ImportBatch batch) => context.Add(batch);

    public Task<ImportBatch?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<ImportBatch>().Include(batch => batch.Issues).SingleOrDefaultAsync(batch => batch.Id == id, cancellationToken);

    public async Task<PagedResult<ImportBatch>> QueryAsync(int pageIndex, int pageSize, int? uploadedByUserId, CancellationToken cancellationToken)
    {
        var all = context.Set<ImportBatch>().AsNoTracking().Where(batch => uploadedByUserId == null || batch.UploadedByUserId == uploadedByUserId);
        var total = await all.CountAsync(cancellationToken);
        var index = Paging.ClampPageIndex(pageIndex, total, pageSize);
        var items = await all.OrderByDescending(batch => batch.UploadedAtUtc).ThenByDescending(batch => batch.Id)
            .Skip(index * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<ImportBatch>(items, total, index, pageSize);
    }
}

internal sealed class EfBatchTransactionFactory(HrmsDbContext context) : IBatchTransactionFactory
{
    public async Task<IBatchTransaction> BeginAsync(CancellationToken cancellationToken) =>
        new EfBatchTransaction(context, await context.Database.BeginTransactionAsync(cancellationToken));

    private sealed class EfBatchTransaction(HrmsDbContext context, IDbContextTransaction transaction) : IBatchTransaction
    {
        public Task SavepointAsync(string name, CancellationToken cancellationToken) =>
            transaction.CreateSavepointAsync(name, cancellationToken);

        public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken) =>
            transaction.RollbackToSavepointAsync(name, cancellationToken);

        public void ClearTracked() => context.ChangeTracker.Clear();

        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken) => transaction.RollbackAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            context.ChangeTracker.Clear();
        }
    }
}
