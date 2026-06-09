using PadronWtd.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PadronWtd.Repository.DI
{
    public interface IPSaltaRepository
    {
        Task<List<PSaltaRecord>> GetAllAsync();
        Task<string> UpdateAsync(PSaltaRecord r);
        Task<List<PSaltaRecord>> GetImportadosYErrorByPeriodoAnioAsync(string q_value, string anio);
        Task BulkInsertAsync(List<PSaltaRecord> records, IProgress<int> progress = null);
        Task<int> MarkNonExistentProvidersAsync(string qValue, string year);
        Task DeleteByAnioAndQAsync(string q_value, string anio);
        Task<int> CountErrorsAsync(string qValue, string year);
        Task ResetErrorRecordsAsync(string qValue, string year);
        (bool success, string error) InsertWtd3Direct(int entry, string wddCode, string cuit, DateTime desde, DateTime hasta, string part2, string detType);
        (bool alreadyExists, bool previousOK) CheckWtd3Exists(int entry, string wddCode, string cuit, DateTime desde, DateTime hasta);
        Task<Dictionary<string, int>> GetStatsByAnioAsync(string qValue, string year);
        bool CuitExistsInSap(string cuit);
    }
}
