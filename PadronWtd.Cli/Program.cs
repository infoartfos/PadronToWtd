using Microsoft.Extensions.DependencyInjection;
using PadronWtd.Application.Interfaces;
using PadronWtd.Application.Services;
using PadronWtd.Infrastructure.Repositories;
using PadronWtd.Infrastructure.Services;
using PadronWtd.Infrastructure.Utils;

var services = new ServiceCollection();
services.AddSingleton<IPadronRepository, InMemoryPadronRepository>();
services.AddSingleton<IRunRepository, InMemoryRunRepository>();
services.AddSingleton<ITaxRepository, InMemoryTaxRepository>();
services.AddSingleton<IProcessLogRepository, InMemoryProcessLogRepository>();
services.AddSingleton<ISnRepository, DummySnRepository>();
services.AddSingleton<IWtdService, DummyWtdService>();
services.AddSingleton<ICsvImporter, CsvImporter>();
services.AddSingleton<ImportPadronUseCase>();
services.AddSingleton<ProcessRunUseCase>();

var provider = services.BuildServiceProvider();

if (args.Length == 0)
{
    Console.WriteLine("Utilidad Padrón Salta V" + "0.20");
    Console.WriteLine("---------------------");
    Console.WriteLine("");
    Console.WriteLine("Comandos disponibles:");
    Console.WriteLine(" import <archivo.tsv>");
    Console.WriteLine(" process <runId>");
    return;
}

var command = args[0];
switch (command)
{
    case "import":
        var file = args.ElementAtOrDefault(1);
        if (string.IsNullOrEmpty(file)) { Console.WriteLine("Falta archivo."); return; }
        await provider.GetRequiredService<ImportPadronUseCase>().ExecuteAsync(file, 1, "demo");
        break;
    case "process":
        var runId = int.TryParse(args.ElementAtOrDefault(1), out var id) ? id : 1;
        await provider.GetRequiredService<ProcessRunUseCase>().ExecuteAsync(runId, "demo");
        break;
    default:
        Console.WriteLine("Comando no reconocido.");
        break;
}



//Console.WriteLine("Utilidad Padrón Salta V" + "0.20");
//Console.WriteLine("---------------------");
//Console.WriteLine("");
//Console.WriteLine("Corriendo...");
