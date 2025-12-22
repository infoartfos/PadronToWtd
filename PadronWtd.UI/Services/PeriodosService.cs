using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.DI;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PadronWtd.UI.Services
{
    public class PeriodosService
    {
        private readonly ContDateRepository _repository;
        private readonly PSaltaRepository _padronRepository;
        private readonly Company _company;


        public PeriodosService(bool forceServiceUser = true)
        {
            _company = SapConnectionManager.Instance.GetCompany(forceServiceUser);
            _repository = new ContDateRepository(_company);
            _padronRepository = new PSaltaRepository(_company);
        }

        public async Task<List<ComboItem>> GetActivePeriodosAsync()
        {
            var rawData = await _repository.GetFechasAsync();
            var activos = rawData.ToList();
            // var activos = rawData.Where(r => r.U_Activo == "SI").ToList();

            var result = new List<ComboItem>();

            foreach (var r in activos)
            {
                int errores = await _padronRepository.CountErrorsAsync(r.U_Periodo, r.Year);

                string desc = $"{r.Year} {r.U_Periodo} ({r.U_Desde:dd/MM}-{r.U_Hasta:dd/MM})";
                if (errores > 0)
                {
                    desc += $" [⚠️ {errores} Errores]";
                    result.Add(new ComboItem
                    {
                        Value = r.Year + " " + r.U_Periodo,
                        Description = desc
                    });
                } else
                {
                    if (r.U_Activo == "SI")
                    {
                        result.Add(new ComboItem
                        {
                            Value = r.Year + " " + r.U_Periodo,
                            Description = desc
                        });
                    }
                }
            }

            return result.OrderBy(x => x.Value).ToList();
        }


        public async Task<(DateTime? Desde, DateTime? Hasta)> GetDatesAsync(string year, string qValue)
        {
            var rawData = await _repository.GetFechasAsync();

            var periodo = rawData.FirstOrDefault(r =>
                r.Year == year &&
                r.U_Periodo == qValue
            );

            if (periodo != null)
            {
                return (periodo.U_Desde, periodo.U_Hasta);
            }

            return (null, null);
        }
    }
}
