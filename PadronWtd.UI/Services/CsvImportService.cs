using PadronWtd.UI.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace PadronWtd.UI.Services
{
    public class CsvImportService
    {
        public List<P_SaltaCsvRow> ReadCsv(string filePath)
        {
            var list = new List<P_SaltaCsvRow>();

            foreach (var line in File.ReadAllLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(';'); // o ',' según tu archivo

                if (parts.Length < 5) continue;

                list.Add(new P_SaltaCsvRow
                {
                    Anio = parts[0].Trim(),
                    Padron = parts[1].Trim(),
                    Cuit = parts[2].Trim(),
                    Inscripcion = parts[3].Trim(),
                    Riesgo = parts[4].Trim(),
                    Notas = parts.Length > 5 ? parts[5].Trim() : null
                });
            }

            return list;
        }
    }
}
