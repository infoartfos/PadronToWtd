using SAPbobsCOM;
using System;

namespace PadronWtd.UI.Services
{
    public class SapPadronService : IDisposable
    {
        private Company _company;

        public SapPadronService(Company company)
        {
            _company = company;
        }

        public int Insert(PadronWtd.UI.Models.P_SaltaCsvRow row, out string error)
        {
            error = string.Empty;

            try
            {
                UserTable table = (UserTable)_company.UserTables.Item("P_Salta");

                table.Name = Guid.NewGuid().ToString("N").Substring(0, 10);
                table.UserFields.Fields.Item("U_Anio").Value = row.Anio;
                table.UserFields.Fields.Item("U_Padron").Value = row.Padron;
                table.UserFields.Fields.Item("U_Cuit").Value = row.Cuit;
                table.UserFields.Fields.Item("U_Inscripcion").Value = row.Inscripcion;
                table.UserFields.Fields.Item("U_Riesgo").Value = row.Riesgo;
                table.UserFields.Fields.Item("U_Notas").Value = row.Notas;

                int ret = table.Add();
                if (ret != 0)
                {
                    error = _company.GetLastErrorDescription();
                    return -1;
                }

                return 1;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return -1;
            }
        }

        public void Dispose()
        {
            if (_company != null)
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(_company);
                _company = null;
            }
        }
    }
}
