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

                // 4. Inicializar la variable Global App
                App.SBO_Application = guiApi.GetApplication(-1);

                // 5. Inicializar la DI API (Company) automáticamente (SSO)
                // Esto es vital para que tus repositorios funcionen
                App.Company = (SAPbobsCOM.Company)App.SBO_Application.Company.GetDICompany();

                // 6. Configurar Menús
                // Asumo que tu clase Menu tiene lógica interna
                Menu MyMenu = new Menu();
                MyMenu.AddMenuItems();
                // CORRECCIÓN: Suscribir evento de menú estándar
                App.SBO_Application.MenuEvent += new _IApplicationEvents_MenuEventEventHandler(MyMenu.SBO_Application_MenuEvent);

                // 7. Suscribir Eventos Globales
                App.SBO_Application.AppEvent += new _IApplicationEvents_AppEventEventHandler(SBO_Application_AppEvent);

                // Si tienes un manejador de ItemEvent:
                // App.SBO_Application.ItemEvent += new _IApplicationEvents_ItemEventEventHandler(SBO_Application_ItemEvent);

                // 8. Bucle principal (Mantiene el Addon vivo)
                // CORRECCIÓN: 'oApp.Run()' no existe en la API estándar. 
                // Se usa el loop de Windows Forms.
                System.Windows.Forms.Application.Run();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
        }

        public static SAPbouiCOM.Form oForm;
        public static SAPbouiCOM.Item oItem;
        public static SAPbouiCOM.Item oOldItem;

        //static void SBO_Application_ItemEvent(string FormUID, ref SAPbouiCOM.ItemEvent pVal, out bool BubbleEvent)
        //{
        //    //throw new NotImplementedException();
        //    BubbleEvent = true;
        //    if ((pVal.FormType == 146 && pVal.EventType != SAPbouiCOM.BoEventTypes.et_FORM_UNLOAD) && pVal.Before_Action == true)
        //    {
        //        oForm = App.SBO_Application.Forms.GetFormByTypeAndCount(pVal.FormType, pVal.FormTypeCount);
        //        if (pVal.EventType == SAPbouiCOM.BoEventTypes.et_FORM_LOAD && pVal.Before_Action == true)
        //        {
        //            SAPbouiCOM.Button obt = null;
        //            oOldItem = oForm.Items.Item("2");
        //            oItem = oForm.Items.Add("btnNew", SAPbouiCOM.BoFormItemTypes.it_BUTTON);
        //            oItem.Top = oOldItem.Top;
        //            oItem.Height = oOldItem.Height;
        //            oItem.Left = oOldItem.Left + oOldItem.Width + 5;
        //            oItem.Width = oOldItem.Width + 40;
        //            obt = (SAPbouiCOM.Button)oItem.Specific;
        //            obt.Caption = "Lectora de Cheques";
        //        }
        //        if (pVal.ItemUID == "btnNew" & (pVal.EventType == SAPbouiCOM.BoEventTypes.et_ITEM_PRESSED || pVal.EventType == SAPbouiCOM.BoEventTypes.et_CLICK) & pVal.Before_Action == true)
        //        {
        //            App.SBO_Application.ActivateMenuItem("Lectora_de_Cheques.Form1");
        //        }
        //    }
        //}

        static void SBO_Application_AppEvent(SAPbouiCOM.BoAppEventTypes EventType)
        {
            switch (EventType)
            {
                case SAPbouiCOM.BoAppEventTypes.aet_ShutDown:
                    //Exit Add-On
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

  