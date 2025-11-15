using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PadronWtd.UI.Logging;

namespace PadronWtd.UI.Services
{
    internal class FrmImportarService : IImportService
    {
        private readonly ILogger _logger;
        private readonly int _batchSize;

        public FrmImportarService(ILogger logger, int batchSize = 500)
        {
            _logger = logger;
            _batchSize = batchSize;
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
