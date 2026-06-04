using SAPbobsCOM;

namespace PadronWtd.Repository.DI
{
    public interface ITransactionManager
    {
        void StartTransaction();
        void EndTransaction(BoWfTransOpt option);
        bool InTransaction { get; }
    }
}
