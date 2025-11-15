namespace PadronWtd.Application.Dto
{
    public class PSaltaDto
    {
        public string Code { get; set; }     // Clave primaria UDT
        public string Name { get; set; }     // Nombre visible

        public string Anio { get; set; }
        public string Padron { get; set; }
        public string Cuit { get; set; }
        public string Inscripcion { get; set; }
        public string Riesgo { get; set; }
        public string Notas { get; set; }
        public string Procesado { get; set; }
    }
}
