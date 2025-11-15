using Microsoft.EntityFrameworkCore;
using PadronWtd.Application.Interfaces;
using PadronWtd.Domain.Entities;
using PadronWtd.Infrastructure.Persistence;

namespace PadronWtd.Infrastructure.Repositories.SqlServer;

public class SqlPadronRepository : IPadronRepository
{
    private readonly PadronDbContext _context;

    public SqlPadronRepository(PadronDbContext context)
    {
        _context = context;
    }

    public async Task BulkInsertAsync(IEnumerable<PadronEntry> entries, string user)
    {
        // Insert masivo EF optimizado
        await _context.PadronEntries.AddRangeAsync(entries);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<PadronEntry>> GetByRunAsync(int runId)
    {
        return await _context.PadronEntries
            .Where(e => e.RunId == runId)
            .ToListAsync();
    }
}
