using System;
using System.Collections.Generic;
using System.Configuration;
// Asegúrate de tener este using para las listas
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using SAPbouiCOM;
using static System.Runtime.CompilerServices.RuntimeHelpers;


namespace PadronWtd.UI.Services
{
    public class ProcessInfoService
    {
        private readonly ILogger _logger;
        private readonly IPSaltaRepository _impSaltaRepository;
        private readonly ISaltaConfigRepository _configRepository;
        private readonly IContDateRepository _contDateRepository;
        private readonly IPeriodosService _periodosService;
        private readonly SAPbobsCOM.Company _company;
        

        private Dictionary<string, List<ImpuestoCacheItem>> _impuestosCache;

        public ProcessInfoService(
            ILogger logger,
            IPSaltaRepository impSaltaRepository,
            ISaltaConfigRepository configRepository,
            IContDateRepository contDateRepository,
            IPeriodosService periodosService = null,
            SAPbobsCOM.Company company = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _impSaltaRepository = impSaltaRepository ?? throw new ArgumentNullException(nameof(impSaltaRepository));
            _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
            _contDateRepository = contDateRepository ?? throw new ArgumentNullException(nameof(contDateRepository));
            _periodosService = periodosService;
            _company = company;
        }

        public ProcessInfoService(bool forceServiceUser = true)
        {
            _logger = SimpleServiceProvider.Get<ILogger>();

            _company = SapConnectionManager.Instance.GetCompany(forceServiceUser);
            _impSaltaRepository = new PSaltaRepository(_company);
            _configRepository = new SaltaConfigRepository(_company);
            _contDateRepository = new ContDateRepository(_company);
            _periodosService = null;
        }

        
        public async Task<ProcessResult> ProcessRecordsAsync(string qPeriodo, string year, IProgress<int> progress = null)
        {
            _logger.Info($"Iniciando procesamiento para {year} - {qPeriodo}...");
            var result = new ProcessResult();

            await LoadImpuestosCacheAsync();

            // 1. Calcular totales iniciales de control
            result.TotalRegistros = await CalculateTotalRecordsAsync(qPeriodo, year);

            await _impSaltaRepository.MarkNonExistentProvidersAsync(qPeriodo, year);

            // 2. Obtener lote de registros a procesar
            List<PSaltaRecord> records = await _impSaltaRepository.GetImportadosYErrorByPeriodoAnioAsync(qPeriodo, year);
            if (records == null || !records.Any())
            {
                _logger.Warn("No se encontraron registros para procesar.");
                return result;
            }

            (DateTime desde, DateTime hasta) = await GetDynamicDates(qPeriodo, year);
            
            // 3. Procesamiento del lote (Excluimos Task.Run innecesario para no matar el contexto del SDK)
            var (successCount, errorCount) = await ProcessRecordListAsync(records, desde, hasta, progress);

            // 4. Cierre del proceso
            progress?.Report(100);
            await _contDateRepository.DeactivatePeriodAsync(year, qPeriodo);

            result.RegistrosConError = errorCount;
            result.ProcesadosExitosos = successCount; 

            _logger.Info($"Procesamiento finalizado. Total lote: {records.Count}, Exitosos: {successCount}, Errores: {errorCount}");
            return result;
        }

        private async Task<int> CalculateTotalRecordsAsync(string qPeriodo, string year)
        {
            var stats = await _impSaltaRepository.GetStatsByAnioAsync(qPeriodo, year);
            string[] keys = { "Importado", "10", "Procesado", "20", "No Encontrado", "30", "Error", "40" };
            return keys.Sum(key => stats.TryGetValue(key, out int value) ? value : 0);
        }

        private async Task<(int successCount, int errorCount)> ProcessRecordListAsync(
            List<PSaltaRecord> records, DateTime desde, DateTime hasta, IProgress<int> progress)
        {
            int successCount = 0;
            int errorCount = 0;
            int total = records.Count;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            for (int i = 0; i < total; i++)
            {
                var record = records[i];
                bool isProcessed = await ProcessSingleRecordAsync(record, desde, hasta, timestamp);

                if (isProcessed) successCount++;
                else errorCount++;

                if (progress != null && i % 20 == 0)
                {
                    progress.Report((int)((double)i / total * 100));
                }
            }

            return (successCount, errorCount);
        }


