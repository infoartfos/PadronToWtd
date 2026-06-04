using System.Runtime.InteropServices;
using SAPbobsCOM;

namespace PadronWtd.Repository.DI
{
    public class SapProviderChecker : IProviderChecker
    {
        private readonly Company _company;

        public SapProviderChecker(Company company)
        {
            _company = company;
        }

        public bool CuitExists(string cuit)
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                string query = $@"
                    SELECT COUNT(*) 
                    FROM ""OCRD"" 
                    WHERE ""LicTradNum"" = '{cuit}'
                    AND UPPER(""CardCode"") LIKE 'PL%'";

                rs.DoQuery(query);

                if (!rs.EoF)
                    return int.Parse(rs.Fields.Item(0).Value.ToString()) > 0;

                return false;
            }
            catch { return false; }
            finally { if (rs != null) Marshal.ReleaseComObject(rs); }
        }
    }
}
