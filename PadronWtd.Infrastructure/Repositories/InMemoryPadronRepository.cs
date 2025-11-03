using PadronWtd.Application.Interfaces;
using PadronWtd.Domain.Entities;

namespace PadronWtd.Infrastructure.Repositories;

public class InMemoryPadronRepository : IPadronRepository
{
    private readonly List<PadronEntry> _entries = new();

    public Task BulkInsertAsync(IEnumerable<PadronEntry> entries, string user)
    {
        _entries.AddRange(entries);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<PadronEntry>> GetByRunAsync(int runId)
        => Task.FromResult(_entries.Where(e => e.RunId == runId).AsEnumerable());
}
