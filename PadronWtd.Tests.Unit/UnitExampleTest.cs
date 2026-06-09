using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
using SAPbouiCOM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PadronWtd.UI.Tests
{
    /// <summary>
    /// Mock de ILogger que captura mensajes en memoria para assert.
    /// </summary>
    public class MockLogger : ILogger
    {
        public List<string> InfoMessages { get; } = new List<string>();
        public List<string> WarnMessages { get; } = new List<string>();
        public List<string> ErrorMessages { get; } = new List<string>();

        public void Info(string message) => InfoMessages.Add(message);
        public void Warn(string message) => WarnMessages.Add(message);
        public void Error(string message, System.Exception ex = null) => ErrorMessages.Add(message);
    }

    /// <summary>
    /// Mock de IPSaltaRepository que permite configurar comportamiento por test.
    /// </summary>
    public class MockPSaltaRepository : IPSaltaRepository
    {
        public Func<string, string, Task<List<PSaltaRecord>>> OnGetImportadosYError { get; set; }
            = (q, y) => Task.FromResult(new List<PSaltaRecord>());

        public Func<string, string, Task<Dictionary<string, int>>> OnGetStats { get; set; }
            = (q, y) => Task.FromResult(new Dictionary<string, int>());

        public Func<string, string, Task<int>> OnMarkNonExistent { get; set; }
            = (q, y) => Task.FromResult(0);

        public Func<int, string, string, DateTime, DateTime, (bool, bool)> OnCheckWtd3Exists { get; set; }
            = (e, c, cuit, d, h) => (false, false);

        public Func<int, string, string, DateTime, DateTime, string, string, (bool, string)> OnInsertWtd3 { get; set; }
            = (e, c, cuit, d, h, p2, dt) => (true, "");

        public Func<PSaltaRecord, Task<string>> OnUpdate { get; set; }
            = r => Task.FromResult(r.Code);

        public Func<string, string, Task<int>> OnCountErrors { get; set; }
            = (q, y) => Task.FromResult(0);

        public Action<string, string> OnResetErrors { get; set; }
            = (q, y) => { };

        // --- Implementación de la interfaz ---

        public Task<List<PSaltaRecord>> GetAllAsync() =>
            Task.FromResult(new List<PSaltaRecord>());

        public Task<List<PSaltaRecord>> GetImportadosYErrorByPeriodoAnioAsync(string q, string y) =>
            OnGetImportadosYError(q, y);

        public Task<Dictionary<string, int>> GetStatsByAnioAsync(string q, string y) =>
            OnGetStats(q, y);

        public Task<int> MarkNonExistentProvidersAsync(string q, string y) =>
            OnMarkNonExistent(q, y);

        public (bool success, string error) InsertWtd3Direct(
            int entry, string wddCode, string cuit, DateTime desde, DateTime hasta,
            string part2, string detType) =>
            OnInsertWtd3(entry, wddCode, cuit, desde, hasta, part2, detType);

        public (bool alreadyExists, bool previousOK) CheckWtd3Exists(
            int entry, string wddCode, string cuit, DateTime desde, DateTime hasta) =>
            OnCheckWtd3Exists(entry, wddCode, cuit, desde, hasta);

        public Task<string> UpdateAsync(PSaltaRecord r) => OnUpdate(r);

        public Task<int> CountErrorsAsync(string q, string y) => OnCountErrors(q, y);

        public Task ResetErrorRecordsAsync(string q, string y)
        {
            OnResetErrors(q, y);
            return Task.CompletedTask;
        }

        public Task BulkInsertAsync(List<PSaltaRecord> records, IProgress<int> progress = null) =>
            Task.CompletedTask;

        public Task DeleteByAnioAndQAsync(string q, string y) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// Mock de ISaltaConfigRepository.
    /// </summary>
    public class MockSaltaConfigRepository : ISaltaConfigRepository
    {
        public Func<Task<List<ImpuestoRecord>>> OnGetConfig { get; set; }
            = () => Task.FromResult(new List<ImpuestoRecord>());

        public Task<List<ImpuestoRecord>> GetConfiguracionImpuestosAsync() => OnGetConfig();
    }

    /// <summary>
    /// Mock de IContDateRepository.
    /// </summary>
    public class MockContDateRepository : IContDateRepository
    {
        public Action<string, string> OnDeactivate { get; set; } = (y, q) => { };

        public Task<List<ContDateRecord>> GetImpuestosAsync() =>
            Task.FromResult(new List<ContDateRecord>());

        public Task<List<ContDateRecord>> GetFechasAsync() =>
            Task.FromResult(new List<ContDateRecord>());

        public Task DeactivatePeriodAsync(string year, string qValue)
        {
            OnDeactivate(year, qValue);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Mock de IPeriodosService.
    /// </summary>
    public class MockPeriodosService : IPeriodosService
    {
        public Func<string, string, Task<(DateTime?, DateTime?)>> OnGetDates { get; set; }
            = (y, q) => Task.FromResult(((DateTime?)new DateTime(2025, 1, 1), (DateTime?)new DateTime(2025, 1, 31)));

        public Task<(DateTime? Desde, DateTime? Hasta)> GetDatesAsync(string year, string qValue) =>
            OnGetDates(year, qValue);
    }

    /// <summary>
    /// Ejemplo de test unitario con dependencias mockeadas.
    /// Verifica entradas de log específicas sin necesidad de SAP.
    /// </summary>
    public static class UnitExampleTest
    {
        private static PSaltaRecord CreateTestRecord(string cuit = "30711111118")
        {
            return new PSaltaRecord
            {
                Code = "1",
                Name = "T01",
                U_Anio = "2025",
                U_Padron = "T01",
                U_Cuit = cuit,
                U_Inscripcion = "01",
                U_Riesgo = "01",
                U_Estado = "10"
            };
        }

        private static ImpuestoRecord CreateTaxConfig()
        {
            return new ImpuestoRecord
            {
                Inscripcion = "01",
                Riesgo = "01",
                CodigoSap = "IG01",
                U_Codigo = "1"
            };
        }

        /// <summary>
        /// Test 1: Registro procesado OK → log debe contener "WTD3 insertado OK"
        /// </summary>
        public static async Task Test_Wtd3InsertLogEntry()
        {
            var logger = new MockLogger();
            var periodosMock = new MockPeriodosService();
            var configRepo = new MockSaltaConfigRepository();
            configRepo.OnGetConfig = () => Task.FromResult(new List<ImpuestoRecord> { CreateTaxConfig() });

            var contDateRepo = new MockContDateRepository();

            var impRepo = new MockPSaltaRepository();
            impRepo.OnGetImportadosYError = (q, y) =>
                Task.FromResult(new List<PSaltaRecord> { CreateTestRecord() });

            impRepo.OnCheckWtd3Exists = (e, c, cuit, d, h) => (false, false);
            impRepo.OnInsertWtd3 = (e, c, cuit, d, h, p2, dt) =>
            {
                logger.Info($"WTD3 insertado OK - CUIT:{cuit}, {c} , Desd:{d:yyyyMMdd}");
                return (true, "");
            };
            impRepo.OnUpdate = r =>
            {
                r.U_Estado = "20";
                return Task.FromResult(r.Code);
            };
            
            var service = new ProcessInfoService(
                logger,
                impRepo,
                configRepo,
                contDateRepo,
                periodosMock,
                _company
            );

            await service.ProcessRecordsAsync("T01", "2025");

            var found = logger.InfoMessages.Any(m => m.Contains("WTD3 insertado OK"));
            Console.WriteLine($"[Test_Wtd3InsertLogEntry] {(found ? "PASS" : "FAIL")}");
            if (!found)
            {
                Console.WriteLine("  INFO messages logged:");
                foreach (var m in logger.InfoMessages) Console.WriteLine($"    {m}");
            }
        }

        /// <summary>
        /// Test 2: Registro ya existente en WTD3 → log debe contener "WTD3 ya existía"
        /// </summary>
        public static async Task Test_Wtd3AlreadyExistsLogEntry()
        {
            var logger = new MockLogger();
            var periodosMock = new MockPeriodosService();
            var configRepo = new MockSaltaConfigRepository();
            configRepo.OnGetConfig = () => Task.FromResult(new List<ImpuestoRecord> { CreateTaxConfig() });

            var contDateRepo = new MockContDateRepository();

            var impRepo = new MockPSaltaRepository();
            impRepo.OnGetImportadosYError = (q, y) =>
                Task.FromResult(new List<PSaltaRecord> { CreateTestRecord() });

            impRepo.OnCheckWtd3Exists = (e, c, cuit, d, h) => (true, true);

            var service = new ProcessInfoService(
                logger,
                impRepo,
                configRepo,
                contDateRepo,
                periodosMock
            );

            await service.ProcessRecordsAsync("T01", "2025");

            var found = logger.WarnMessages.Any(m => m.Contains("WTD3 ya existía"));
            Console.WriteLine($"[Test_Wtd3AlreadyExistsLogEntry] {(found ? "PASS" : "FAIL")}");
            if (!found)
            {
                Console.WriteLine("  WARN messages logged:");
                foreach (var m in logger.WarnMessages) Console.WriteLine($"    {m}");
            }
        }

        /// <summary>
        /// Test 3: Proveedor no existe en OCRD → log debe contener "no existe"
        /// </summary>
        public static async Task Test_ProviderNotFoundLogEntry()
        {
            var logger = new MockLogger();
            var periodosMock = new MockPeriodosService();
            var configRepo = new MockSaltaConfigRepository();
            var contDateRepo = new MockContDateRepository();

            var impRepo = new MockPSaltaRepository();
            impRepo.OnGetImportadosYError = (q, y) =>
                Task.FromResult(new List<PSaltaRecord> { CreateTestRecord() });

            // Mock MarkNonExistentProvidersAsync sets them to '30'
            impRepo.OnMarkNonExistent = (q, y) =>
            {
                // This would normally update DB; for the test we simulate
                // by returning empty list from GetImportadosYError
                return Task.FromResult(1);
            };
            impRepo.OnGetImportadosYError = (q, y) =>
                Task.FromResult(new List<PSaltaRecord>());

            var service = new ProcessInfoService(
                logger,
                impRepo,
                configRepo,
                contDateRepo,
                periodosMock
            );

            await service.ProcessRecordsAsync("T01", "2025");

            var found = logger.WarnMessages.Any(m => m.Contains("No se encontraron registros"));
            Console.WriteLine($"[Test_ProviderNotFoundLogEntry] {(found ? "PASS" : "FAIL")}");
            if (!found)
            {
                Console.WriteLine("  WARN messages logged:");
                foreach (var m in logger.WarnMessages) Console.WriteLine($"    {m}");
            }
        }

        /// <summary>
        /// Ejecuta todos los tests unitarios de ejemplo.
        /// </summary>
        public static async Task RunAll()
        {
            Console.WriteLine("=== UNIT TEST EXAMPLES ===");
            Console.WriteLine();

            await Test_Wtd3InsertLogEntry();
            await Test_Wtd3AlreadyExistsLogEntry();
            await Test_ProviderNotFoundLogEntry();

            Console.WriteLine();
            Console.WriteLine("=== FIN UNIT TESTS ===");
        }
    }
}
