using SAPbobsCOM;

namespace PadronWtd.UI.Services
{
    public static class SapConnectionFactory
    {
        public static Company CreateCompany()
        {
            Company oCompany = new Company
            {
                DbServerType = BoDataServerTypes.dst_HANADB,
                //Server = "contreras-hanadb",
                Server = "10.250.2.11", //   "10.250.2.150",

                //Server = "contreras-hanadb.sbo.contreras.com.ar",
                CompanyDB = "SBP_SIOC_CHAR",
                UserName = "gschneider",
                Password = "TzLt3#MA",
                UseTrusted = false,
                language = BoSuppLangs.ln_Spanish,
                LicenseServer = "hanab1:40000"

            }; 

            int ret = oCompany.Connect();
            if (ret != 0)
            {
                string err = oCompany.GetLastErrorDescription();
                throw new System.Exception("Error al conectar a SAP: " + err);
            }

            return oCompany;
        }
    }
}
