using System;
using System.Threading.Tasks;

namespace PadronWtd.UI.Services
{
    public interface IPeriodosService
    {
        Task<(DateTime? Desde, DateTime? Hasta)> GetDatesAsync(string year, string qValue);
    }
}
