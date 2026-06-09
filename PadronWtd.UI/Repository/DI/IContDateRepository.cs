using PadronWtd.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PadronWtd.Repository.DI
{
    public interface IContDateRepository
    {
        Task<List<ContDateRecord>> GetImpuestosAsync();
        Task<List<ContDateRecord>> GetFechasAsync();
        Task DeactivatePeriodAsync(string year, string qValue);
    }
}
