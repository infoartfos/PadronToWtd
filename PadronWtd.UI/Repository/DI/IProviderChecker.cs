namespace PadronWtd.Repository.DI
{
    public interface IProviderChecker
    {
        bool CuitExists(string cuit);
    }
}
