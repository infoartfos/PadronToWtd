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

        // UID constante para identificar este formulario unívocamente
        private const string FormUID = "frmPadron";

        public MainForm(Application app)
        {
            _app = app;
            string text = AppConstants.MainFormTitle;
            string apiUrl = AppSettings.ApiUrl;

            Console.WriteLine("Title  : " + text);

            // Llamamos a la creación
            CreateForm();
        }

        private void CreateForm()
        {
            try
            {
                // -----------------------------------------------------------
                // PATRÓN SINGLETON: Verificar si el form ya existe
                // -----------------------------------------------------------
                try
                {
                    // Intentamos obtener el formulario por su ID
                    _form = _app.Forms.Item(FormUID);

                    // Si no falla la línea anterior, significa que existe.
                    // Lo traemos al frente y salimos.
                    _form.Select();
                    return;
                }
                catch
                {
                    // Si cae aquí, el formulario no existe (SAP lanza excepción).
                    // Continuamos con la creación normal.
                }

                // -----------------------------------------------------------
                // CREACIÓN DEL FORMULARIO
                // -----------------------------------------------------------
                FormCreationParams cp = (FormCreationParams)_app.CreateObject(BoCreatableObjectType.cot_FormCreationParams);
                cp.UniqueID = FormUID;
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

            // Validamos que el evento sea para ESTE formulario
            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && !pVal.BeforeAction && FormUID == MainForm.FormUID)
            {
                switch (pVal.ItemUID)
                {
                    case "btnFecha":
                        ActivateMenuByTitle("Fechas de Procesamiento SALTA", "Fechas de Procesamiento");
                        break;

                    case "btnImp":
                        ActivateMenuByTitle("Parametros Padrón SALTA", "Parametros Padrón");
                        break;

                    case "btnProc":
                        OnImportarClick();
                        break;

                    case "btnTbl":
                        ActivateMenuByTitle("Padron Salta", "Padron Salta");
                        break;

                    default:
                        break;
                }
            }
        }

        private void OnImportarClick()
        {
            // FrmImportar ya debería tener su propia lógica Singleton en su método CreateForm
            var frmImportar = new FrmImportar(_app);
            frmImportar.CreateForm();
        }

        // --------------------------------------------------------------------------------------------
        // NUEVOS MÉTODOS PARA ABRIR POR NOMBRE (CON CONTROL DE DUPLICADOS)
        // --------------------------------------------------------------------------------------------

        private void ActivateMenuByTitle(string menuTitle, string windowTitle)
        {
            try
            {
                // 1. Verificar si ya hay un formulario abierto con ese título
                if (SelectFormByTitle(menuTitle, windowTitle))
                {
                    // Si ya estaba abierto y lo seleccionamos, no hacemos nada más.
                    return;
                }

                // 2. Si no estaba abierto, buscamos el menú y lo activamos
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
        /// Busca si existe un formulario abierto con el título exacto y le da foco.
        /// </summary>
        private bool SelectFormByTitle(string title, string secondary_title)
        {
            try
            {
                // Recorremos la colección de formularios abiertos
                for (int i = 0; i < _app.Forms.Count; i++)
                {
                    var frm = _app.Forms.Item(i);
                    string titulo = frm.Title.Trim();
                    if (titulo.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        secondary_title.Equals(titulo))
                    {
                        frm.Select(); // Traer al frente
                        return true;
                    }
                }
            }
            catch
            {
                // Ignorar errores al iterar forms
            }
            return false;
        }

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