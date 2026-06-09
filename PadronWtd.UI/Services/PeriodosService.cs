using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PadronWtd.UI.Services
{
    public class PeriodosService : IPeriodosService
    {
        private readonly IContDateRepository _repository;
        private readonly IPSaltaRepository _padronRepository;
        private readonly ILogger _logger;

        public PeriodosService(IContDateRepository repository, IPSaltaRepository padronRepository, ILogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _padronRepository = padronRepository ?? throw new ArgumentNullException(nameof(padronRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public PeriodosService(bool forceServiceUser = true)
        {
            _logger = SimpleServiceProvider.Get<ILogger>();
            var company = SapConnectionManager.Instance.GetCompany(forceServiceUser);
            _repository = new ContDateRepository(company);
            _padronRepository = new PSaltaRepository(company);
        }

        public async Task<List<ComboItem>> GetActivePeriodosAsync()
        {
            var rawData = await _repository.GetFechasAsync();
            var activos = rawData.ToList();

            var result = new List<ComboItem>();

            foreach (var r in activos)
            {
                string year = $"{r.U_Desde:yyyy}";

                int errores = await _padronRepository.CountErrorsAsync(r.U_Periodo, year);

                string desc = $"{year} {r.U_Periodo} ({r.U_Desde:dd/MM}-{r.U_Hasta:dd/MM})";

                if (errores > 0)
                {
                    desc += $" [⚠️ {errores} Errores]";
                }

                if (errores > 0 || r.U_Activo == "SI")
                {
                    result.Add(new ComboItem
                    {
                        Value = $"{year} {r.U_Periodo}",
                        Description = desc
                    });
                }
            }

            return result.OrderBy(x => x.Value).ToList();
        }

        public async Task<(DateTime? Desde, DateTime? Hasta)> GetDatesAsync(string year, string qValue)
        {
            var rawData = await _repository.GetFechasAsync();

            var periodo = rawData.FirstOrDefault(r =>
                $"{r.U_Desde:yyyy}" == year &&
                r.U_Periodo == qValue);

            if (periodo != null)
            {
                return (periodo.U_Desde, periodo.U_Hasta);
            }

            return (null, null);
        }
    }
}
