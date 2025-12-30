using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using System;
using System.Configuration;

namespace PadronWtd.UI.Services
{
    public sealed class SapConnectionManager
    {
        private static readonly Lazy<SapConnectionManager> _instance =
            new Lazy<SapConnectionManager>(() => new SapConnectionManager());

        private Company _serviceCompany; // Caché para el usuario de servicio
        private readonly object _lock = new object();
        private readonly ILogger _logger;

        public static SapConnectionManager Instance => _instance.Value;

        private SapConnectionManager() 
        {
            _logger = SimpleServiceProvider.Get<ILogger>();
        }

        /// <summary>
        /// Obtiene la compañía según la necesidad del servicio.
        /// </summary>
        /// <param name="useServiceAccount">Si es true, intenta conectar con el usuario del config. 
        /// Si es false, intenta usar el usuario logueado.</param>
        /// Si es true , intenta con el usuario del archivo de configuración
        public Company GetCompany(bool useServiceAccount = true)
        {
            useServiceAccount = ConfigurationManager.AppSettings["SAP.UseLoggedUser"].ToLower().Equals("true");
            if (useServiceAccount)
            {
                return GetServiceCompany();
            }

            // Caso: Usuario Logueado (Add-on)
            if (App.Company != null && App.Company.Connected)
            {
                return App.Company;
            }
            _logger.Error("No se detectó una sesión activa de SAP Business One (UI API).");
            throw new InvalidOperationException("No se detectó una sesión activa de SAP Business One (UI API).");
        }

        private Company GetServiceCompany()
        {
            lock (_lock)
            {
                if (_serviceCompany != null && _serviceCompany.Connected)
                {
                    return _serviceCompany;
                }

                if (_serviceCompany != null)
                {
                    try { _serviceCompany.Disconnect(); } catch { }
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(_serviceCompany);
                    _serviceCompany = null;
                }

                _serviceCompany = ConnectNewSession();
                return _serviceCompany;
            }
        }

        private Company ConnectNewSession()
        {
            Company oCompany = new Company();
            try
            {
                oCompany.DbServerType = (BoDataServerTypes)Enum.Parse(typeof(BoDataServerTypes), ConfigurationManager.AppSettings["SAP.DbServerType"]);
                oCompany.Server = ConfigurationManager.AppSettings["SAP.Server"];
                oCompany.LicenseServer = ConfigurationManager.AppSettings["SAP.LicenseServer"];
                oCompany.CompanyDB = ConfigurationManager.AppSettings["SAP.CompanyDB"];
                //oCompany.UserName = ConfigurationManager.AppSettings["SAP.UserName"];

                // Desencriptación de la clave
                string encryptedPass = ConfigurationManager.AppSettings["SAP.Password"];
                oCompany.Password = EncryptionHelper.Decrypt(encryptedPass);

                //oCompany.UserName = "GSCHNEIDER";
                //oCompany.Password = "TzLt3#MA";

                //oCompany.DbUserName = "USERINTDEV";
                //oCompany.DbPassword = "Argentina2025!";

                oCompany.UseTrusted = false;
                oCompany.language = BoSuppLangs.ln_Spanish_La;


                int result = oCompany.Connect();
                if (result != 0)
                {
                    string err;
                    int errCode;
                    oCompany.GetLastError(out errCode, out err);
                    string errString = $"Error DI API: {errCode} - {err}";
                    _logger.Error(errString);
                    string connectString = $"Conexion a: \n-Server: {oCompany.Server} " +
                                                 $"\n-DbServerType: {oCompany.DbServerType} " +
                                                 $"\n-CompanyDB: {oCompany.CompanyDB} " +
                                                 $"\n-UserName: {oCompany.UserName} " +
                                                 $"\n-DbUserName: {oCompany.DbUserName} " +
                                                 $"\n-LicenseServer: {oCompany.LicenseServer} ";
                    _logger.Info(connectString);
                    throw new Exception(errString);
                }

                return oCompany;
            }
            catch (Exception)
            {
                if (oCompany != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(oCompany);
                throw;
            }
        }

        public void DisconnectServiceCompany()
        {
            lock (_lock)
            {
                if (_serviceCompany != null && _serviceCompany.Connected)
                {
                    _serviceCompany.Disconnect();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(_serviceCompany);
                    _serviceCompany = null;
                }
            }
        }
    }
}