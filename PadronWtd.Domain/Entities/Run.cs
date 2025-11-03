namespace PadronWtd.Domain.Entities;

public class Run
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public bool Active { get; set; }
}
