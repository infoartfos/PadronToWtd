using System;
using System.Collections.Generic;
using SAPbouiCOM.Framework;

namespace PadronWtd.UI
{
    class Program
    {

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Application oApp = null;
                if (args.Length < 1)
                {
                    oApp = new Application();
                }
                else
                {
                    //If you want to use an add-on identifier for the development license, you can specify an add-on identifier string as the second parameter.
                    //oApp = new Application(args[0], "XXXXX");
                    oApp = new Application(args[0]);
                }
                Menu MyMenu = new Menu();
                MyMenu.AddMenuItems();
                oApp.RegisterMenuEventHandler(MyMenu.SBO_Application_MenuEvent);

                Application.SBO_Application.AppEvent += new SAPbouiCOM._IApplicationEvents_AppEventEventHandler(SBO_Application_AppEvent);
                Application.SBO_Application.ItemEvent += SBO_Application_ItemEvent;
                oApp.Run();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
        }

        public static SAPbouiCOM.Form oForm;
        public static SAPbouiCOM.Item oItem;
        public static SAPbouiCOM.Item oOldItem;

        static void SBO_Application_ItemEvent(string FormUID, ref SAPbouiCOM.ItemEvent pVal, out bool BubbleEvent)
        {
            //throw new NotImplementedException();
            BubbleEvent = true;
            if ((pVal.FormType == 146 && pVal.EventType != SAPbouiCOM.BoEventTypes.et_FORM_UNLOAD) && pVal.Before_Action == true)
            {
                oForm = Application.SBO_Application.Forms.GetFormByTypeAndCount(pVal.FormType, pVal.FormTypeCount);
                if (pVal.EventType == SAPbouiCOM.BoEventTypes.et_FORM_LOAD && pVal.Before_Action == true)
                {
                    SAPbouiCOM.Button obt = null;
                    oOldItem = oForm.Items.Item("2");
                    oItem = oForm.Items.Add("btnNew", SAPbouiCOM.BoFormItemTypes.it_BUTTON);
                    oItem.Top = oOldItem.Top;
                    oItem.Height = oOldItem.Height;
                    oItem.Left = oOldItem.Left + oOldItem.Width + 5;
                    oItem.Width = oOldItem.Width + 40;
                    obt = (SAPbouiCOM.Button)oItem.Specific;
                    obt.Caption = "Lectora de Cheques";
                }
                if (pVal.ItemUID == "btnNew" & (pVal.EventType == SAPbouiCOM.BoEventTypes.et_ITEM_PRESSED || pVal.EventType == SAPbouiCOM.BoEventTypes.et_CLICK) & pVal.Before_Action == true)
                {
                    Application.SBO_Application.ActivateMenuItem("Lectora_de_Cheques.Form1");
                }
            }
        }

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

  