        private async Task<bool> ProcessSingleRecordAsync(PSaltaRecord record, DateTime desde, DateTime hasta, string timestamp)
        {
            try
            {
                if (string.IsNullOrEmpty(record.U_Cuit))
                    throw new ArgumentException("El registro no tiene CUIT.");

                if (!CuitExistsInSap(record.U_Cuit))
                {
                    _logger.Warn($"Proveedor con CUIT {record.U_Cuit} no existe. Omitiendo.");
                    await SafeUpdateRecord(record, "30", "Proveedor No Existe", timestamp);
                    return false;
                }

                List<ImpuestoCacheItem> taxItems = GetWhtCodesFromCache(record.U_Inscripcion, record.U_Riesgo);
                if (!taxItems.Any())
                {
                    _logger.Warn($"No existe configuración para Insc: {record.U_Inscripcion} / Riesgo: {record.U_Riesgo}");
                    await SafeUpdateRecord(record, "40", "Configuración Impuesto No Encontrada", timestamp);
                    return false;
                }

                // Procesar inserciones con transacción DI API
                bool transactionStarted = false;
                List<string> existingBadCodes = new List<string>();
                List<string> existingOkCodes = new List<string>();
                List<string> insertedCodes = new List<string>();
                try
                {
                    _company.StartTransaction();
                    transactionStarted = true;

                    foreach (var item in taxItems)
                    {
                        int taxEntry = int.TryParse(item.U_Codigo, out int parsed) ? parsed : 1;
                        string codigoSap = item.CodigoSap;
                        string cuit = record.U_Cuit;

                        // Verificar si ya existe en WTD3
                        var (alreadyExists, previousOK) = _impSaltaRepository.CheckWtd3Exists(taxEntry, codigoSap, cuit, desde, hasta);
                        if ( alreadyExists || previousOK)
                        {
                            if (previousOK) 
                            {
                                _logger.Warn($"WTD3 ya existía para CUIT:{cuit} WTCode:{codigoSap} - Omitiendo");
                                existingOkCodes.Add(codigoSap);
                            } else {
                                _logger.Warn($"WTD3 ya existía para CUIT:{cuit} WTCode:{codigoSap} - Omitiendo marca error");
                                existingBadCodes.Add(codigoSap);
                            }
                            continue;
                        } else
                        {
                            _logger.Info($"InsertWtd3 taxEntry:{taxEntry}, codigoSap:{codigoSap}, cuit:{cuit}");

                            var (success, error) = _impSaltaRepository.InsertWtd3Direct(
                                taxEntry,
                                codigoSap,
                                cuit,
                                desde,
                                hasta,
                                "80",
                                "A"
                            );

                            if (!success)
                                throw new Exception($"{error}");

                            insertedCodes.Add(codigoSap);
                        }

                    }

                    _company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                    transactionStarted = false;
                }
                catch (Exception ex)
                {
                    if (transactionStarted && _company.InTransaction)
                    {
                        try { _company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack); }
                        catch (Exception rbEx) { _logger.Error($"Error en rollback: {rbEx.Message}"); }
                        transactionStarted = false;
                    }
                    throw(ex);
                }

                string note = "";
                if (existingOkCodes.Any() || existingBadCodes.Any())
                {
                    note = $"Se encontraron ya registrados: ";
                    if (existingOkCodes.Any())
                    {
                        note += $"[OK { string.Join(" ", existingOkCodes)}]";
                    }

                    if (existingBadCodes.Any())
                    {
                        note += $"[CON DIFERENCIAS { string.Join(" ", existingBadCodes)}]";
                        if (insertedCodes.Any())
                        {
                            note += $"insertados OK [ {string.Join(" ", insertedCodes)}]";
                        }
                        await SafeUpdateRecord(record, "40", note, timestamp);
                        _logger.Warn($"Registro CUIT {record.U_Cuit} ${note}");
                        return false;
                    }

                }

                string processedCodes = string.Join(" ", insertedCodes);
                string message = $"Procesado OK. Insertados: [{processedCodes}] {note}";
                await SafeUpdateRecord(record, "20", message, timestamp);
                _logger.Info($"Procesado CUIT {record.U_Cuit}: {message}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error procesando CUIT {record.U_Cuit}: {ex.Message} {ex.StackTrace}");
                await SafeUpdateRecord(record, "40", $"{ex.Message}", timestamp);
                return false;
            }
        }

