using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PadronWtd.UI
{
class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
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
}
