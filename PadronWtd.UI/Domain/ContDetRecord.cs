using System;

namespace PadronWtd.Domain
{
    public class ContDateRecord
    {
        public string HeaderCode { get; set; }
        public string Year { get; set; }
        public string DetailCode { get; set; }
        public string U_Periodo { get; set; }
        public DateTime? U_Desde { get; set; }
        public DateTime? U_Hasta { get; set; }
        public string U_Activo { get; set; }
    }
}