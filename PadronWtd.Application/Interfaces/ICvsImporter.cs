using PadronWtd.Domain.Entities;

namespace PadronWtd.Application.Interfaces;

public interface ICsvImporter
{
    Task<int> ImportAsync(string path, int runId, string user);
}
