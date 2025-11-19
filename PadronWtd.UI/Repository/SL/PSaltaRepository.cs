using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class PSaltaRepository
{
    private readonly ServiceLayerClientDebug _sl;

    public PSaltaRepository(ServiceLayerClientDebug sl)
    {
        _sl = sl;
    }

    // -----------------------------------------------------------------------
    // GET /P_Salta
    // -----------------------------------------------------------------------
    public async Task<List<PSaltaRecord>> GetAllAsync()
    {
        string json = await _sl.GetAsync("P_Salta");

        var wrapper = JsonConvert.DeserializeObject<PSaltaWrapper>(json);

        return wrapper.value;
    }

    // -----------------------------------------------------------------------
    // POST /P_Salta
    // -----------------------------------------------------------------------
    public async Task<string> CreateAsync(PSaltaRecord r)
    {
        r.Code = await this.GetNextCodeAsync();

        return await _sl.PostAsync("P_Salta", r);
    }

    // -----------------------------------------------------------------------
    // PUT /P_Salta('Code')
    // -----------------------------------------------------------------------
    public Task<string> UpdateAsync(PSaltaRecord r)
    {
        return _sl.PutAsync($"P_Salta('{r.Code}')", r);
    }

    // -----------------------------------------------------------------------
    // Para deserializar los GET
    // -----------------------------------------------------------------------
    private class PSaltaWrapper
    {
        public List<PSaltaRecord> value { get; set; }
    }

    // -----------------------------------------------------------------------
    // Obtiene el próximo CODE incremental
    // -----------------------------------------------------------------------
    private async Task<string> GetNextCodeAsync()
    {
        // GET ordenando desc para obtener el último
        string json = await _sl.GetAsync("P_Salta?$select=Code&$orderby=Code desc&$top=1");

        var wrapper = JsonConvert.DeserializeObject<PSaltaWrapper>(json);

        string lastCode = "0";

        if (wrapper.value != null && wrapper.value.Count > 0)
            lastCode = wrapper.value[0].Code;

        int next = int.Parse(lastCode) + 1;

        return next.ToString();
    }
}
