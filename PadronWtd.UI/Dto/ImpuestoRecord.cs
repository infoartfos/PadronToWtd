namespace PadronWtd.Domain
{
    public class ImpuestoRecord
    {
        public string Inscripcion { get; set; }  // U_Inscripcion
        public string Riesgo { get; set; }       // U_Riesgo
        public string U_Codigo { get; set; }       // U_Codigo
        public string CodigoSap { get; set; }    // U_Codigo_SAP (Del detalle)
        public string Activo { get; set; }       // U_Activo

        public int DocEntry { get; set; }
        public string DetalleRetencion { get; set; }
    }
}