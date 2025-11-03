using PadronWtd.Application.Interfaces;
using PadronWtd.Domain.Entities;

namespace PadronWtd.Infrastructure.Repositories;

public class InMemoryProcessLogRepository : IProcessLogRepository
{
    public readonly List<ProcessLog> Logs = new();
    public Task AddAsync(ProcessLog log)
    {
        Logs.Add(log);
        return Task.CompletedTask;
    }
}
