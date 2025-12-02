namespace PadronWtd.Domain
{
    public class ComboItem
    {
        public string Value { get; set; }       // Ej: "2025 Q1" (Lo que se guarda en BD)
        public string Description { get; set; } // Ej: "Q1 2025 - Activo" (Lo que ve el usuario)
    }
}