namespace PadronWtd.Application.Interfaces;

public interface IWtdService
{
    Task InsertWtd3Async(string cuit, int wtCode);
}
