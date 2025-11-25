using PadronWtd.ServiceLayer;
using PadronWtd.UI.DI;
using PadronWtd.UI.Forms;   // donde está tu FrmImportarService
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
using PadronWtd.UI.SL;      // donde está tu ServiceLayerClient
using SAPbobsCOM;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PadronWtd.DebugRunner
{
    public class LeerPadronRunner
    {
        private readonly ILogger _logger;
        public LeerPadronRunner()
        {
            _logger = SimpleServiceProvider.Get<ILogger>();

        }

        public void Run()
        {
            Company oCompany = null;

            try
            {
                // ---------------------------------------------------------
                // 1. Configuración de la Conexión
                // ---------------------------------------------------------
                oCompany = new Company();

                oCompany.DbServerType = BoDataServerTypes.dst_HANADB;
                oCompany.Server = "hanadb1:30013";  // Si cambio esto no hay cambios,   con xxxx tambien da  ERROR DE CONEXIÓN (-132): Error during SBO user authentication

                oCompany.LicenseServer = "hanab1:40000";
                // oCompany.SLDServer = "hanab1:40000"; // Si descomento da error ERROR DE CONEXIÓN(100000060): B1 License Error Unknown error #100000060


                // Base de Datos y Credenciales
                oCompany.CompanyDB = "SBP_SIOC_CHAR"; // ERROR DE CONEXIÓN (-132): Error during SBO user authentication
                // oCompany.CompanyDB = "sbp_sioc_char"; // ERROR DE CONEXIÓN(100000060): B1 License Error Unknown error #100000060
                // oCompany.CompanyDB = "NDB";           // ERROR DE CONEXIÓN (100000060): B1 License Error Unknown error #100000060
                // oCompany.CompanyDB = "SBO_COMMON";    // ERROR DE CONEXIÓN (100000060): B1 License Error Unknown error #100000060

                oCompany.UserName = "gschneider";     // Usuario de SAP B1
                oCompany.Password = "TzLt3#MA";        // Contraseña de SAP B1
                oCompany.UseTrusted = false;
                oCompany.language = BoSuppLangs.ln_Spanish_La;

                Console.WriteLine("Conectando a SAP Business One...");
                _logger.Info("Conectando a SAP Business One...");
                int returnCode = oCompany.Connect();

                if (returnCode != 0)
                {
                    string errorMsg = oCompany.GetLastErrorDescription();
                    _logger.Info($"ERROR DE CONEXIÓN ({returnCode}): {errorMsg}");
                    Console.WriteLine($"ERROR DE CONEXIÓN ({returnCode}): {errorMsg}");
                    return;
                }
                _logger.Info($"Conectando a: {oCompany.Server} | SLD: {oCompany.SLDServer}...");
                Console.WriteLine($"Conectando a: {oCompany.Server} | SLD: {oCompany.SLDServer}...");

                RunAppAsync(oCompany).GetAwaiter().GetResult();

            }
            catch (Exception ex)
            {
                _logger.Error($"Excepción Crítica: {ex.Message}");
                Console.WriteLine($"Excepción Crítica: {ex.Message}");
            }
            finally
            {
                // ---------------------------------------------------------
                // 4. Desconexión y Limpieza
                // ---------------------------------------------------------
                if (oCompany != null)
                {
                    if (oCompany.Connected)
                    {
                        oCompany.Disconnect();
                        Console.WriteLine("Desconectado de SAP.");
                    }
                    Marshal.ReleaseComObject(oCompany);
                }
            }

            _logger.Info("TERMINO");
            Console.WriteLine("Presione ENTER para salir...");
            Console.ReadLine();
        }

        /// <summary>
        /// Método auxiliar para ejecutar lógica asíncrona
        /// </summary>
        private static async Task RunAppAsync(Company company)
        {
            var log = SimpleServiceProvider.Get<ILogger>();
            log.Info("Iniciando servicio de Padrón...");
            Console.WriteLine("Iniciando servicio de Padrón...");

            // Instanciamos el servicio pasando la compañía conectada
            var padronService = new PadronService(company);

            // Llamamos al método que creamos en el paso anterior
            await padronService.ProcesarPadron2025();
            log.Info("Servicio finalizado");
            Console.WriteLine("Servicio finalizado.");
        }
    }

}
