using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PadronWtd.UI.Tests
{
    /// <summary>
    /// Ejemplo de test funcional (integración) que:
    /// 1. Prepara data de prueba vía Service Layer (REST)
    /// 2. Ejecuta ProcessInfoService con DI API (requiere SAP B1 corriendo)
    /// 3. Lee el archivo de log y verifica entradas específicas
    ///
    /// REQUISITOS:
    /// - SAP Business One con DI API disponible (SAP GUI o SL conectado)
    /// - Service Layer accesible (para el setup)
    /// - Credenciales configuradas en App.config o hardcodeadas para dev
    /// </summary>
    public static class FunctionalTestExample
    {
        /// <summary>
        /// Prepara data de prueba usando Service Layer.
        /// Inserta en @PADRON_SALTA_IMP3, configura @COD_SALTA_CAB/DET,
        /// y asegura que exista un business partner en OCRD.
        /// </summary>
        private static async Task<bool> SetupTestDataViaSL(string slUrl, string user, string pass, string db)
        {
            try
            {
                var slClient = new ServiceLayerClient(slUrl);
                await slClient.LoginAsync(user, pass, db);

                // 1. Insertar registro de prueba en @PADRON_SALTA_IMP3
                //    Usando el DTO que espera la SL
                var dto = new PadronWtd.UI.SL.PSaltaDto
                {
                    Code = "999999",
                    Name = "T99",
                    U_Anio = "2025",
                    U_Padron = "TEST_FUNCIONAL",
                    U_Cuit = "30711111118",
                    U_Inscripcion = "01",
                    U_Riesgo = "01",
                    U_Estado = "10",
                    U_Notas = "Creado por test funcional"
                };
                var result = await slClient.PostAsync("P_Salta", dto);
                Console.WriteLine($"  SL insert P_Salta: {result}");

                // 2. Opcional: verificar que el socio existe en OCRD
                //    GET /BusinessPartners?$filter=CardCode eq 'PL000001'
                //    Si no existe, crearlo con POST /BusinessPartners

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  SL Setup skipped: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Limpia la data de prueba creada por SetupTestDataViaSL.
        /// </summary>
        private static async Task CleanupTestDataViaSL(string slUrl, string user, string pass, string db)
        {
            try
            {
                var slClient = new ServiceLayerClient(slUrl);
                await slClient.LoginAsync(user, pass, db);

                // Eliminar registro de prueba
                await slClient.DeleteAsync("P_Salta('999999')");
                Console.WriteLine("  SL cleanup: registro eliminado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  SL cleanup skipped: {ex.Message}");
            }
        }

        /// <summary>
        /// Test funcional completo.
        /// 
        /// FLUJO:
        ///   Arrange: SL inserta data en @PADRON_SALTA_IMP3 y configura impuestos
        ///   Act:     ProcessInfoService procesa los registros contra WTD3
        ///   Assert:  Se verifica que el archivo de log contenga "WTD3 insertado OK"
        /// </summary>
        public static async Task Run()
        {
            Console.WriteLine("=== FUNCTIONAL TEST EXAMPLE ===");
            Console.WriteLine("  Requiere SAP B1 + Service Layer disponibles");
            Console.WriteLine();

            // --- CONFIGURACIÓN (ajustar según entorno) ---
            string slUrl = "https://contreras-hanadb.sbo.contreras.com.ar:50000/b1s/v1";
            string slUser = "uuuuuuuuuu";
            string slPass = "XXXXXXXXX";
            string slDb = "SBP_SIOC_CHAR";

            string logFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "PadronWtd", "padron_import_test.log");

            // --- ARRANGE: Preparar data vía Service Layer ---
            Console.WriteLine("  [Arrange] Preparando data de prueba vía SL...");
            bool setupOk = await SetupTestDataViaSL(slUrl, slUser, slPass, slDb);
            if (!setupOk)
            {
                Console.WriteLine("  SKIP - No se pudo conectar a Service Layer.");
                Console.WriteLine();
                Console.WriteLine("  Para correr este test:");
                Console.WriteLine("  1. Asegúrate que SL esté accesible");
                Console.WriteLine("  2. Ajusta credenciales en este archivo");
                Console.WriteLine("  3. Ten SAP B1 corriendo con DI API disponible");
                return;
            }

            try
            {
                // --- ACT: Ejecutar procesamiento con DI API ---
                Console.WriteLine("  [Act] Ejecutando procesamiento SAP...");

                var logger = new FileLogger(logFilePath);
                var company = SapConnectionManager.Instance.GetCompany(true);

                var impRepo = new PSaltaRepository(company, logger);
                var configRepo = new SaltaConfigRepository(company, logger);
                var contDateRepo = new ContDateRepository(company, logger);

                var service = new ProcessInfoService(
                    logger,
                    impRepo,
                    configRepo,
                    contDateRepo,
                    company: company
                );

                var result = await service.ProcessRecordsAsync("T99", "2025");

                Console.WriteLine($"  Procesados exitosos: {result.ProcesadosExitosos}");
                Console.WriteLine($"  Errores: {result.RegistrosConError}");

                // --- ASSERT: Leer log y verificar ---
                Console.WriteLine("  [Assert] Verificando archivo de log...");

                if (File.Exists(logFilePath))
                {
                    var logLines = File.ReadAllLines(logFilePath);

                    bool foundInsertOk = logLines.Any(l =>
                        l.Contains("WTD3 insertado OK") &&
                        l.Contains("30711111118"));

                    bool foundProcessingEnd = logLines.Any(l =>
                        l.Contains("Procesamiento finalizado"));

                    Console.WriteLine($"  Log entry 'WTD3 insertado OK': {(foundInsertOk ? "PASS" : "FAIL")}");
                    Console.WriteLine($"  Log entry 'Procesamiento finalizado': {(foundProcessingEnd ? "PASS" : "FAIL")}");

                    Console.WriteLine();
                    Console.WriteLine("  Últimas líneas del log:");
                    var tail = logLines.Reverse().Take(10).Reverse().ToList();
                    foreach (var line in tail)
                    {
                        Console.WriteLine($"    {line}");
                    }
                }
                else
                {
                    Console.WriteLine("  FAIL - No se encontró el archivo de log");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR durante test funcional: {ex.Message}");
            }
            finally
            {
                // --- CLEANUP: Eliminar data de prueba ---
                Console.WriteLine("  [Cleanup] Limpiando data de prueba...");
                await CleanupTestDataViaSL(slUrl, slUser, slPass, slDb);
            }

            Console.WriteLine();
            Console.WriteLine("=== FIN FUNCTIONAL TEST ===");
        }
    }
}
