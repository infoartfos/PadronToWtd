using PadronWtd.UI.Constants;
using PadronWtd.UI.Forms;
using SAPbouiCOM.Framework;
using System;

namespace PadronWtd.UI
{
    internal class Menu
    {
        public void AddMenuItems()
        {
            var oApp = Application.SBO_Application;
            SAPbouiCOM.Menus oMenus = oApp.Menus;
            SAPbouiCOM.MenuItem oMenuItem;

            var oCreationPackage =
                (SAPbouiCOM.MenuCreationParams)oApp.CreateObject(
                    SAPbouiCOM.BoCreatableObjectType.cot_MenuCreationParams
                );

            // Obtener menú "Modules"
            oMenuItem = oMenus.Item(MenuConstants.ModulesMenuId);
            oMenus = oMenuItem.SubMenus;

            // Crear menú raíz
            oCreationPackage.Type = SAPbouiCOM.BoMenuType.mt_POPUP;
            oCreationPackage.UniqueID = MenuConstants.RootMenuId;
            oCreationPackage.String = MenuConstants.RootMenuTitle;
            oCreationPackage.Position = -1;

            try
            {
                oMenus.AddEx(oCreationPackage);
            }
            catch
            {
                // ya existe
            }

            // Submenú Padrón Salta
            try
            {
                oMenuItem = oApp.Menus.Item(MenuConstants.RootMenuId);
                oMenus = oMenuItem.SubMenus;

                oCreationPackage.Type = SAPbouiCOM.BoMenuType.mt_STRING;
                oCreationPackage.UniqueID = MenuConstants.MenuPadronSaltaId;
                oCreationPackage.String = MenuConstants.MenuPadronSaltaTitle;

                oMenus.AddEx(oCreationPackage);
            }
            catch
            {
                oApp.SetStatusBarMessage(
                    AppConstants.MenuAlreadyExists,
                    SAPbouiCOM.BoMessageTime.bmt_Short,
                    true
                );
            }
        }

        public void SBO_Application_MenuEvent(ref SAPbouiCOM.MenuEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            try
            {
                if (pVal.BeforeAction && pVal.MenuUID == MenuConstants.MenuPadronSaltaId)
                {
                    var form = new MainForm(Application.SBO_Application);
                }
            }
            catch (Exception ex)
            {
                Application.SBO_Application.MessageBox(
                    AppConstants.ErrorUnexpected + "\n" + ex.Message,
                    1, "OK"
                );
            }
        }
    }
}
