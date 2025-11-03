using PadronWtd.Application.Interfaces;
using PadronWtd.Domain.Entities;

namespace PadronWtd.Infrastructure.Repositories;

public class InMemoryTaxRepository : ITaxRepository
{
    public Task<IEnumerable<Tax>> GetAllAsync() =>
        Task.FromResult<IEnumerable<Tax>>(new[]
        {
            new Tax { Id = 1, Code = "RQ35", Detail = "Retención IIBB", WtCode = 35 },
            new Tax { Id = 2, Code = "RQ37", Detail = "Percepción IVA", WtCode = 37 }
        });
}
