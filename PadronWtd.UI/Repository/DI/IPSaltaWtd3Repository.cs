using System;

namespace PadronWtd.Repository.DI
{
    public interface IPSaltaWtd3Repository
    {
        (bool success, string error) InsertWtd3Direct(
            SAPbobsCOM.Company company,
            int entry,
            string wddCode,
            string cuit,
            DateTime desde,
            DateTime hasta,
            string part2,
            string detType);

        bool CheckWtd3Exists(int entry, string wddCode, string cuit, DateTime desde);

        System.Threading.Tasks.Task<string> UpdateAsync(Domain.PSaltaRecord record);
    }
}
