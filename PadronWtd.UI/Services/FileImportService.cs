using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.DI; // Para acceder a App.Company
using PadronWtd.UI.Logging;

namespace PadronWtd.UI.Services
{
    public class FileImportService
    {
        private readonly ILogger _logger;

        public FileImportService()
        {
            _logger = SimpleServiceProvider.Get<ILogger>();
        }

        public async Task<int> ProcessImportAsync(string filePath, string year, string qValue)
        {
            // 1. Validaciones previas
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("El archivo especificado no existe.", filePath);

            if (App.Company == null || !App.Company.Connected)
                throw new InvalidOperationException("No hay conexión activa con DI API (App.Company es nulo).");

            // 2. Lectura y Parsing (CPU Bound)
            List<PSaltaRecord> recordsToInsert = await Task.Run(() => ParseFile(filePath, year, qValue));

            if (recordsToInsert.Count == 0)
                return 0;

            // 3. Persistencia (IO Bound)
            var repository = new PSaltaRepository(App.Company);
            await repository.DeleteByAnioAndQAsync(qValue, year);

            //await repository.BulkInsertAsync(recordsToInsert);
            //return recordsToInsert.Count;
            return 5;
        }

        private List<PSaltaRecord> ParseFile(string path, string year, string qValue)
        {
            var list = new List<PSaltaRecord>();
            var lines = File.ReadAllLines(path);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split('\t'); // Asumimos tabulador como separador

                // Ignorar encabezados
                if (cols.Length > 0 && cols[0].Trim().ToUpper().StartsWith("CUIT")) continue;

                string cuit = cols.Length > 0 ? cols[0].Trim() : "";

                // Validación mínima de datos
                if (string.IsNullOrEmpty(cuit)) continue;

                // Mapeo seguro
                string riesgo = cols.Length > 2 ? cols[2].Trim() : "";
                string inscripcion = cols.Length > 3 ? cols[3].Trim() : "";

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