using PadronWtd.Domain.Entities;

namespace PadronWtd.Application.Interfaces;

public interface IPadronRepository
{
    Task BulkInsertAsync(IEnumerable<PadronEntry> entries, string user);
    Task<IEnumerable<PadronEntry>> GetByRunAsync(int runId);
}
