namespace PadronWtd.Domain.Entities;

public class Tax
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Detail { get; set; } = "";
    public int WtCode { get; set; }
}
