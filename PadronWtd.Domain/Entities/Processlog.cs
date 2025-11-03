namespace PadronWtd.Domain.Entities;

public class ProcessLog
{
    public int RunId { get; set; }
    public string CUIT { get; set; } = "";
    public string CardCode { get; set; } = "";
    public string CardName { get; set; } = "";
    public bool Updated { get; set; }
    public string Details { get; set; } = "";
}
