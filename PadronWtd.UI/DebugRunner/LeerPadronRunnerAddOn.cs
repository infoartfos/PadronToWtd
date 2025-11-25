using SAPbobsCOM;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
using SAPbouiCOM;
//using SAPbobsCOM;
using System;
using System.Threading.Tasks;

namespace PadronWtd.DebugRunner
{
    public class LeerPadronRunnerAddOn
    {
        private readonly ILogger _logger;
        private SAPbouiCOM.Application _application; // La aplicación visual (Formularios, Menús)
        private SAPbobsCOM.Company _company;         // La conexión de datos (DI API)

        public LeerPadronRunnerAddOn()
        {
            _logger = SimpleServiceProvider.Get<ILogger>();

        }

        public void Run()
        {
            try
            {
                // ---------------------------------------------------------
                // 1. Conexión "Single Sign-On" (SSO)
                // ---------------------------------------------------------
                // En lugar de new Company(), nos conectamos a la GUI que nos llamó.
                ConnectToSAPClient();

                if (_company == null || !_company.Connected)
                {
                    _logger.Error("No se pudo obtener la conexión DI API.");
                    return;
                }

                _logger.Info($"Conectado exitosamente a la empresa: {_company.CompanyName}");
                Console.WriteLine($"Conectado a: {_company.CompanyName}");

                // ---------------------------------------------------------
                // 2. Ejecutar tu lógica de negocio
                // ---------------------------------------------------------
                // Nota: En un Addon real, aquí no sueles ejecutar un proceso y salir.
                // Generalmente aquí cargas menús y esperas eventos (ItemEvent, MenuEvent).
                // Pero para mantener tu ejemplo, ejecutamos el servicio:

                RunAppAsync(_company).GetAwaiter().GetResult();

                // ---------------------------------------------------------
                // 3. Mantener el Addon vivo (Ciclo de vida)
                // ---------------------------------------------------------
                // Si esto es un Addon con formularios, necesitas mantener la aplicación corriendo.
                // En Windows Forms se usa Application.Run().
                // System.Windows.Forms.Application.Run(); 
            }
            catch (Exception ex)
            {
                _logger.Error($"Excepción Crítica en Addon: {ex.Message}");
                // En modo UI, mostramos un mensaje al usuario
                if (_application != null)
                    _application.MessageBox($"Error crítico: {ex.Message}");
            }
            finally
            {
                // IMPORTANTE EN ADDONS:
                // No hacemos _company.Disconnect() aquí si el Addon sigue vivo,
                // porque perderíamos la conexión para futuros eventos.
                // Solo desconectamos si la aplicación se está cerrando.
            }
        }
        /// <summary>
        /// Se conecta al Cliente SAP B1 y obtiene la instancia de Company compartida (DI API)
        /// </summary>
        private void ConnectToSAPClient()
        {
            try
            {
                // 1. Obtener la cadena de conexión de los argumentos de inicio
                // SAP pasa una cadena larga automáticamente cuando inicia el addon.
                // Si estamos depurando desde Visual Studio, usamos una cadena de desarrollo por defecto.
                string connectionString = Environment.GetCommandLineArgs().Length > 1
                    ? Environment.GetCommandLineArgs()[1]
                    : "0030002C0030002C00530041005000420044005F00440061007400650076002C0050004C006F006D0056004900490056";

                // 2. Conectar la GUI (SboGuiApi)
                var guiApi = new SboGuiApi();
                guiApi.Connect(connectionString);

                // 3. Obtener el objeto Application (UI)
                _application = guiApi.GetApplication();

                // 4. Obtener el objeto Company (DI) a través de la UI ("Cookie" connection)
                // Esto hace la magia: Usa la sesión actual del usuario. No pide password.
                _company = (SAPbobsCOM.Company)_application.Company.GetDICompany();

                _logger.Info("Conexión UI y DI establecida.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Falló la conexión con el cliente SAP: {ex.Message}");
            }
        }

        /// <summary>
        /// Método auxiliar para ejecutar lógica asíncrona
        /// </summary>
        private static async Task RunAppAsync(SAPbobsCOM.Company company)
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
