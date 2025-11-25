using PadronWtd.UI.SL;
using System.Threading.Tasks;

namespace PadronWtd.UI.SL
{
    public class SapPSaltaService
    {
        private ServiceLayerClient _client;

        public SapPSaltaService(ServiceLayerClient client)
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
