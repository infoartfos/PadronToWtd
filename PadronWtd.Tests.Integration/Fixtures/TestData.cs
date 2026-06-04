using System;

namespace PadronWtd.Tests.Integration.Fixtures
{
    public static class TestData
    {
        /// <summary>CUIT de un proveedor PL existente en la compañía de testing.</summary>
        public const string ExistingCuit = "20000156982";

        /// <summary>CUIT que NO existe en OCRD.</summary>
        public const string NonExistentCuit = "20999999999";

        /// <summary>WTCode de prueba (debe existir en la base).</summary>
        public const string WtcCode = "TEST01";

        /// <summary>AbsEntry de prueba (debe ser un AbsEntry existente de WTD1/WTD2).</summary>
        public const int AbsEntry = 1;

        /// <summary>Fecha desde para las pruebas.</summary>
        public static DateTime Desde => new DateTime(2026, 1, 1);

        /// <summary>Fecha hasta para las pruebas.</summary>
        public static DateTime Hasta => new DateTime(2026, 12, 31);
    }
}
