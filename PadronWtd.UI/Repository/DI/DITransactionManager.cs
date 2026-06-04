using SAPbobsCOM;

namespace PadronWtd.Repository.DI
{
    public class DITransactionManager : ITransactionManager
    {
        private readonly Company _company;

        public DITransactionManager(Company company)
        {
            _company = company;
        }

        public void StartTransaction() => _company.StartTransaction();

        public void EndTransaction(BoWfTransOpt option) => _company.EndTransaction(option);

        public bool InTransaction => _company.InTransaction;
    }
}
