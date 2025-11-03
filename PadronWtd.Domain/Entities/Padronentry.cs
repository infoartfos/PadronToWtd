namespace PadronWtd.Domain.Entities;

public class PadronEntry
{
    public int RunId { get; set; }
    public int LineNumber { get; set; }
    public string CUIT { get; set; } = "";
    public string Denominacion { get; set; } = "";
    public string ActividadEconomica { get; set; } = "";
    public string NivelRiesgo { get; set; } = "";
}
