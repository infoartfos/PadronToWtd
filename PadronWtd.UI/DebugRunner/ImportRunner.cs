using PadronWtd.UI.DI;
using PadronWtd.UI.Forms;   // donde está tu FrmImportarService
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
using PadronWtd.UI.SL;      // donde está tu ServiceLayerClient
using System;
using System.Threading.Tasks;

namespace PadronWtd.DebugRunner
{
    public class ImportRunner
    {
        private readonly ServiceLayerClient _sl;
        private readonly FrmImportarService _service;
        private readonly ILogger _logger;
        public ImportRunner()
        {
            _logger = SimpleServiceProvider.Get<ILogger>();
            // No existe _app (SAP) en debug mode, lo reemplazamos por null
            _sl = new ServiceLayerClient(_logger);

            // Pasamos null como Application (no se usa para debug)
            _service = new FrmImportarService(app: null, _sl);

        }

        public async Task RunAsync()
        {
            string archivo = @"C:\Users\cvalicenti\source\repos\PadronToWtd\etc\padron.csv";

            Console.WriteLine($"Login SL...");
            await _sl.LoginAsync();
            Console.WriteLine($"Login OK.");

            Console.WriteLine($"Importando archivo: {archivo}");
            object value = await _service.ImportarAsync(archivo, msg => Console.WriteLine(msg));

            Console.WriteLine("Importación completa.");
        }
    }
}
