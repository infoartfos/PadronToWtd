using System.Collections.Generic;

namespace PadronWtd.Domain
{
    public class ProcessResult
    {
        public int TotalRegistros { get; set; }
        public int ProcesadosExitosos { get; set; }
        public int RegistrosConError { get; set; }

        // Opcional: Lista de mensajes de error para mostrar al final si se desea
        public List<string> MensajesError { get; set; } = new List<string>();
    }
}