using PadronWtd.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PadronWtd.Repository.DI
{
    public interface ISaltaConfigRepository
    {
        Task<List<ImpuestoRecord>> GetConfiguracionImpuestosAsync();
    }
}
