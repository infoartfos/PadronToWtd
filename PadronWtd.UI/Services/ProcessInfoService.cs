using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
// Asegúrate de tener este using para las listas
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using System.Configuration;

namespace PadronWtd.UI.Services
{
    public class ProcessInfoService
    {
        private readonly ILogger _logger;
        private readonly PSaltaRepository _impSaltaRepository;
        private readonly SaltaConfigRepository _configRepository;
        private readonly ContDateRepository _contDateRepository;
        private readonly Company _company;

        private Dictionary<string, List<ImpuestoCacheItem>> _impuestosCache;

        public ProcessInfoService(bool forceServiceUser = true)
        {
            _logger = SimpleServiceProvider.Get<ILogger>();

            _company = SapConnectionManager.Instance.GetCompany(forceServiceUser);
            _impSaltaRepository = new PSaltaRepository(_company);
            _configRepository = new SaltaConfigRepository(_company);
            _contDateRepository = new ContDateRepository(_company);
        }

        
        public async Task<ProcessResult> ProcessRecordsAsync(string qValue, string year, IProgress<int> progress = null)
        {
            _logger.Info($"Iniciando procesamiento para {year} - {qValue}...");

            var result = new ProcessResult();

            await LoadImpuestosCacheAsync();

            var stats = await _impSaltaRepository.GetStatsByAnioAsync(qValue, year);
            int total = (stats.ContainsKey("Importado") ? stats["Importado"] : 0) + (stats.ContainsKey("10") ? stats["10"] : 0) +
                        (stats.ContainsKey("Procesado") ? stats["Procesado"] : 0) + (stats.ContainsKey("20") ? stats["20"] : 0) +
                        (stats.ContainsKey("No Encontrado") ? stats["No Encontrado"] : 0) + (stats.ContainsKey("30") ? stats["30"] : 0) +
                        (stats.ContainsKey("Error") ? stats["Error"] : 0) + (stats.ContainsKey("Error") ? stats["40"] : 0);


            await _impSaltaRepository.MarkNonExistentProvidersAsync(qValue, year);

            List<PSaltaRecord> records = await _impSaltaRepository.GetByAnioAsync(qValue, year);

            if (records == null || records.Count == 0)
            {
                _logger.Warn("No se encontraron registros para procesar.");
                return result;
            }

            result.TotalRegistros = total;
            (DateTime desde, DateTime hasta) = await GetDynamicDates(year, qValue);

            int successCount = 0;
            int errorCount = 0;
            int recordCount = records.Count();

            await Task.Run(async () =>
            {
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                for (int i = 0; i < recordCount; i++)
                {
                    var record = records[i];

                    try
                    {
                        if (string.IsNullOrEmpty(record.U_Cuit))
                            throw new Exception("El registro no tiene CUIT.");

                        if (!CuitExistsInSap(record.U_Cuit))
                        {
                            _logger.Warn($"Proveedor con CUIT {record.U_Cuit} no existe. Omitiendo.");
                            await SafeUpdateRecord(record, "30", "Proveedor No Existe", now);
                            errorCount++;
                            continue;
                        }

                        List<ImpuestoCacheItem> taxItems = GetWhtCodesFromCache(record.U_Inscripcion, record.U_Riesgo);

                        if (taxItems.Count == 0)
                        {
                            _logger.Warn($"No existe configuración para Insc: {record.U_Inscripcion} / Riesgo: {record.U_Riesgo}");
                            await SafeUpdateRecord(record, "40", "Configuración Impuesto No Encontrada", now);
                            errorCount++;
                            continue;
                        }

                        string processedCodes = "";

                        foreach (var item in taxItems)
                        {
                            string tipo = "A";
                            double rate = 0.0;
                            if (!int.TryParse(item.U_Codigo, out int taxEntry))
                            {
                                taxEntry = 1; // Valor por defecto si viene vacío o no es número
                            }
                            int linea = _impSaltaRepository.GetNextLineId(taxEntry);

                            string riskFlag = MapRiskToFlag(record.U_Riesgo);

                            var execute = 2;
                            if (execute==0)
                            {
                                _logger.Info($"ExecutePrWtd3 taxEntry:{taxEntry},linea:{linea},item.CodigoSap:{item.CodigoSap},tipo:{tipo},record.U_Cuit:{record.U_Cuit},riskFlag:{riskFlag},rate:{rate},desde:{desde},hasta:{hasta}");
                                _impSaltaRepository.ExecutePrWtd3(
                                    _company,
                                    taxEntry,
                                    linea,
                                    item.CodigoSap,
                                    tipo,
                                    record.U_Cuit,
                                    riskFlag,
                                    rate,
                                    desde,
                                    hasta
                                );
                            } else if (execute==1) {

                                _logger.Info($"ExecuteSpInsertWtd3 taxEntry:{taxEntry},linea:{linea},item.CodigoSap:{item.CodigoSap},record.U_Cuit:{record.U_Cuit},desde:{desde},hasta:{hasta},80,tipo:{tipo}");
                                _impSaltaRepository.ExecuteSpInsertWtd3(
                                    _company,
                                    taxEntry,
                                    linea,
                                    item.CodigoSap,
                                    record.U_Cuit,
                                    desde,
                                    hasta,
                                    "80",
                                    tipo
                                );
                            } else if (execute == 2) {

                                _logger.Info($"InsertWtd3Direct taxEntry:{taxEntry},linea:{linea},item.CodigoSap:{item.CodigoSap},record.U_Cuit:{record.U_Cuit},desde:{desde},hasta:{hasta},80,tipo:{tipo}");
                                _impSaltaRepository.InsertWtd3Direct(
                                    _company,
                                    taxEntry,    
                                    linea,      
                                    item.CodigoSap, 
                                    record.U_Cuit,
                                    desde,
                                    hasta,
                                    "80",       
                                    "A"         
                                );
                            } else if (execute == 3)
                            {
                                _impSaltaRepository.UpsertWtd3Direct(
                                    _company,
                                    taxEntry, 
                                    item.CodigoSap,    
                                    record.U_Cuit,
                                    desde,
                                    hasta,
                                    "80",       // part2
                                    "A"         // detType
                                );
                            } else
                            { // execute == 4 
                                //        public void ExecutePrWtd3Logic(Company company, string entryStr, string wtCode, string tipo, string cuit, string risk, double rate, DateTime desde, DateTime hasta)
                                _logger.Info($"ExecutePrWtd3Logic entryStr:{item.U_Codigo},wtCode:{item.CodigoSap}, tipo:{tipo},cuit:{record.U_Cuit},risk:N,rate:{rate},desde:{desde},hasta:{hasta}");
                                _impSaltaRepository.ExecutePrWtd3Logic(_company, item.U_Codigo, item.CodigoSap, tipo, record.U_Cuit, "N", rate, desde, hasta);
                            }
                            processedCodes += item.CodigoSap + " ";
                        }

                        await SafeUpdateRecord(record, "20", $"Procesado OK. Códigos: {processedCodes.Trim()}", now);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.Error($"Error procesando CUIT {record.U_Cuit}: {ex.Message} {ex.StackTrace}");
                        await SafeUpdateRecord(record, "40", $"Error: Insertando en WDT3", now);
                    }

                    if (progress != null && i % 20 == 0)
                    {
                        int percent = (int)((double)i / recordCount * 100);
                        progress.Report(percent);
                    }
                }
            });
            progress.Report(100);
            await _contDateRepository.DeactivatePeriodAsync(year, qValue);
            result.RegistrosConError = errorCount;
            result.ProcesadosExitosos = result.TotalRegistros - result.RegistrosConError;
            _logger.Info($"Procesamiento finalizado. Total: {result.TotalRegistros}, Exitosos: {result.ProcesadosExitosos}, Errores: {result.RegistrosConError}");
            return result;
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

                    if (notas.Length > 99)
                    {
                        notas = notas.Substring(0, 99);
                    }
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
                _logger.Error($"Fallo CRÍTICO al actualizar estado en SAP para Code {record.Code}. Error original fue ignorado. Causa: {ex.Message}");
            }
        }

        private string MapRiskToFlag(string padronRisk)
        {
            if (string.IsNullOrEmpty(padronRisk)) return "N";
            string risk = padronRisk.Trim().ToUpper();
            if (risk == "RA" || risk == "RB" || risk == "JU") return "Y";
            return "N";
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

        private async Task<(DateTime, DateTime)> GetDynamicDates(string year, string qValue)
        {
            var periodoService = new PeriodosService();
            var fechas = await periodoService.GetDatesAsync(year, qValue);

            if (fechas.Desde.HasValue && fechas.Hasta.HasValue)
            {
                return (fechas.Desde.Value, fechas.Hasta.Value);
            }

            throw new Exception($"No se encontraron fechas configuradas para {year} {qValue}");
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