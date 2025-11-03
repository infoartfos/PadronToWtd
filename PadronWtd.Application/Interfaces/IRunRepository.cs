using PadronWtd.Domain.Entities;

namespace PadronWtd.Application.Interfaces;

public interface IRunRepository
{
    Task<Run?> GetActiveAsync();
}
