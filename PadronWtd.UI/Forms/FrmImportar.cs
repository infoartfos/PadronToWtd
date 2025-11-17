using PadronSaltaAddOn.UI.DI;
using PadronSaltaAddOn.UI.Logging;
using PadronSaltaAddOn.UI.Services;
using PadronWtd.UI.Services;
using SAPbobsCOM;
using SAPbouiCOM;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        private StaticText lblProgress; // etiqueta para progreso
        private readonly IImportService _importService;
        private readonly ILogger _logger;
        private CancellationTokenSource _cts;

        public FrmImportar(SAPbouiCOM.Application application)
        {
            SBO_Application = application;

            // obtener servicios desde provider simples (asegúrate de registrar antes)
            _logger = SimpleServiceProvider.Get<ILogger>();
            _importService = SimpleServiceProvider.Get<IImportService>();
        }

        public void CreateForm()
        {
            FormCreationParams creationPackage =
                (FormCreationParams)SBO_Application.CreateObject(BoCreatableObjectType.cot_FormCreationParams);

            creationPackage.UniqueID = "FrmImp";
            creationPackage.FormType = "FrmImp";
            creationPackage.BorderStyle = BoFormBorderStyle.fbs_Sizable;

            oForm = SBO_Application.Forms.AddEx(creationPackage);
            oForm.Title = "Importar y Procesar";
            oForm.Width = 520;
            oForm.Height = 380;

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

            // Progress label
            top += spacing + 10;
            lblProgress = AddLabel("lblProgr", "Progreso: 0%", left, top);
            lblProgress.Item.Width = 450;

            // Eventos
            SBO_Application.ItemEvent += SBO_Application_ItemEvent;

            oForm.Visible = true;
        }

        private void SBO_Application_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            // Procesar cola justo al activar el form
            if (FormUID == "FrmImp" && pVal.EventType == BoEventTypes.et_FORM_ACTIVATE)
            {
                ProcesarColaDeArchivos();
            }

            if (FormUID != "FrmImp" || !pVal.BeforeAction)
                return;

            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && pVal.ItemUID == "btnImport")
            {
                BubbleEvent = false;
                // iniciar proceso asincrónico
                _ = Task.Run(() => StartImportAsync());
            }

            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && pVal.ItemUID == "btnBrowse")
            {
                BubbleEvent = false;
                SeleccionarArchivo();
            }
        }

        private void ProcesarColaDeArchivos()
        {
            if (_filePathQueue.TryDequeue(out string filePath))
            {
                try
                {
                    _logger.Info("Procesando archivo desde cola: " + filePath);
                    oForm.Freeze(true);
                    EditText field = (EditText)oForm.Items.Item("txtArchivo").Specific;
                    field.Value = filePath;
                }
                catch (Exception ex)
                {
                    _logger.Error("Error al actualizar campo txtArchivo", ex);
                    SBO_Application.StatusBar.SetText("Error al actualizar ruta del archivo.", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
                }
                finally
                {
                    oForm.Freeze(false);
                }
            }

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

        private void SeleccionarArchivo()
        {
            var t = new System.Threading.Thread(() =>
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
                            _filePathQueue.Enqueue(dialog.FileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error en hilo de diálogo", ex);
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        // Inicia la importación (orquesta, async)
        private async Task StartImportAsync()
        {
            try
            {
                // Leer path actual del campo (en el hilo del add-on, acceder con caution)
                string path;
                try
                {
                    oForm.Freeze(true);
                    path = ((EditText)oForm.Items.Item("txtArchivo").Specific).Value?.Trim() ?? "";
                }
                finally
                {
                    try { oForm.Freeze(false); } catch { }
                }

                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    SBO_Application.MessageBox("Debe seleccionar un archivo válido.");
                    return;
                }

                // Preparar cancellation token
                _cts = new CancellationTokenSource();

                // Deshabilitar botón mientras procesa
                SetButtonEnabled("btnImport", false);
                SetButtonEnabled("btnBrowse", false);

                var progress = new Progress<int>(pct =>
                {
                    UpdateProgressLabel(pct);
                    SBO_Application.StatusBar.SetText($"Importando... {pct}%", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);
                });

                // Llamada al servicio con un onBatch simple que guarda (simulado)
                await _importService.ImportFileAsync(
                    path,
                    progress,
                    _cts.Token,
                    async (batch) =>
                    {

                        this.importar_rows(path);

                        _logger.Info($"Persistiendo lote de {batch?.AsListOrCount() ?? 0} lineas...");
                        
                    });

                // Al finalizar
                UpdateSummary("Importación finalizada correctamente.");
                SBO_Application.StatusBar.SetText("Importación finalizada correctamente.", BoMessageTime.bmt_Medium, BoStatusBarMessageType.smt_Success);
            }
            catch (OperationCanceledException)
            {
                UpdateSummary("Importación cancelada.");
                SBO_Application.StatusBar.SetText("Importación cancelada.", BoMessageTime.bmt_Medium, BoStatusBarMessageType.smt_Warning);
            }
            catch (Exception ex)
            {
                _logger.Error("Error en StartImportAsync", ex);
                UpdateSummary("Error durante la importación: " + ex.Message);
                SBO_Application.StatusBar.SetText("Error durante la importación.", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
            }
            finally
            {
                SetButtonEnabled("btnImport", true);
                SetButtonEnabled("btnBrowse", true);
                _cts?.Dispose();
                _cts = null;
            }
        }


    private void importar_rows(string filePath)
    {

        var csv = new CsvImportService();
        var rows = csv.ReadCsv(filePath);

        var company = SapConnectionFactory.CreateCompany();
        var repo = new SapPadronService(company);

        int ok = 0, error = 0;

        foreach (var row in rows)
        {
            string err;
            int result = repo.Insert(row, out err);

            if (result == 1)
                ok++;
            else
            {
                error++;
                //Application.SBO_Application.SetStatusBarMessage(
                //    $"Error insertando {row.Cuit}: {err}",
                //    SAPbouiCOM.BoMessageTime.bmt_Short,
                //    true
                //);
            }
        }

        MessageBox.Show($"Importación completa.\nCorrectos: {ok}\nErrores: {error}");
    }





    private void UpdateProgressLabel(int pct)
        {
            try
            {
                oForm.Freeze(true);
                ((StaticText)oForm.Items.Item("lblProgr").Specific).Caption = $"Progreso: {pct}%";
            }
            catch { }
            finally
            {
                try { oForm.Freeze(false); } catch { }
            }
        }

        private void UpdateSummary(string text)
        {
            try
            {
                oForm.Freeze(true);
                ((StaticText)oForm.Items.Item("lblResumen").Specific).Caption = text;
            }
            catch { }
            finally
            {
                try { oForm.Freeze(false); } catch { }
            }
        }

        private void SetButtonEnabled(string btnId, bool enabled)
        {
            try
            {
                oForm.Freeze(true);
                var btn = (SAPbouiCOM.Button)oForm.Items.Item(btnId).Specific;
                btn.Item.Enabled = enabled;
            }
            catch { }
            finally
            {
                try { oForm.Freeze(false); } catch { }
            }
        }
    }

    // Helper extension local
    internal static class EnumerableHelpers
    {
        public static int AsListOrCount<T>(this IEnumerable<T> e)
        {
            if (e == null) return 0;
            if (e is System.Collections.ICollection c) return c.Count;
            return e is System.Collections.Generic.ICollection<T> col ? col.Count : new List<T>(e).Count;
        }
    }
}

