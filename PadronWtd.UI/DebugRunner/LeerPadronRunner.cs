using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
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
            // oCompany.LicenseServer = "hanab1:40000"; // No va en SAP B1 10.x
            // Base de Datos y Credenciales
            //oCompany.DbUserName = "USERINTDEV";
            //oCompany.DbPassword = "Argentina2025!";
            var pass = "1&ns$YI5";
            Company oCompany = null;
            try
            {
                // ---------------------------------------------------------
                // 1. Configuración de la Conexión
                // ---------------------------------------------------------
                oCompany = new Company();
                Console.WriteLine("64 bit process: " + Environment.Is64BitProcess);
                Console.WriteLine("DI API version: " + oCompany.MinimalSupportedVersion);
                oCompany.DbServerType = BoDataServerTypes.dst_HANADB;
                oCompany.Server = "hanab1.sbo.contreras.com.ar";  // Si cambio esto no hay cambios,   con xxxx tambien da  ERROR DE CONEXIÓN (-132): Error during SBO user authentication
                oCompany.SLDServer = "hanab1.sbo.contreras.com.ar:40000"; // Si descomento da error ERROR DE CONEXIÓN(100000060): B1 License Error Unknown error #100000060

                oCompany.Server = "10.250.2.10";  // Si cambio esto no hay cambios,   con xxxx tambien da  ERROR DE CONEXIÓN (-132): Error during SBO user authentication
                oCompany.SLDServer = "10.250.2.10:40000"; // Si descomento da error ERROR DE CONEXIÓN(100000060): B1 License Error Unknown error #100000060


                oCompany.CompanyDB = "SBP_SIOC_CHAR"; // ERROR DE CONEXIÓN (-132): Error during SBO user authentication
                oCompany.UserName = "DESARROLLOS"; // Usuario de SAP B1
                oCompany.Password = pass;        // Contraseña de SAP B1
                oCompany.UseTrusted = false;
                oCompany.language = BoSuppLangs.ln_Spanish_La;

                Console.WriteLine("Conectando a SAP Business One...");
                _logger.Info("Conectando a SAP Business One...");

                string connectString = $"Conexion a: " +
                                             $"\n-Server:       {oCompany.Server} " +
                                             $"\n-DbServerType: {oCompany.DbServerType} " +
                                             $"\n-SLDServer:    {oCompany.SLDServer} " +
                                             $"\n-CompanyDB:    {oCompany.CompanyDB} " +
                                             $"\n-UserName:     {oCompany.UserName} " +
                                             // $"\n-pass:         {pass} " +
                                             $"\n-DbUserName:   {oCompany.DbUserName} " +
                                             $"\n-LicenseServer:{oCompany.LicenseServer} ";
                _logger.Info(connectString);


                int returnCode = oCompany.Connect();

                if (returnCode != 0)
                {
                    string errorMsg = oCompany.GetLastErrorDescription();
                    _logger.Info($"ERROR DE CONEXIÓN ({returnCode}): {errorMsg}");
                    Console.WriteLine($"ERROR DE CONEXIÓN ({returnCode}): {errorMsg}");
                    Console.WriteLine($"({connectString})");
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
