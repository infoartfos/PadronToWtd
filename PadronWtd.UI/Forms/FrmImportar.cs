using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.DI;
using PadronWtd.UI.Helpers;
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
using SAPbouiCOM;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers; // Necesario para el Timer

namespace PadronWtd.UI.Forms
{
    public class FrmImportar
    {
        private static FrmImportar _instance;
        // --- Constantes de UI ---
        private const string FormUID = "FrmImp";
        private const string CmbPeriodoID = "cmbPeriodo";
        private const string TxtArchivoID = "txtArchivo";
        private const string BtnBrowseID = "btnBrowse";
        private const string BtnImportID = "btnImport";
        private const string BtnReprocessID = "btnReproc";
        private const string LblResumenID = "lblResumen";
        private const string LblProgressID = "lblProgr";
        private const string LblLine1ID = "lblLine1";
        private const string LblLine2ID = "lblLine2";
        private const string LblLine3ID = "lblLine3";

        // --- Dependencias ---
        private readonly SAPbouiCOM.Application _application;
        private readonly ILogger _logger;
        private readonly FileImportService _importService;
        private readonly PeriodosService _periodosService;
        private readonly PSaltaRepository _repository;
        private readonly SAPbobsCOM.Company _company;

        // --- Estado y Colas ---
        private readonly ConcurrentQueue<string> _filePathQueue = new ConcurrentQueue<string>();
        private Form _oForm;
        private ComboBox _cmb;
        private SAPbouiCOM.Button _btnProc;
        private SAPbouiCOM.Button _btnReproc;

        private bool _isDialogOpen = false;
        private readonly object _dialogLock = new object();

        private bool _isProcessOpen = false;
        private readonly object _processLock = new object();

        private bool _isReprocessOpen = false;
        private readonly object _reprocessLock = new object();

        // Timer para revisar la cola de archivos (Solución al problema de foco)
        private System.Timers.Timer _uiTimer;

        // Bandera para evitar doble clic o ejecución simultánea
        private bool _isActionRunning = false;

        private FrmImportar(SAPbouiCOM.Application application)
        {
            _application = application;
            _logger = SimpleServiceProvider.Get<ILogger>();
            try
            {
                _importService = new FileImportService();
            }
            catch (Exception ex)
            {
                _logger.Error("Error al conectarse a la DB", ex);
                _application.MessageBox("Error al conectarse a la DB: " + ex.Message);
                throw ex;
            }
            _periodosService = new PeriodosService();

            _company = App.Company;
            if (_company == null)
            {
                throw new InvalidOperationException("La conexión DI API no está inicializada.");
            }

            _repository = new PSaltaRepository(_company);
        }

        public static void Show(SAPbouiCOM.Application app)
        {
            // Si la instancia no existe, la creamos
            if (_instance == null)
            {
                _instance = new FrmImportar(app);

                // Suscribimos eventos UNA SOLA VEZ al crear la instancia
                _instance._application.ItemEvent += _instance.SBO_Application_ItemEvent;
            }

            // Llamamos a la lógica interna para mostrar/crear la ventana visual
            _instance.EnsureFormVisible();
        }

        // Renombrado de CreateForm a EnsureFormVisible para claridad
        private void EnsureFormVisible()
        {
            try
            {
                // 1. Verificar si la ventana visual ya existe en SAP
                try
                {
                    var existingForm = _application.Forms.Item(FormUID);
                    existingForm.Select(); // Traer al frente
                    return; // Ya existe, no hacemos nada más
                }
                catch { }

                // 2. Si no existe, la dibujamos
                BuildUserInterface();
            }
            catch (Exception ex)
            {
                _logger.Error("Error al abrir formulario", ex);
            }
        }

