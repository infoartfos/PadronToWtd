using PadronWtd.Application.Interfaces;

namespace PadronWtd.Infrastructure.Repositories.InMemory;

public class DummySnRepository : ISnRepository
{
    private readonly Dictionary<string, (string, string)> _mockSn = new()
    {
        ["20000156982"] = ("C0001", "Proveedor A"),
        ["20000957748"] = ("C0002", "Proveedor B")
    };

    public Task<(string CardCode, string CardName)?> FindByCuitAsync(string cuit)
    {
        if (_mockSn.TryGetValue(cuit, out var sn))
            return Task.FromResult<(string, string)?>(sn);
        return Task.FromResult<(string, string)?>(null);
    }
}
