using PadronWtd.Application.Interfaces;
using PadronWtd.Domain.Entities;

namespace PadronWtd.Infrastructure.Repositories;

public class InMemoryRunRepository : IRunRepository
{
    private readonly Run _active = new() { Id = 1, Name = "Ejecución Demo", Active = true, DateFrom = DateTime.Now.AddDays(-30), DateTo = DateTime.Now };
    public Task<Run?> GetActiveAsync() => Task.FromResult<Run?>(_active);
}
