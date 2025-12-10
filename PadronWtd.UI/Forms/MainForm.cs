using PadronWtd.DebugRunner;
using PadronWtd.UI.Configuration;
using PadronWtd.UI.Constants;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbouiCOM;
using System;
using System.IO;

namespace PadronWtd.UI.Forms
{
    internal class MainForm
    {
        private readonly Application _app;
        private Form _form;

        public MainForm(Application app)
        {
            _app = app;
            string text = AppConstants.MainFormTitle;
            string apiUrl = AppSettings.ApiUrl;

            Console.WriteLine("Title  : " + text);
            CreateForm();
        }

        private void CreateForm()
        {
            try
            {
                FormCreationParams cp = (FormCreationParams)_app.CreateObject(BoCreatableObjectType.cot_FormCreationParams);
                cp.UniqueID = "frmPadron";
                cp.FormType = "frmPadron";
                cp.BorderStyle = BoFormBorderStyle.fbs_Fixed;

                _form = _app.Forms.AddEx(cp);
                _form.Title = "ACTUALIZACION IMPOSITIVA *SALTA*";
                _form.Width = 430;
                _form.Height = 300;

                Item label = _form.Items.Add("lblOpt", BoFormItemTypes.it_STATIC);
                label.Top = 40; label.Left = 20;
                ((StaticText)label.Specific).Caption = "Opciones:";

                AddButton("btnFecha", "Mantenimiento de Fecha", 70);
                AddButton("btnImp", "Mantenimiento de Impuestos", 110);
                AddButton("btnProc", "Importar y procesar", 150);
                AddButton("btnTbl", "Ver tabla importación", 190);

                _app.ItemEvent += App_ItemEvent;
                _form.Visible = true;
            }
            catch (Exception ex)
            {
                _app.MessageBox("Error al crear form principal: " + ex.Message);
            }
        }

        private void AddButton(string id, string caption, int top)
        {
            Item btn = _form.Items.Add(id, BoFormItemTypes.it_BUTTON);
            btn.Top = top; btn.Left = 40; btn.Width = 200;
            ((Button)btn.Specific).Caption = caption;
        }

        private void App_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;
            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && !pVal.BeforeAction && FormUID == "frmPadron")
            {
                switch (pVal.ItemUID)
                {
                    case "btnFecha":
                        //try
                        //{
                        //    string xmlMenus = _app.Menus.GetAsXML();
                        //    string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PadronWtd");
                        //    string menuFile = Path.Combine(appData, "SapMenus.xml");
                        //    System.IO.File.WriteAllText(menuFile, xmlMenus);
                        //    _app.MessageBox("Menús exportados a " + menuFile);
                        //}
                        //catch (Exception ex)
                        //{
                        //    _app.MessageBox("Error: " + ex.Message);
                        //}
                        // Abre buscando el título exacto tal cual se ve en SAP
                        ActivateMenuByTitle("Fechas de Procesamiento SALTA");
                        break;

                    case "btnImp":
                        ActivateMenuByTitle("Parametros Padrón SALTA");
                        break;

                    case "btnProc":
                        OnImportarClick();
                        break;

                    case "btnTbl":
                        ActivateMenuByTitle("Padron Salta");
                        break;

                    default:
                        break;
                }
            }
        }

        private void OnImportarClick()
        {
            var frmImportar = new FrmImportar(_app);
            frmImportar.CreateForm();
        }

        // --------------------------------------------------------------------------------------------
        // NUEVOS MÉTODOS PARA ABRIR POR NOMBRE
        // --------------------------------------------------------------------------------------------

        private void ActivateMenuByTitle(string menuTitle)
        {
            try
            {
                _app.StatusBar.SetText($"Buscando menú '{menuTitle}'...", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);

                string menuId = FindMenuIdRecursive(_app.Menus, menuTitle);

                if (!string.IsNullOrEmpty(menuId))
                {
                    _app.Menus.Item(menuId).Activate();
                }
                else
                {
                    _app.MessageBox($"No se encontró ningún menú con el nombre: '{menuTitle}'");
                }
            }
            catch (Exception ex)
            {
                _app.MessageBox($"Error al activar menú: {ex.Message}");
            }
        }

        /// <summary>
        /// Busca recursivamente en la estructura de árbol de menús de SAP.
        /// </summary>
        private string FindMenuIdRecursive(SAPbouiCOM.Menus menus, string titleToFind)
        {
            for (int i = 0; i < menus.Count; i++)
            {
                try
                {
                    SAPbouiCOM.MenuItem item = menus.Item(i);
                    if (item.String.Trim().Equals(titleToFind.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return item.UID;
                    }
                    if (item.Type == BoMenuType.mt_POPUP && item.SubMenus.Count > 0)
                    {
                        string foundId = FindMenuIdRecursive(item.SubMenus, titleToFind);
                        if (!string.IsNullOrEmpty(foundId))
                        {
                            return foundId;
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }
    }
}