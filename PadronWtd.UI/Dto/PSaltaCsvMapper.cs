using System;
using System.Collections.Generic;
using System.IO;

public static class PSaltaCsvMapper
{
    private static readonly string[] Header =
    {
        "Code","Name","DocEntry","Canceled","Object","LogInst","UserSign","Transfered",
        "CreateDate","CreateTime","UpdateDate","UpdateTime","DataSource",
        "U_Anio","U_Padron","U_Cuit","U_Inscripcion","U_Riesgo","U_Notas","U_Procesado"
    };

    public static List<PSaltaRecord> ReadCsv(string path)
    {
        var list = new List<PSaltaRecord>();

        using (var reader = new StreamReader(path))
        {
            string header = reader.ReadLine(); // skip header

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(';');

                var r = new PSaltaRecord
                {
                    Code = cols[0],
                    Name = cols[1],
                    DocEntry = ParseInt(cols[2]),
                    Canceled = cols[3],
                    Object = cols[4],
                    LogInst = ParseInt(cols[5]),
                    UserSign = ParseInt(cols[6]),
                    Transfered = cols[7],
                    CreateDate = cols[8],
                    CreateTime = cols[9],
                    UpdateDate = cols[10],
                    UpdateTime = cols[11],
                    DataSource = cols[12],
                    U_Anio = cols[13],
                    U_Padron = cols[14],
                    U_Cuit = cols[15],
                    U_Inscripcion = cols[16],
                    U_Riesgo = cols[17],
                    U_Notas = cols[18],
                    U_Procesado = cols[19]
                };

                list.Add(r);
            }
        }

        return list;
    }

    public static void WriteCsv(string path, IEnumerable<PSaltaRecord> records)
    {
        using (var writer = new StreamWriter(path, false))
        {
            writer.WriteLine(string.Join(";", Header));

            foreach (var r in records)
            {
                writer.WriteLine(string.Join(";", new[]
                {
                    r.Code, r.Name, r.DocEntry?.ToString() ?? "",
                    r.Canceled, r.Object, r.LogInst?.ToString() ?? "",
                    r.UserSign?.ToString() ?? "", r.Transfered,
                    r.CreateDate, r.CreateTime, r.UpdateDate, r.UpdateTime,
                    r.DataSource,
                    r.U_Anio, r.U_Padron, r.U_Cuit, r.U_Inscripcion,
                    r.U_Riesgo, r.U_Notas, r.U_Procesado
                }));
            }
        }
    }

    private static int? ParseInt(string s)
    {
        int v;
        return int.TryParse(s, out v) ? (int?)v : null;
    }
}
