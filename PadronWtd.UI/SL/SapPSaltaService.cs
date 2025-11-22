using PadronWtd.UI.SL;
using System.Threading.Tasks;

namespace PadronWtd.UI.SL
{
    public class SapPSaltaService
    {
        private readonly ServiceLayerClientOriginalBorrar _client;

        public SapPSaltaService(ServiceLayerClientOriginalBorrar client)
        {
            _client = client;
        }

        public Task<string> InsertAsync(PSaltaDto dto)
        {
            // Resource name tal como en Service Layer
            return _client.PostAsync("P_Salta", dto);
        }
    }
}
