using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
using PadronWtd.UI.SL;
using SAPbouiCOM;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PadronWtd.UI.Forms
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
        private StaticText lblProgress;
        private readonly IImportService _importService;
        private readonly ILogger _logger;
        private readonly ServiceLayerClient _sl;
        private CancellationTokenSource _cts;
        private string q_value = "Q8";
        private string year = string.Empty;

        public FrmImportar(SAPbouiCOM.Application application)
        {
            SBO_Application = application;
            _logger = SimpleServiceProvider.Get<ILogger>();

            _sl = new ServiceLayerClient("https://contreras-hanadb.sbo.contreras.com.ar:50000/b1s/v1/");
            _importService = new FrmImportarService(application, _sl);
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

            AddLabel("lblId", "ID:", left, top);
            txtId = AddEditText("txtId", left + lblWidth, top, fieldWidth);
            txtId.Value = "1";

            top += spacing;
            AddLabel("lblPeriodo", "Período a Procesar:", left, top);
            cmbPeriodo = AddComboBox("cmbPeriodo", left + lblWidth, top, fieldWidth);
            cmbPeriodo.ValidValues.Add("1", "Q1 2025");
            cmbPeriodo.ValidValues.Add("2", "Q2 2025");
            cmbPeriodo.ValidValues.Add("3", "Q3 2025");
            cmbPeriodo.ValidValues.Add("4", "Q4 2025");
            cmbPeriodo.Select("1", BoSearchKey.psk_ByValue);

            top += spacing;
            AddLabel("lblArchivo", "Archivo a procesar:", left, top);
            txtArchivo = AddEditText("txtArchivo", left + lblWidth, top, fieldWidth - 60);
            btnBrowse = AddButton("btnBrowse", "...", left + lblWidth + fieldWidth - 50, top, 40);

            top += spacing * 2;
            btnImport = AddButton("btnImport", "Importar y Procesar", left + lblWidth, top, 200);

            top += spacing * 2;
            lblResumen = AddLabel("lblResumen", "", left, top);
            lblResumen.Item.Width = 450;

            top += spacing + 10;
            lblProgress = AddLabel("lblProgr", "Progreso: 0%", left, top);
            lblProgress.Item.Width = 450;

            SBO_Application.ItemEvent += SBO_Application_ItemEvent;
            oForm.Visible = true;
        }

        private void SBO_Application_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            if (FormUID == "FrmImp" && pVal.EventType == BoEventTypes.et_FORM_ACTIVATE)
            {
                ProcesarColaDeArchivos();
            }

            if (FormUID != "FrmImp" || !pVal.BeforeAction)
                return;
            q_value = "Q1";
            year = "2025";
            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && pVal.ItemUID == "btnImport")
            {
                BubbleEvent = false;
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
            var t = new Thread(() =>
            {
                try
                {
                    using (var dialog = new System.Windows.Forms.OpenFileDialog())
                    {
                        dialog.Filter = "Archivos CSV|*.csv|Todos los archivos|*.*";
                        dialog.Title = "Seleccionar archivo de padrón";
                        dialog.RestoreDirectory = true;

                        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
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

        private async Task StartImportAsync()
        {
            try
            {
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

                _cts = new CancellationTokenSource();

                SetButtonEnabled("btnImport", false);
                SetButtonEnabled("btnBrowse", false);

                var progress = new Progress<int>(pct =>
                {
                    UpdateProgressLabel(pct);
                    SBO_Application.StatusBar.SetText($"Importando... {pct}%", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);
                });

                // Crear SL una sola vez por importación
                //using (var sl = new ServiceLayerClient(SL_BASE_URL, SL_USER, SL_PASS, SL_COMPANY, _logger))
                //using (var sl = new ServiceLayerClient())
        
                var sl = new ServiceLayerClient("https://contreras-hanadb.sbo.contreras.com.ar:50000/b1s/v1/");
                {
                    try
                    {
        //                private readonly string baseUrl = "https://contreras-hanadb.sbo.contreras.com.ar:50000/b1s/v1/";
        //private readonly string user = "gschneider";
        //private readonly string pass = "TzLt3#MA";
        //private readonly string company = "SBP_SIOC_CHAR";


                await sl.LoginAsync("gschneider", "TzLt3#MA", "SBP_SIOC_CHAR").ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error conectando SL: ", ex);
                        throw;
                    }

                    var psaltaService = new SapPSaltaService(sl);

                    // callback para el import service: procesa lote usando la misma instancia SL
                    Func<IEnumerable<string>, Task> onBatch = async (batch) =>
                    {
                        _logger.Info($"Persistiendo lote de {batch?.AsListOrCount() ?? 0} lineas...");
                        foreach (var line in batch)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            // parsear columnas (adaptar separador)
                            var cols = line.Split('\t');
                            if (cols[0].Trim() == "CUIT") continue;

                            var tmp_Inscripcion = cols.Length > 3 ? cols[3].Trim() : "";
                            var tmp_Riesgo = cols.Length > 2 ? cols[2].Trim() : "";

                            var dto = new PSaltaDto
                            {
                                Code = SequentialId.Generate(), // Guid.NewGuid().ToString("N"),
                                Name = q_value,
                                U_Anio = year.Length > 0 ?  year : "--",
                                U_Padron = line.Length > 0 ? line : "--",
                                U_Cuit = cols.Length > 1 ? cols[0].Trim() : "",
                                U_Inscripcion = tmp_Inscripcion,
                                U_Riesgo = tmp_Riesgo,
                                U_Notas = "",
                                U_Procesado = ""
                            };

                            try
                            {
                                await psaltaService.InsertAsync(dto).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Error insertando fila CSV: {line}", ex);
                                throw;
                            }
                        }
                    };

                    // ejecutar import
                    await _importService.ImportFileAsync(path, progress, _cts.Token, onBatch).ConfigureAwait(false);
                }

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
