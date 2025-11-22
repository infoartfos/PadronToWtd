using System;

namespace PadronWtd.Domain
{
    /// <summary>
    /// Representa un registro de la tabla de usuario @P_SALTA
    /// </summary>
    public class PSaltaRecord
    {
        // Campos Estándar de SAP (PK y Metadatos)
        public string Code { get; set; }
        public string Name { get; set; }
        public int DocEntry { get; set; }
        public string Canceled { get; set; }
        public string Object { get; set; }
        public int? UserSign { get; set; }
        public DateTime? CreateDate { get; set; }
        public string DataSource { get; set; }

        // Campos de Usuario (UDFs)
        public string U_Anio { get; set; }
        public string U_Padron { get; set; }
        public string U_Cuit { get; set; }
        public string U_Inscripcion { get; set; }
        public string U_Riesgo { get; set; }
        public string U_Notas { get; set; }
        public string U_Procesado { get; set; } // Puede ser 'Y', 'N' o nulo
        public string U_Estado { get; set; }

        public PSaltaRecord()
        {
            // Inicialización por defecto si es necesario
            Code = string.Empty;
            Name = string.Empty;
        }
    }
}