        public void CreateForm()
        {
            try
            {
                try
                {
                    var existingForm = _application.Forms.Item(FormUID);
                    existingForm.Select();
                    return;
                }
                catch { }

                BuildUserInterface();
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
            _oForm.Height = 420;
            _oForm.Visible = true;
            _oForm.AutoManaged = true;

            int left = 20, top = 30, lblWidth = 150, fieldWidth = 250, spacing = 30;

            // 1. Periodo
            top += spacing;
            AddLabel("lblPer", "Período a Procesar:", left, top);
            _cmb = AddComboBox(CmbPeriodoID, left + lblWidth, top, fieldWidth);

            // 2. Archivo
            top += spacing;
            AddLabel("lblFile", "Archivo a procesar:", left, top);
            AddEditText(TxtArchivoID, left + lblWidth, top, fieldWidth - 60);
            AddButton(BtnBrowseID, "...", left + lblWidth + fieldWidth - 50, top, 40);

            // 3. Botones
            top += spacing * 2;
            this._btnProc = AddButton(BtnImportID, "Importar y Procesar", left + lblWidth, top, 200);
            this._btnProc.Item.Visible = false;

            // top += spacing + 10; En el mismo lugar que el anterior
            _btnReproc = AddButton(BtnReprocessID, "Reprocesar Errores", left + lblWidth, top, 200);
            _btnReproc.Item.Visible = false;

            // 4. Resultados
            top += spacing;
            var lblRes = AddLabel(LblResumenID, "", left, top); lblRes.Item.Width = 450;

            top += spacing;
            var l1 = AddLabel(LblLine1ID, "", left, top); l1.Item.Width = 450;
            top += spacing;
            var l2 = AddLabel(LblLine2ID, "", left, top); l2.Item.Width = 450;
            top += spacing;
            var l3 = AddLabel(LblLine3ID, "", left, top); l3.Item.Width = 450;

            // Carga inicial y arranque del Timer
            _ = LoadPeriodosAsync(_cmb);
            StartQueueTimer();
        }

        // --- TIMER PARA CHECK DE ARCHIVOS ---

        private void StartQueueTimer()
        {
            if (_uiTimer == null)
            {
                _uiTimer = new System.Timers.Timer(500); // Revisar cada 500ms
                _uiTimer.Elapsed += OnTimerElapsed;
                _uiTimer.AutoReset = true;
                _uiTimer.Enabled = true;
            }
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_filePathQueue.IsEmpty)
            {
                CheckFileQueue();
            }
        }

        // --------------------------------------------------------------------------------------------
        // EVENT HANDLER PRINCIPAL
        // --------------------------------------------------------------------------------------------
        private void SBO_Application_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            if (FormUID != FrmImportar.FormUID) return;
            
            if (pVal.EventType == BoEventTypes.et_FORM_CLOSE && !pVal.BeforeAction)
            {
                DisposeInstance();
                return;
            }


            // PROTECCIÓN: Si ya hay una acción corriendo, ignoramos nuevos clics (excepto Browse)
            if (_isActionRunning && pVal.EventType == BoEventTypes.et_ITEM_PRESSED && !pVal.BeforeAction)
            {
                return;
            }

            // Mantenemos esto por redundancia, aunque el Timer hace el trabajo principal
            if (pVal.EventType == BoEventTypes.et_FORM_ACTIVATE)
            {
                CheckFileQueue();
            }

