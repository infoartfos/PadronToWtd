using PadronWtd.UI.Logging;
using PadronWtd.UI.SL;
using SAPbouiCOM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PadronWtd.UI.Services
{
    public class ServiceLayerAuthException : Exception
    {
        public ServiceLayerAuthException(string msg) : base(msg) { }
    }
    public class ImportResult
    {
        public int Ok { get; set; }
        public int Errores { get; set; }
    }
    internal class FrmImportarService : IImportService
    {
        private readonly ILogger _logger = null;
        private readonly int _batchSize = 100;
        private readonly ServiceLayerClient _sl;

        private Application _app;  // puede ser null en debug

        public FrmImportarService(Application app, ServiceLayerClient s1)
        {
            _app = app;
            _sl = s1;
        }


        //public FrmImportarService()
        //{
        //    _sl = new ServiceLayerClient();
        //}

        public async Task<ImportResult> ImportarAsync(string csvPath, Action<string> log)
        {

            log("Iniciando login contra Service Layer...");

            await _sl.LoginAsync("gschneider", "TzLt3#MA", "SBP_SIOC_CHAR");

            log("Login OK.");

            var result = new ImportResult();

            var lineas = File.ReadAllLines(csvPath);
            log($"Archivo leído. {lineas.Length} líneas.");

            foreach (var linea in lineas)
            {
                log($"Procesando: {linea}");

                bool ok = await EjecutarConReintentosAsync(
                    async () =>
                    {
                        // TODO: Parseo real
                        var json = new
                        {
                            Name = linea,
                            Code = linea.GetHashCode().ToString()
                        };
                        await _sl.PostAsync("/Items", json);
                        return true;
                    },
                    log
                );

                if (ok)
                    result.Ok++;
                else
                    result.Errores++;
            }

            return result;
        }

        private async Task<bool> EjecutarConReintentosAsync(Func<Task<bool>> action, Action<string> log)
        {
            int intentos = 0;

            while (true)
            {
                try
                {
                    intentos++;
                    return await action();
                }
                catch (ServiceLayerAuthException)
                {
                    log("Token expirado. Reintentando login...");
                    await _sl.LoginAsync("gschneider", "TzLt3#MA", "SBP_SIOC_CHAR");
                }
                catch (Exception ex)
                {
                    if (intentos >= 3)
                    {
                        log("ERROR permanente: " + ex.Message);
                        return false;
                    }

                    log($"Error transitorio: {ex.Message}. Reintentando {intentos}/3...");
                    await Task.Delay(1000);
                }
            }
        }

        public async Task ImportFileAsync(
            string filePath,
            IProgress<int> progress,
            CancellationToken cancellationToken,
            Func<IEnumerable<string>, Task> onBatchAsync = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            _logger.Info($"Iniciando importación del archivo: {filePath}");

            // Abrimos el archivo y contamos líneas para calcular progreso
            long totalLines = 0;
            using (var sr = new StreamReader(filePath))
            {
                while (await sr.ReadLineAsync().ConfigureAwait(false) != null)
                {
                    totalLines++;
                }
            }

            if (totalLines == 0)
            {
                _logger.Warn("Archivo sin líneas.");
                progress?.Report(100);
                return;
            }

            long processed = 0;
            var batch = new List<string>(_batchSize);

            using (var sr = new StreamReader(filePath))
            {
                string line;
                while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // podés aplicar validaciones / transformaciones aquí
                    batch.Add(line);

                    if (batch.Count >= _batchSize)
                    {
                        // persistir lote
                        if (onBatchAsync != null)
                        {
                            try
                            {
                                await onBatchAsync(batch).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error("Error en onBatchAsync", ex);
                                throw;
                            }
                        }

                        processed += batch.Count;
                        batch.Clear();

                        int pct = (int)((processed * 100L) / totalLines);
                        progress?.Report(Math.Min(100, pct));
                    }
                }

                // último batch
                if (batch.Count > 0)
                {
                    if (onBatchAsync != null)
                    {
                        try
                        {
                            await onBatchAsync(batch).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Error en onBatchAsync (último lote)", ex);
                            throw;
                        }
                    }

                    processed += batch.Count;
                    int pct = (int)((processed * 100L) / totalLines);
                    progress?.Report(Math.Min(100, pct));
                }
            }

            // finalizar
            progress?.Report(100);
            _logger.Info($"Importación finalizada. Total líneas: {totalLines}");
        }
    }


}
