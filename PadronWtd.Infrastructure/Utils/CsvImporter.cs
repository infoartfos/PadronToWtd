using PadronWtd.Application.Interfaces;
using PadronWtd.Domain.Entities;

namespace PadronWtd.Infrastructure.Utils;

public class CsvImporter : ICsvImporter
{
    private readonly IPadronRepository _repo;
    public CsvImporter(IPadronRepository repo) => _repo = repo;

    public async Task<int> ImportAsync(string path, int runId, string user)
    {
        var entries = new List<PadronEntry>();
        int line = 0;
        foreach (var l in File.ReadLines(path))
        {
            line++;
            if (string.IsNullOrWhiteSpace(l)) continue;
            var cols = l.Split('\t');
            if (cols.Length < 4) continue;
            entries.Add(new PadronEntry
            {
                RunId = runId,
                LineNumber = line,
                CUIT = cols[0].Trim(),
                Denominacion = cols[1].Trim(),
                ActividadEconomica = cols[2].Trim(),
                NivelRiesgo = cols[3].Trim()
            });
        }
        await _repo.BulkInsertAsync(entries, user);
        return entries.Count;
    }
}
