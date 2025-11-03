using PadronWtd.Application.Interfaces;
using PadronWtd.Domain.Entities;

namespace PadronWtd.Application.Services;

public class ProcessRunUseCase
{
    private readonly IPadronRepository _padronRepo;
    private readonly IRunRepository _runRepo;
    private readonly ITaxRepository _taxRepo;
    private readonly ISnRepository _snRepo;
    private readonly IWtdService _wtdService;
    private readonly IProcessLogRepository _logRepo;

    public ProcessRunUseCase(IPadronRepository padronRepo, IRunRepository runRepo, ITaxRepository taxRepo,
        ISnRepository snRepo, IWtdService wtdService, IProcessLogRepository logRepo)
    {
        _padronRepo = padronRepo;
        _runRepo = runRepo;
        _taxRepo = taxRepo;
        _snRepo = snRepo;
        _wtdService = wtdService;
        _logRepo = logRepo;
    }

    public async Task ExecuteAsync(int runId, string user)
    {
        var run = await _runRepo.GetActiveAsync();
        if (run == null || run.Id != runId)
            throw new InvalidOperationException("No existe ejecución activa o no coincide el ID");

        var taxes = await _taxRepo.GetAllAsync();
        var entries = await _padronRepo.GetByRunAsync(runId);

        int ok = 0, fail = 0;
        foreach (var entry in entries)
        {
            var sn = await _snRepo.FindByCuitAsync(entry.CUIT);
            if (sn == null)
            {
                await _logRepo.AddAsync(new ProcessLog
                {
                    RunId = runId,
                    CUIT = entry.CUIT,
                    Updated = false,
                    Details = "SN no encontrado"
                });
                fail++;
                continue;
            }

            foreach (var tax in taxes)
                await _wtdService.InsertWtd3Async(entry.CUIT, tax.WtCode);

            await _logRepo.AddAsync(new ProcessLog
            {
                RunId = runId,
                CUIT = entry.CUIT,
                CardCode = sn.Value.CardCode,
                CardName = sn.Value.CardName,
                Updated = true
            });
            ok++;
        }

        Console.WriteLine($"Procesamiento finalizado. Actualizados: {ok}, No encontrados: {fail}");
    }
}