        // --------------------------------------------------------------------------------------------
        // Helpers para evitar Crash en Updates
        // --------------------------------------------------------------------------------------------

        private async Task SafeUpdateRecord(PSaltaRecord record, string estado, string notas, string fecha)
        {
            try
            {
                record.U_Estado = estado;
                record.U_Procesado = fecha;

                if (notas != null)
                {
                    notas = notas.Replace("'", "");
                    SAPbobsCOM.UserTables oUserTables = _company.UserTables;
                    SAPbobsCOM.UserTable oTable = oUserTables.Item("PADRON_SALTA_IMP3");

                    int maxStringLength = oTable.UserFields.Fields.Item("U_Notas").Size;
                    if (notas.Length > maxStringLength)
                    {
                        _logger.Warn($"El mensaje de notas superaba los {maxStringLength} caracteres. Se recortó dinámicamente.");
                        notas = notas.Substring(0, maxStringLength);
                    }
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(oTable);
                }
                else
                {
                    notas = "";
                }

                record.U_Notas = notas;
                await _impSaltaRepository.UpdateAsync(record);
            }
            catch (Exception ex)
            {
                _logger.Error($"Fallo CRÍTICO al actualizar estado en SAP para Code {record.Code}. Causa: {ex.Message}");
            }
        }

        // --------------------------------------------------------------------------------------------
        // Helpers de Lógica de Negocio (Cache y DB)
        // --------------------------------------------------------------------------------------------

        private async Task LoadImpuestosCacheAsync()
        {
            _impuestosCache = new Dictionary<string, List<ImpuestoCacheItem>>();

            var configs = await _configRepository.GetConfiguracionImpuestosAsync();

            foreach (var cfg in configs)
            {
                string key = $"{cfg.Inscripcion.Trim().ToUpper()}_{cfg.Riesgo.Trim().ToUpper()}";

                if (!_impuestosCache.ContainsKey(key))
                {
                    _impuestosCache[key] = new List<ImpuestoCacheItem>();
                }

                bool existe = _impuestosCache[key].Exists(x => x.CodigoSap == cfg.CodigoSap);

                if (!existe)
                {
                    _impuestosCache[key].Add(new ImpuestoCacheItem
                    {
                        CodigoSap = cfg.CodigoSap,
                        U_Codigo = cfg.U_Codigo
                    });
                }
            }

            _logger.Info($"Caché de impuestos cargada: {_impuestosCache.Count} combinaciones.");
        }

        private List<ImpuestoCacheItem> GetWhtCodesFromCache(string inscripcion, string riesgo)
        {
            if (_impuestosCache == null) return new List<ImpuestoCacheItem>();

            string key = $"{inscripcion?.Trim().ToUpper()}_{riesgo?.Trim().ToUpper()}";

            if (_impuestosCache.TryGetValue(key, out List<ImpuestoCacheItem> items))
            {
                return items;
            }
            return new List<ImpuestoCacheItem>();
        }

        private async Task<(DateTime, DateTime)> GetDynamicDates(string qPeriodo, string year)
        {
            var service = _periodosService ?? new PeriodosService();
            var fechas = await service.GetDatesAsync(year, qPeriodo);

            if (fechas.Desde.HasValue && fechas.Hasta.HasValue)
            {
                return (fechas.Desde.Value, fechas.Hasta.Value);
            }

            throw new Exception($"No se encontraron fechas configuradas para {year} {qPeriodo}");
        }

        private bool CuitExistsInSap(string cuit)
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                string query = $@"
                    SELECT COUNT(*) 
                    FROM ""OCRD"" 
                    WHERE ""LicTradNum"" = '{cuit}'
                    AND UPPER(""CardCode"") LIKE 'PL%'";

                rs.DoQuery(query);

                if (!rs.EoF)
                    return int.Parse(rs.Fields.Item(0).Value.ToString()) > 0;

                return false;
            }
            catch { return false; }
            finally { if (rs != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(rs); }
        }

    }

    public class ImpuestoCacheItem
    {
        public string CodigoSap { get; set; }
        public string U_Codigo { get; set; }
    }
}