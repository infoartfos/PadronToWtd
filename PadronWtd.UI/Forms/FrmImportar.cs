using SAPbobsCOM;
using SAPbouiCOM;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Concurrent;

namespace PadronSaltaAddOn.UI.Forms
{
    public class FrmImportar
    {
        private readonly SAPbouiCOM.Application SBO_Application;
        private SAPbouiCOM.Form oForm;
        private readonly ConcurrentQueue<string> _filePathQueue = new ConcurrentQueue<string>();

        private EditText txtId;
        private SAPbouiCOM.ComboBox cmbPeriodo;
        private EditText txtArchivo;
        private SAPbouiCOM.Button btnBrowse;
        private SAPbouiCOM.Button btnImport;
        private StaticText lblResumen;

        public FrmImportar(SAPbouiCOM.Application application)
        {
            SBO_Application = application;
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
            btnImport = AddButton("btnImport", "Importar y Procesar", left + lblWidth, top, 200);

            // Resumen
            top += spacing * 2;
            lblResumen = AddLabel("lblResumen", "", left, top);
            lblResumen.Item.Width = 450;

            // Eventos
            SBO_Application.ItemEvent += SBO_Application_ItemEvent;

            oForm.Visible = true;
        }

        public string GetFormUid()
        {
            return oForm.UniqueID;
        }

        // --------------------------------------------
        // EVENTOS
        // --------------------------------------------

        private void SBO_Application_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            // Procesar la cola APENAS SAP refresca el form
            if (FormUID == "FrmImportar" && pVal.EventType == BoEventTypes.et_FORM_ACTIVATE)
            {
                ProcesarColaDeArchivos();
            }

            // Otros eventos propios del form
            if (FormUID != "FrmImportar" || !pVal.BeforeAction)
                return;

            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && pVal.ItemUID == "btnImport")
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

        // --------------------------------------------
        // PROCESAR COLA
        // --------------------------------------------
        private void ProcesarColaDeArchivos()
        {
            if (_filePathQueue.TryDequeue(out string filePath))
            {
                try
                {
                    Console.WriteLine("[DEBUG] Procesando archivo desde cola: " + filePath);

                    oForm.Freeze(true);
                    EditText field = (EditText)oForm.Items.Item("txtArchivo").Specific;
                    field.Value = filePath;
                }
                catch (Exception ex)
                {
                    SBO_Application.StatusBar.SetText(
                        $"Error al actualizar archivo: {ex.Message}",
                        BoMessageTime.bmt_Short,
                        BoStatusBarMessageType.smt_Error
                    );
                }
                finally
                {
                    oForm.Freeze(false);
                }
            }
        }

        // --------------------------------------------
        // BOTÓN EXAMINAR
        // --------------------------------------------
        private void SeleccionarArchivo()
        {
            var t = new Thread(() =>
            {
                try
                {
                    using (var dialog = new OpenFileDialog())
                    {
                        dialog.Filter = "Archivos CSV|*.csv|Todos los archivos|*.*";
                        dialog.Title = "Seleccionar archivo de padrón";
                        dialog.RestoreDirectory = true;

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            Console.WriteLine("[DEBUG] Archivo seleccionado: " + dialog.FileName);
                            _filePathQueue.Enqueue(dialog.FileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DEBUG] Error en hilo de diálogo: " + ex.Message);
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        // --------------------------------------------
        // PROCESAR ARCHIVO
        // --------------------------------------------
        private void ProcesarArchivo()
        {
            string path = txtArchivo.Value.Trim();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                SBO_Application.MessageBox("Debe seleccionar un archivo válido.");
                return;
            }

            string periodo = cmbPeriodo.Selected.Description;
            int confirm = SBO_Application.MessageBox(
                $"¿Confirmar importación del archivo para el período:\n{periodo}?",
                2, "Sí", "No", "");

            if (confirm != 1)
                return;

            // Simulación del proceso
            int totalRegistros = 17898;
            int actualizados = 109;
            int noEnPadron = 2;

            lblResumen.Caption =
                $"Registros del Padrón: {totalRegistros}\n" +
                $"Registros Actualizados: {actualizados}\n" +
                $"Registros NO en Padrón: {noEnPadron}\n\n" +
                $"FINALIZADO";

            SBO_Application.StatusBar.SetText(
                "Importación finalizada correctamente.",
                BoMessageTime.bmt_Medium,
                BoStatusBarMessageType.smt_Success
            );
        }

        // --------------------------------------------
        // HELPERS UI
        // --------------------------------------------
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

        private SAPbouiCOM.ComboBox AddComboBox(string uid, int left, int top, int width)
        {
            Item item = oForm.Items.Add(uid, BoFormItemTypes.it_COMBO_BOX);
            item.Left = left;
            item.Top = top;
            item.Width = width;
            return (SAPbouiCOM.ComboBox)item.Specific;
        }

        private SAPbouiCOM.Button AddButton(string uid, string caption, int left, int top, int width)
        {
            Item item = oForm.Items.Add(uid, BoFormItemTypes.it_BUTTON);
            item.Left = left;
            item.Top = top;
            item.Width = width;
            SAPbouiCOM.Button btn = (SAPbouiCOM.Button)item.Specific;
            btn.Caption = caption;
            return btn;
        }
    }
}
