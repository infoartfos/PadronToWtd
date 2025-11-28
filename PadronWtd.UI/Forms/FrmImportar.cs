using PadronWtd.UI.DI;
using PadronWtd.UI.Helpers;
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
using SAPbouiCOM;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PadronWtd.UI.Forms
{
    public class FrmImportar
    {
        // Constantes para IDs de Controles (Evita errores de tipeo)
        private const string FormUID = "FrmImp";
        private const string CmbPeriodoID = "cmbPeriodo";
        private const string TxtArchivoID = "txtArchivo";
        private const string BtnBrowseID = "btnBrowse";
        private const string BtnImportID = "btnImport";
        private const string LblResumenID = "lblResumen";
        private const string LblProgressID = "lblProgr";

        private readonly SAPbouiCOM.Application _application;
        private readonly ILogger _logger;
        private readonly FileImportService _importService;

        // Cola para comunicación entre el hilo de diálogo (STA) y la UI de SAP
        private readonly ConcurrentQueue<string> _filePathQueue = new ConcurrentQueue<string>();

        private Form _oForm;

        public FrmImportar(SAPbouiCOM.Application application)
        {
            _application = application;
            _logger = SimpleServiceProvider.Get<ILogger>();
            _importService = new FileImportService();
        }

        /// <summary>
        /// Método principal llamado desde el Menú. 
        /// Verifica si el form existe antes de crearlo.
        /// </summary>
        public void CreateForm()
        {
            try
            {
                // 1. Patrón Singleton: Si el formulario ya existe, solo lo traemos al frente.
                try
                {
                    var existingForm = _application.Forms.Item(FormUID);
                    existingForm.Select();
                    return;
                }
                catch
                {
                    // El formulario no existe (SAP lanza excepción), continuamos creándolo.
                }

                // 2. Crear la UI visualmente
                BuildUserInterface();

                // 3. Suscribir eventos
                _application.ItemEvent += SBO_Application_ItemEvent;
            }
            catch (Exception ex)
            {
                _logger.Error("Error al abrir el formulario de importación", ex);
                _application.MessageBox("Error al abrir formulario: " + ex.Message);
            }
        }

        private void BuildUserInterface()
        {
            FormCreationParams creationPackage = (FormCreationParams)_application.CreateObject(BoCreatableObjectType.cot_FormCreationParams);
            creationPackage.UniqueID = FormUID;
            creationPackage.FormType = "FrmImpType";
            creationPackage.BorderStyle = BoFormBorderStyle.fbs_Sizable;

            _oForm = _application.Forms.AddEx(creationPackage);
            _oForm.Title = "Importar y Procesar Padrón";
            _oForm.Width = 520;
            _oForm.Height = 380;
            _oForm.Visible = true;
            _oForm.AutoManaged = true;

            // Layout
            int left = 20, top = 30, lblWidth = 150, fieldWidth = 250, spacing = 30;

            // 1. Periodo
            top += spacing;
            AddLabel("lblPer", "Período a Procesar:", left, top);
            var cmb = AddComboBox(CmbPeriodoID, left + lblWidth, top, fieldWidth);
            FillPeriodos(cmb);

            // 2. Archivo
            top += spacing;
            AddLabel("lblFile", "Archivo a procesar:", left, top);
            AddEditText(TxtArchivoID, left + lblWidth, top, fieldWidth - 60);
            AddButton(BtnBrowseID, "...", left + lblWidth + fieldWidth - 50, top, 40);

            // 3. Botón Acción
            top += spacing * 2;
            AddButton(BtnImportID, "Importar y Procesar", left + lblWidth, top, 200);

            // 4. Feedback
            top += spacing * 2;
            var lblRes = AddLabel(LblResumenID, "Listo.", left, top);
            lblRes.Item.Width = 450;

            top += spacing + 10;
            var lblProg = AddLabel(LblProgressID, "Estado: Esperando archivo...", left, top);
            lblProg.Item.Width = 450;
        }

        private void FillPeriodos(SAPbouiCOM.ComboBox cmb)
        {
            cmb.ValidValues.Add("2025 Q1", "Q1 2025");
            cmb.ValidValues.Add("2025 Q2", "Q2 2025");
            cmb.ValidValues.Add("2025 Q3", "Q3 2025");
            cmb.ValidValues.Add("2025 Q4", "Q4 2025");
            // Seleccionar por defecto (con try por seguridad)
            try { cmb.Select("2025 Q1", BoSearchKey.psk_ByValue); } catch { }
        }

        private void SBO_Application_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            // Filtrar eventos solo para este formulario
            if (FormUID != FrmImportar.FormUID) return;

            // WORKAROUND: Evento Activate para chequear la cola de archivos
            // (Ya que el OpenFileDialog corre en otro hilo, no puede escribir directo en SAP)
            if (pVal.EventType == BoEventTypes.et_FORM_ACTIVATE)
            {
                CheckFileQueue();
            }

            // Eventos de Click (After Action)
            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && !pVal.BeforeAction)
            {
                switch (pVal.ItemUID)
                {
                    case BtnBrowseID:
                        HandleBrowseClick();
                        break;
                    case BtnImportID:
                        HandleImportClick();
                        break;
                }
            }
        }

        private void HandleBrowseClick()
        {
            // Delegamos la apertura del diálogo al Helper
            FileDialogHelper.OpenFileDialog((fileName) =>
            {
                // Encolamos el resultado para que el evento FORM_ACTIVATE lo lea
                _filePathQueue.Enqueue(fileName);
            });
        }

        private void HandleImportClick()
        {
            // Obtener valores UI
            string filePath = ((EditText)_oForm.Items.Item(TxtArchivoID).Specific).Value;
            string valPeriodo = ((SAPbouiCOM.ComboBox)_oForm.Items.Item(CmbPeriodoID).Specific).Value;

            if (string.IsNullOrEmpty(filePath))
            {
                _application.StatusBar.SetText("Seleccione un archivo.", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
                return;
            }

            // Parsear periodo (Ej: "2025 Q1")
            string year = "2025";
            string qValue = "Q1";
            var parts = valPeriodo.Split(' ');
            if (parts.Length > 1)
            {
                year = parts[0];
                qValue = parts[1];
            }

            // Ejecutar lógica asíncrona
            _ = RunImportProcessAsync(filePath, year, qValue);
        }

        private async Task RunImportProcessAsync(string filePath, string year, string qValue)
        {
            try
            {
                SetUIBusy(true);
                UpdateStatus("Leyendo y procesando archivo...");

                // Llamada al Servicio de Negocio
                int count = await _importService.ProcessImportAsync(filePath, year, qValue);

                if (count > 0)
                {
                    UpdateStatus($"¡Éxito! {count} registros procesados.");
                    _application.StatusBar.SetText($"Importación completada: {count} registros.", BoMessageTime.bmt_Medium, BoStatusBarMessageType.smt_Success);
                    _application.MessageBox($"Proceso finalizado.\nRegistros importados: {count}");
                }
                else
                {
                    UpdateStatus("El archivo estaba vacío o no contenía registros válidos.");
                    _application.MessageBox("No se importaron registros.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error en importación", ex);
                UpdateStatus("Error: " + ex.Message);
                _application.MessageBox($"Error Crítico: {ex.Message}");
            }
            finally
            {
                SetUIBusy(false);
            }
        }

        // --- Helpers de UI y Thread Safety ---

        private void CheckFileQueue()
        {
            if (_filePathQueue.TryDequeue(out string filePath))
            {
                SafeUpdateUI(() =>
                {
                    ((EditText)_oForm.Items.Item(TxtArchivoID).Specific).Value = filePath;
                });
            }
        }

        private void SetUIBusy(bool busy)
        {
            SafeUpdateUI(() =>
            {
                _oForm.Items.Item(BtnImportID).Enabled = !busy;
                _oForm.Items.Item(BtnBrowseID).Enabled = !busy;
                _oForm.Items.Item(CmbPeriodoID).Enabled = !busy;
            });
        }

        private void UpdateStatus(string message)
        {
            SafeUpdateUI(() =>
            {
                ((StaticText)_oForm.Items.Item(LblResumenID).Specific).Caption = message;
            });
        }

        /// <summary>
        /// Ejecuta cambios en la UI manejando el Freeze/Unfreeze para evitar parpadeos y errores COM.
        /// </summary>
        private void SafeUpdateUI(Action action)
        {
            try
            {
                if (_oForm != null) _oForm.Freeze(true);
                action();
            }
            catch (Exception ex)
            {
                _logger.Error("Error actualizando UI", ex);
            }
            finally
            {
                if (_oForm != null) _oForm.Freeze(false);
            }
        }

        // --- Wrappers para creación de controles (Reducen ruido visual) ---

        private StaticText AddLabel(string uid, string caption, int left, int top)
        {
            Item item = _oForm.Items.Add(uid, BoFormItemTypes.it_STATIC);
            item.Left = left; item.Top = top; item.Width = 150;
            StaticText lbl = (StaticText)item.Specific;
            lbl.Caption = caption;
            return lbl;
        }

        private EditText AddEditText(string uid, int left, int top, int width)
        {
            Item item = _oForm.Items.Add(uid, BoFormItemTypes.it_EDIT);
            item.Left = left; item.Top = top; item.Width = width;
            return (EditText)item.Specific;
        }

        private SAPbouiCOM.ComboBox AddComboBox(string uid, int left, int top, int width)
        {
            Item item = _oForm.Items.Add(uid, BoFormItemTypes.it_COMBO_BOX);
            item.Left = left; item.Top = top; item.Width = width;
            return (SAPbouiCOM.ComboBox)item.Specific;
        }

        private SAPbouiCOM.Button AddButton(string uid, string caption, int left, int top, int width)
        {
            Item item = _oForm.Items.Add(uid, BoFormItemTypes.it_BUTTON);
            item.Left = left; item.Top = top; item.Width = width;
            SAPbouiCOM.Button btn = (SAPbouiCOM.Button)item.Specific;
            btn.Caption = caption;
            return btn;
        }
    }
}