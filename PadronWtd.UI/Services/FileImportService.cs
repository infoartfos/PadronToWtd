using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PadronWtd.UI.Services
{
    public class FileImportService
    {
        private readonly ILogger _logger;

        public FileImportService()
        {
            _logger = SimpleServiceProvider.Get<ILogger>();
        }

        public async Task<int> ProcessImportAsync(string filePath, string year, string qValue, IProgress<int> progress = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("El archivo especificado no existe.", filePath);

            if (App.Company == null || !App.Company.Connected)
                throw new InvalidOperationException("No hay conexión activa con DI API (App.Company es nulo).");

            List<PSaltaRecord> recordsToInsert = await Task.Run(() => ParseFile(filePath, year, qValue));

            if (recordsToInsert.Count == 0)
                return 0;

            progress.Report(0);
            var repository = new PSaltaRepository(App.Company);
            _logger.Info("Borrando registros anteriores " + qValue + " " + year);
            await repository.DeleteByAnioAndQAsync(qValue, year);
            _logger.Info("terminó borrado registros anteriores ");
            await repository.BulkInsertAsync(recordsToInsert, progress);
            progress.Report(100);
            return recordsToInsert.Count;
            
        }

        private List<PSaltaRecord> ParseFile(string path, string year, string qValue)
        {
            var list = new List<PSaltaRecord>();
            var lines = File.ReadAllLines(path);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split('\t'); 

                // Ignorar encabezados
                if (cols.Length > 0 && cols[0].Trim().ToUpper().StartsWith("CUIT")) continue;

                string cuit = cols.Length > 0 ? cols[0].Trim() : "";

                if (string.IsNullOrEmpty(cuit)) continue;

                string inscripcion = cols.Length > 2 ? cols[2].Trim() : "";
                string riesgo = cols.Length > 3 ? cols[3].Trim() : "";

                list.Add(new PSaltaRecord
                {
                    Code = SequentialId.Generate(),
                    Name = qValue,
                    U_Anio = year,
                    U_Padron = line, 
                    U_Cuit = cuit,
                    U_Riesgo = riesgo,
                    U_Inscripcion = inscripcion,
                    U_Estado = "Importado",
                    U_Notas = ""
                });
            }
            return list;
        }
    }
}