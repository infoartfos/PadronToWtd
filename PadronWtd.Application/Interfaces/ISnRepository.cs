namespace PadronWtd.Application.Interfaces;

public interface ISnRepository
{
    Task<(string CardCode, string CardName)?> FindByCuitAsync(string cuit);
}
