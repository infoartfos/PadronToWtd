using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.DI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PadronWtd.UI.Services
{
    public class PeriodosService
    {
        private readonly ContDateRepository _repository;

        public PeriodosService()
        {
            if (App.Company == null)
                throw new InvalidOperationException("DI API no conectada.");

            _repository = new ContDateRepository(App.Company);
        }

        public async Task<List<ComboItem>> GetActivePeriodosAsync()
        {
            var rawData = await _repository.GetFechasAsync();

            var result = rawData
                .Where(r => r.U_Activo == "SI")
                .Select(r => new ComboItem
                {
                    Value = r.Year + " "+ r.U_Periodo,
                    Description = $"{r.Year} {r.U_Periodo} ({r.U_Desde:dd/MM}-{r.U_Hasta:dd/MM} )"
                })
                .OrderBy(x => x.Value) 
                .ToList();

            return result;
        }
    }
}