using PadronWtd.Domain.Entities;

namespace PadronWtd.Application.Interfaces;

public interface ITaxRepository
{
    Task<IEnumerable<Tax>> GetAllAsync();
}
