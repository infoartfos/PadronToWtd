using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PadronSaltaAddOn.UI.Services
{
    public interface IImportService
    {
        /// <summary>
        /// Importa el archivo CSV. Reporta progreso en porcentaje (0-100).
        /// onBatchAsync: callback que recibe un lote de líneas para persistir (puede ser sync o async).
        /// </summary>
        Task ImportFileAsync(
            string filePath,
            IProgress<int> progress,
            CancellationToken cancellationToken,
            Func<IEnumerable<string>, Task> onBatchAsync = null);
    }
}