            if (pVal.EventType == BoEventTypes.et_COMBO_SELECT && !pVal.BeforeAction && pVal.ItemUID == CmbPeriodoID)
            {
                HandleComboSelect();
            }

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
                    case BtnReprocessID:
                        HandleReprocessClick();
                        break;
                }
            }
        }

        private void DisposeInstance()
        {
            try
            {
                // Detener Timer
                if (_uiTimer != null)
                {
                    _uiTimer.Stop();
                    _uiTimer.Dispose();
                    _uiTimer = null;
                }

                // Desuscribir eventos para evitar fugas de memoria
                _application.ItemEvent -= SBO_Application_ItemEvent;
            }
            catch { }
            finally
            {
                // Nulificar la instancia estática para permitir crear una nueva luego
                _instance = null;

                // Forzar limpieza de memoria (Opcional, útil si manejas muchos datos)
                GC.Collect();
            }
        }

        // --------------------------------------------------------------------------------------------
        // LÓGICA DE BOTONES (HANDLERS)
        // --------------------------------------------------------------------------------------------

        private void HandleBrowseClick()
        {
            lock (_dialogLock)
            {
                if (_isDialogOpen) return;
                _isDialogOpen = true;     
            }

            var t = new Thread(() =>
            {
                try
                {
                    using (var dialog = new System.Windows.Forms.OpenFileDialog())
                    {
                        dialog.Filter = "Archivos CSV/TXT|*.csv;*.txt|Todos|*.*";
                        dialog.Title = "Seleccionar Padrón";

                        IntPtr sapHandle = GetForegroundWindow();
                        WindowWrapper wrapper = new WindowWrapper(sapHandle);

                        if (dialog.ShowDialog(wrapper) == System.Windows.Forms.DialogResult.OK)
                        {
                            _filePathQueue.Enqueue(dialog.FileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error en FileDialog", ex);
                }
                finally
                {
                    lock (_dialogLock)
                    {
                        _isDialogOpen = false;
                    }
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private void HandleImportClick()
        {
            lock (_processLock)
            {
                if (_isProcessOpen) return;
                _isProcessOpen = true;
            }

            try
            {
                string filePath = ((EditText)_oForm.Items.Item(TxtArchivoID).Specific).Value;
                string valPeriodo = ((SAPbouiCOM.ComboBox)_oForm.Items.Item(CmbPeriodoID).Specific).Value;

                if (string.IsNullOrEmpty(filePath))
                {
                    _application.StatusBar.SetText("Seleccione un archivo.", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
                    return;
                }

                string year = "";
                string qValue = "";
                var parts = valPeriodo.Split(' ');
                if (parts.Length > 1)
                {
                    year = parts[0];
                    qValue = parts[1];
                }

                string msg = $"Se procesará el Padrón:\nAño: {year}\nPeriodo: {qValue}\n\n¿Continuar?";
                if (_application.MessageBox(msg, 1, "Sí", "No") != 1) return;

                _isActionRunning = true;
                _ = RunImportProcessAsync(filePath, year, qValue);
            }
            catch
            {
                _isActionRunning = false;
            }
            finally
            {
                lock (_processLock)
                {
                    _isProcessOpen = false;
                }
            }
        }

        private void HandleReprocessClick()
        {
            lock (_reprocessLock)
            {
                if (_isReprocessOpen) return;
                _isReprocessOpen = true;
            }

            try
            {
                string valPeriodo = _cmb.Value;
                var parts = valPeriodo.Split(' ');
                if (parts.Length < 2) return;

                string year = parts[0];
                string qValue = parts[1];

                if (_application.MessageBox($"¿Reprocesar SOLO errores para {year} - {qValue}?", 1, "Sí", "No") != 1)
                    return;

                _isActionRunning = true;
                _ = RunReprocessAsync(year, qValue);
            }
            catch
            {
                _isActionRunning = false;
            }
            finally
            {
                lock (_reprocessLock)
                {
                    _isReprocessOpen = false;
                }
            }
        }

        // --------------------------------------------------------------------------------------------
        // PROCESOS ASÍNCRONOS
        // --------------------------------------------------------------------------------------------

        private async Task RunImportProcessAsync(string filePath, string year, string qValue)
        {
            try
            {
                SetUIBusy(true);
                UpdateResultLabels("", "", "");
                UpdateStatus("Leyendo y procesando archivo...");

                var importReporter = new Progress<int>(pct =>
                {
                    _application.StatusBar.SetText($"Importando... {pct}%", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);
                });

                int count = await _importService.ProcessImportAsync(filePath, year, qValue, importReporter);

                if (count > 0)
                {
                    UpdateStatus("Importación OK. Iniciando proceso SAP...");
                    await RunSapProcessing(year, qValue);
                }
                else
                {
                    UpdateStatus("El archivo estaba vacío.");
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
                _isActionRunning = false;
            }
        }

        private async Task RunReprocessAsync(string year, string qValue)
        {
            try
            {
                SetUIBusy(true);
                UpdateResultLabels("", "", "");
                UpdateStatus("Reseteando registros con error...");

                if (_repository != null)
                {
                    await _repository.ResetErrorRecordsAsync(qValue, year);
                }

                await RunSapReprocessing(year, qValue);
            }
            catch (Exception ex)
            {
                _logger.Error("Error en reprocesamiento", ex);
                _application.MessageBox("Error: " + ex.Message);
            }
            finally
            {
                SetUIBusy(false);
                _isActionRunning = false;
            }
        }

        private async Task RunSapProcessing(string year, string qValue)
        {
            UpdateStatus("Procesando información en SAP...");

            var service = new ProcessInfoService();
            var progressReporter = new Progress<int>(percent =>
            {
                _application.StatusBar.SetText($"Procesando SAP... {percent}%", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);
            });

            ProcessResult resultado = await service.ProcessRecordsAsync(qValue, year, progressReporter);

            string txtTotal = $"Total: {resultado.TotalRegistros}";
            string txtOk = $"Exitosos: {resultado.ProcesadosExitosos}";
            string txtError = $"Errores: {resultado.RegistrosConError}";

            UpdateResultLabels(txtTotal, txtOk, txtError);

            await CheckErrorsAndToggleBtnAsync(year, qValue);
            _ = LoadPeriodosAsync(_cmb);

            _application.MessageBox($"Proceso Finalizado.\n{txtTotal}\n{txtOk}\n{txtError}");
        }

        private async Task RunSapReprocessing(string year, string qValue)
        {
            UpdateStatus("Procesando información en SAP...");

            var service = new ProcessInfoService();
            var progressReporter = new Progress<int>(percent =>
            {
                _application.StatusBar.SetText($"Reprocesando SAP... {percent}%", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);
            });

            ProcessResult resultado = await service.ProcessRecordsAsync(qValue, year, progressReporter);

            string txtTotal = $"Exitosos: {resultado.TotalRegistros}";
            string txtOk = $"Procesados: {(-1) * resultado.ProcesadosExitosos}";
            string txtError = $"Errores: {resultado.RegistrosConError}";

            UpdateResultLabels(txtTotal, txtOk, txtError);

            await CheckErrorsAndToggleBtnAsync(year, qValue);
            _ = LoadPeriodosAsync(_cmb);

            _application.MessageBox($"Proceso Finalizado.\n{txtTotal}\n{txtOk}\n{txtError}");
        }


        // --------------------------------------------------------------------------------------------
        // HELPERS
        // --------------------------------------------------------------------------------------------

        private async Task LoadPeriodosAsync(SAPbouiCOM.ComboBox cmb)
        {
            try
            {
                var periodos = await _periodosService.GetActivePeriodosAsync();
                if (periodos.Count == 0) return;

                SafeUpdateUI(() =>
                {
                    while (cmb.ValidValues.Count > 0)
                    {
                        try { cmb.ValidValues.Remove(0, BoSearchKey.psk_Index); } catch { break; }
                    }

                    foreach (var p in periodos)
                    {
                        try { cmb.ValidValues.Add(p.Value, p.Description); } catch { }
                    }

                    if (cmb.ValidValues.Count > 0)
                    {
                        try { cmb.Select(0, BoSearchKey.psk_Index); HandleComboSelect(); } catch { }
                    }
                });
            }
            catch (Exception ex) { _logger.Error("Error cargando periodos", ex); }
        }

        private void HandleComboSelect()
        {
            try
            {
                string valPeriodo = _cmb.Value;
                if (string.IsNullOrEmpty(valPeriodo)) return;

                var parts = valPeriodo.Split(' ');
                if (parts.Length > 1)
                {
                    _ = CheckErrorsAndToggleBtnAsync(parts[0], parts[1]);
                }
            }
            catch { }
        }

        private async Task CheckErrorsAndToggleBtnAsync(string year, string qValue)
        {
            try
            {
                if (_repository == null) return;
                int errors = await _repository.CountErrorsAsync(qValue, year);
                changedPeriodo(errors);
            }
            catch (Exception ex) { _logger.Error("Error chequeando errores", ex); }
        }

        private void changedPeriodo(int errors)
        {
            SafeUpdateUI(() =>
            {
                try
                {
                    _oForm.Items.Item(BtnReprocessID).Visible = (errors > 0);

                    _oForm.Items.Item(BtnImportID).Visible = (errors == 0);
                    _oForm.Items.Item(BtnBrowseID).Visible = (errors == 0);
                    _oForm.Items.Item(TxtArchivoID).Visible = (errors == 0);
                    _oForm.Items.Item("lblFile").Visible = (errors == 0);
                }
                catch
                {
                }
            });
        }



        private void CheckFileQueue()
        {
            if (_filePathQueue.TryDequeue(out string filePath))
            {
                SafeUpdateUI(() =>
                {
                    try { ((EditText)_oForm.Items.Item(TxtArchivoID).Specific).Value = filePath; } catch { }
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
                _oForm.Items.Item(BtnReprocessID).Enabled = !busy;
            });
        }

        private void UpdateStatus(string message)
        {
            SafeUpdateUI(() => ((StaticText)_oForm.Items.Item(LblResumenID).Specific).Caption = message);
        }

        private void UpdateResultLabels(string t1, string t2, string t3, string caption = "Última corrida:")
        {
            SafeUpdateUI(() =>
            {
                ((StaticText)_oForm.Items.Item(LblLine1ID).Specific).Caption = t1;
                ((StaticText)_oForm.Items.Item(LblLine2ID).Specific).Caption = t2;
                ((StaticText)_oForm.Items.Item(LblLine3ID).Specific).Caption = t3;
                ((StaticText)_oForm.Items.Item(LblResumenID).Specific).Caption = caption;
            });
        }

        private void SafeUpdateUI(Action action)
        {
            try
            {
                if (_oForm != null) _oForm.Freeze(true);
                action();
            }
            catch (Exception ex) { _logger.Error("Error UI", ex); }
            finally { if (_oForm != null) _oForm.Freeze(false); }
        }

        // --- WRAPPERS UI ---
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

        // --- WIN32 ---
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private class WindowWrapper : System.Windows.Forms.IWin32Window
        {
            private readonly IntPtr _hwnd;
            public WindowWrapper(IntPtr handle) { _hwnd = handle; }
            public IntPtr Handle => _hwnd;
        }
    }
}