using PadronWtd.Application.Interfaces;
using PadronWtd.Domain.Entities;

namespace PadronWtd.Application.Services;

public class ImportPadronUseCase
{
    private readonly ICsvImporter _importer;
    public ImportPadronUseCase(ICsvImporter importer) => _importer = importer;

    public async Task ExecuteAsync(string path, int runId, string user)
    {
        Console.WriteLine($"Importando archivo {path}...");
        var total = await _importer.ImportAsync(path, runId, user);
        Console.WriteLine($"Importación completada. Total líneas: {total}");
    }
}
