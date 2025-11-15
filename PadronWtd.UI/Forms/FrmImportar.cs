using SAPbobsCOM;
using SAPbouiCOM;
using System;
using System.IO;
// using System.Windows.Forms;

namespace PadronSaltaAddOn.UI.Forms
{
    public class FrmImportar
    {
        private readonly Application SBO_Application;
        // private readonly Company oCompany;
        private Form oForm;

        private EditText txtId;
        private ComboBox cmbPeriodo;
        private EditText txtArchivo;
        private Button btnBrowse;
        private Button btnImportar;
        private StaticText lblResumen;

        // public FrmImportar(Application application, Company company)
        public FrmImportar(Application application)
        {
            SBO_Application = application;
            //oCompany = company;
        }

        public void CreateForm()
        {
            FormCreationParams creationPackage =
                (FormCreationParams)SBO_Application.CreateObject(BoCreatableObjectType.cot_FormCreationParams);

            creationPackage.UniqueID = "FrmImportar";
            creationPackage.FormType = "FrmImportar";
            creationPackage.BorderStyle = BoFormBorderStyle.fbs_Sizable;

            oForm = SBO_Application.Forms.AddEx(creationPackage);
            oForm.Title = "Importar y Procesar";
            oForm.Width = 520;
            oForm.Height = 350;

            int left = 20, top = 30, lblWidth = 150, fieldWidth = 250, spacing = 30;

            // ID
            AddLabel("lblId", "ID:", left, top);
            txtId = AddEditText("txtId", left + lblWidth, top, fieldWidth);
            txtId.Value = "1";

            // Período
            top += spacing;
            AddLabel("lblPeriodo", "Período a Procesar:", left, top);
            cmbPeriodo = AddComboBox("cmbPeriodo", left + lblWidth, top, fieldWidth);
            cmbPeriodo.ValidValues.Add("1", "Ejecución 1 - Primer Trimestre");
            cmbPeriodo.ValidValues.Add("2", "Ejecución 2 - Segundo Trimestre");
            cmbPeriodo.ValidValues.Add("3", "Ejecución 3 - Tercer Trimestre");
            cmbPeriodo.ValidValues.Add("4", "Ejecución 4 - Cuarto Trimestre");
            cmbPeriodo.Select("1", BoSearchKey.psk_ByValue);

            // Archivo
            top += spacing;
            AddLabel("lblArchivo", "Archivo a procesar:", left, top);
            txtArchivo = AddEditText("txtArchivo", left + lblWidth, top, fieldWidth - 60);
            btnBrowse = AddButton("btnBrowse", "...", left + lblWidth + fieldWidth - 50, top, 40);

            // Botón Importar
            top += spacing * 2;
            btnImportar = AddButton("btnImportar", "Importar y Procesar", left + lblWidth, top, 200);


            // Resumen
            top += spacing * 2;
            lblResumen = AddLabel("lblResumen", "", left, top);
            lblResumen.Item.Width = 450;

            // Evento
            SBO_Application.ItemEvent += SBO_Application_ItemEvent;

            oForm.Visible = true;
        }

        private void SBO_Application_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            if (FormUID != "FrmImportar" || !pVal.BeforeAction)
                return;

            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && pVal.ItemUID == "btnImportar")
            {
                BubbleEvent = false;
                ProcesarArchivo();
            }

            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && pVal.ItemUID == "btnBrowse")
            {
                BubbleEvent = false;
                SeleccionarArchivo();
            }
        }

        private void SeleccionarArchivo()
        {
            try
            {
                // SAP no tiene un diálogo nativo, se puede usar FileDialog de .NET:
                using (var dialog = new System.Windows.Forms.OpenFileDialog())
                {
                    dialog.Filter = "Archivos CSV|*.csv|Todos los archivos|*.*";
                    dialog.Title = "Seleccionar archivo de padrón";

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        txtArchivo.Value = dialog.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                SBO_Application.StatusBar.SetText($"Error al seleccionar archivo: {ex.Message}", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
            }
        }

        private void ProcesarArchivo()
        {
            string path = txtArchivo.Value.Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                SBO_Application.MessageBox("Debe seleccionar un archivo válido.");
                return;
            }

            string periodo = cmbPeriodo.Selected.Description;
            int confirm = SBO_Application.MessageBox($"¿Confirmar importación del archivo para el período:\n{periodo}?", 2, "Sí", "No", "");
            if (confirm != 1)
                return;

            // Simulación del proceso
            int totalRegistros = 17898;
            int actualizados = 109;
            int noEnPadron = 2;

            // Acá podrías llamar a un servicio de dominio real (por ejemplo: PadronService.ImportarArchivo())

            lblResumen.Caption =
                $"Registros del Padrón: {totalRegistros}\n" +
                $"Registros Actualizados: {actualizados}\n" +
                $"Registros NO en Padrón: {noEnPadron}\n\n" +
                $"FINALIZADO";

            SBO_Application.StatusBar.SetText("Importación finalizada correctamente.", BoMessageTime.bmt_Medium, BoStatusBarMessageType.smt_Success);
        }

        // Helpers UI
        private StaticText AddLabel(string uid, string caption, int left, int top)
        {
            Item item = oForm.Items.Add(uid, BoFormItemTypes.it_STATIC);
            item.Left = left;
            item.Top = top;
            item.Width = 150;
            StaticText label = (StaticText)item.Specific;
            label.Caption = caption;
            return label;
        }

        private EditText AddEditText(string uid, int left, int top, int width)
        {
            Item item = oForm.Items.Add(uid, BoFormItemTypes.it_EDIT);
            item.Left = left;
            item.Top = top;
            item.Width = width;
            return (EditText)item.Specific;
        }

        private ComboBox AddComboBox(string uid, int left, int top, int width)
        {
            Item item = oForm.Items.Add(uid, BoFormItemTypes.it_COMBO_BOX);
            item.Left = left;
            item.Top = top;
            item.Width = width;
            return (ComboBox)item.Specific;
        }

        private Button AddButton(string uid, string caption, int left, int top, int width)
        {
            Item item = oForm.Items.Add(uid, BoFormItemTypes.it_BUTTON);
            item.Left = left;
            item.Top = top;
            item.Width = width;
            Button btn = (Button)item.Specific;
            btn.Caption = caption;
            return btn;
        }
    }
}
