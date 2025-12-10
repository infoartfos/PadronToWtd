using PadronWtd.DebugRunner;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbouiCOM;
using System;
using System.IO;

namespace PadronWtd.UI
{
    class Program
    {

        [STAThread]
        static void Main(string[] args)
        {
            try
            {

                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PadronWtd");

                string logFile = Path.Combine(appData, "padron_import.log");
                SimpleServiceProvider.RegisterDefaults(logFile);

                var DEBUG = false;

                if (DEBUG)
                {
                    var _logger = SimpleServiceProvider.Get<ILogger>();

                    _logger.Info("=== DEBUG ARRANCANDO ====");
                    // var runner = new ImportRunner();
                    var runner = new LeerPadronRunner();
                    runner.Run();
                    _logger.Info("=== TERMINO  ====");

                    Environment.Exit(0);

                }

                SboGuiApi guiApi = new SboGuiApi();

                if (args.Length < 1)
                {
                    // Si no hay argumentos, usamos la cadena de desarrollo
                    guiApi.Connect("0030002C0030002C00530041005000420044005F00440061007400650076002C0050004C006F006D0056004900490056");
                }
                else
                {
                    guiApi.Connect(args[0]);
                }

                App.SBO_Application = guiApi.GetApplication(-1);
                App.Company = (SAPbobsCOM.Company)App.SBO_Application.Company.GetDICompany();
                Menu MyMenu = new Menu();
                MyMenu.AddMenuItems();
                App.SBO_Application.MenuEvent += new _IApplicationEvents_MenuEventEventHandler(MyMenu.SBO_Application_MenuEvent);
                App.SBO_Application.AppEvent += new _IApplicationEvents_AppEventEventHandler(SBO_Application_AppEvent);
                System.Windows.Forms.Application.Run();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
        }

        public static SAPbouiCOM.Form oForm;
        public static SAPbouiCOM.Item oItem;

        static void SBO_Application_AppEvent(SAPbouiCOM.BoAppEventTypes EventType)
        {
            switch (EventType)
            {
                case SAPbouiCOM.BoAppEventTypes.aet_ShutDown:
                    System.Windows.Forms.Application.Exit();
                    break;
                case SAPbouiCOM.BoAppEventTypes.aet_CompanyChanged:
                    break;
                case SAPbouiCOM.BoAppEventTypes.aet_FontChanged:
                    break;
                case SAPbouiCOM.BoAppEventTypes.aet_LanguageChanged:
                    break;
                case SAPbouiCOM.BoAppEventTypes.aet_ServerTerminition:
                    break;
                default:
                    break;
            }
        }
    }
}

  