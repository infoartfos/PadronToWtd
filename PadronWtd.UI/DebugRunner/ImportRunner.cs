using PadronWdt.Repository.SL;
using PadronWtd.ServiceLayer;
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
        private readonly ServiceLayerClientDebug _slp;
        private readonly FrmImportarService _service;
        private readonly ILogger _logger;
        public ImportRunner()
        {
            _logger = SimpleServiceProvider.Get<ILogger>();
            // No existe _app (SAP) en debug mode, lo reemplazamos por null
            // _sl = new ServiceLayerClient("https://contreras-hanadb.sbo.contreras.com.ar:50000/b1s/v1/");
            _slp = new ServiceLayerClientDebug("https://contreras-hanadb.sbo.contreras.com.ar:50000/b1s/v1/");

            // Pasamos null como Application (no se usa para debug)
            _service = new FrmImportarService(app: null, _sl);

        }

        public async Task RunAsync()
        {
            var sl = new ServiceLayerClientDebug("https://contreras-hanadb.sbo.contreras.com.ar:50000/b1s/v1");

            Console.WriteLine($"Login SL...");
            //await _slp.LoginAsync( "gschneider", "TzLt3#MA", "SBP_SIOC_CHAR");
            //var items = await _slp.GetAsync("Items?$top=5");
            //// Display en consola los primeros 5 items
            //Console.WriteLine("Items obtenidos:");
            //Console.WriteLine(items);

            //if (!resp)
            //{
            //    Console.WriteLine($"NO SE LOGUEO.");
            //    Environment.Exit(100);
            //}
            //Console.WriteLine($"Login OK.");

            // ============================================================
            // 1) LEER CSV
            // ============================================================
            var lista = PSaltaCsvMapper.ReadCsv(@"C:\Users\cvalicenti\source\repos\PadronToWtd\etc\padron.csv");

            foreach (var r in lista)
            {
                await repo.CreateAsync(r);
            }

            // ============================================================
            // 2) EXPORTAR CSV DESDE SAP SL
            // ============================================================
            var datos = await repo.GetAllAsync();

            PSaltaCsvMapper.WriteCsv("padron_exportado.csv", datos);
        }




        //string archivo = @"C:\Users\cvalicenti\source\repos\PadronToWtd\etc\padron.csv";

        //Console.WriteLine($"Login SL...");
        //    await _slp.LoginAsync( "gschneider", "TzLt3#MA", "SBP_SIOC_CHAR");
        //var items = await _slp.GetAsync("Items?$top=5");
        //// Display en consola los primeros 5 items
        //Console.WriteLine("Items obtenidos:");
        //    Console.WriteLine(items);

        //    //if (!resp)
        //    //{
        //    //    Console.WriteLine($"NO SE LOGUEO.");
        //    //    Environment.Exit(100);
        //    //}
        //    Console.WriteLine($"Login OK.");

        //    Console.WriteLine($"Importando archivo: {archivo}");
        //    object value = await _service.ImportarAsync(archivo, msg => Console.WriteLine(msg));

        //Console.WriteLine("Importación completa.");



    }
}
