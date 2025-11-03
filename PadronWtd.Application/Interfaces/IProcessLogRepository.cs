using PadronWtd.Domain.Entities;

namespace PadronWtd.Application.Interfaces;

public interface IProcessLogRepository
{
    Task AddAsync(ProcessLog log);
}
