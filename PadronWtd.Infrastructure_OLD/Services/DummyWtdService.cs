using PadronWtd.Application.Interfaces;

namespace PadronWtd.Infrastructure.Services;

public class DummyWtdService : IWtdService
{
    public Task InsertWtd3Async(string cuit, int wtCode)
    {
        Console.WriteLine($"[WTD3] Insert para CUIT {cuit} con WTCode {wtCode}");
        return Task.CompletedTask;
    }
}
