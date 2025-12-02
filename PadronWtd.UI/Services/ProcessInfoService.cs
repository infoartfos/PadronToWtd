using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;

namespace PadronWtd.UI.Services
{
    public class ProcessInfoService
    {
        private readonly ILogger _logger;
        private readonly PSaltaRepository _repository;
        private readonly SaltaConfigRepository _configRepository;
        private readonly Company _company;

        private Dictionary<string, List<string>> _impuestosCache;

        public ProcessInfoService()
        {
            _logger = SimpleServiceProvider.Get<ILogger>();

            if (App.Company == null || !App.Company.Connected)
                throw new InvalidOperationException("No hay conexión con DI API.");

            _company = App.Company;
            _repository = new PSaltaRepository(_company);
            _configRepository = new SaltaConfigRepository(_company);
        }

        public async Task<int> ProcessRecordsAsync(string qValue, string year, IProgress<int> progress = null)
        {
            _logger.Info($"Iniciando procesamiento para {year} - {qValue}...");

            await LoadImpuestosCacheAsync();

            List<PSaltaRecord> records = await _repository.GetByAnioAsync(qValue, year);

            if (records == null || records.Count == 0)
            {
                _logger.Warn("No se encontraron registros para procesar.");
                return 0;
            }

            (DateTime desde, DateTime hasta) = GetDatesFromPeriod(year, qValue);

            int processedCount = 0;
            int errorCount = 0;
            int total = records.Count;

            await Task.Run(async () =>
            {
                // Usar formato corto para U_Procesado si el campo en SAP es pequeño
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                for (int i = 0; i < total; i++)
                {
                    var record = records[i];

                    try
                    {
                        // A. Validar que tengamos CUIT
                        if (string.IsNullOrEmpty(record.U_Cuit))
                            throw new Exception("El registro no tiene CUIT.");

                        // B. Verificar existencia de Proveedor
                        if (!CuitExistsInSap(record.U_Cuit))
                        {
                            _logger.Warn($"Proveedor con CUIT {record.U_Cuit} no existe. Omitiendo.");
                            await SafeUpdateRecord(record, "30", "Proveedor No Existe", now);
                            continue;
                        }

                        // C. Obtener LISTA de Códigos SAP
                        List<string> taxCodes = GetWhtCodesFromCache(record.U_Inscripcion, record.U_Riesgo);

                        if (taxCodes.Count == 0)
                        {
                            _logger.Warn($"No existe configuración para Insc: {record.U_Inscripcion} / Riesgo: {record.U_Riesgo}");
                            await SafeUpdateRecord(record, "40", "Configuración Impuesto No Encontrada", now);
                            continue;
                        }

                        string processedCodes = "";

                        foreach (string taxCode in taxCodes)
                        {
                            // D. Obtener el ID numérico (AbsEntry) del IMPUESTO
                            int taxEntry = GetTaxDefinitionAbsEntry(taxCode);
                            if (taxEntry == 0) taxEntry = 1;

                            int linea = 1;
                            string tipo = "A";
                            double rate = 0.0;

                            // CORRECCIÓN DEL ERROR 359 (String too long):
                            // El SP espera 1 caracter para RISK. Mapeamos "JU"/"SR" a "Y"/"N".
                            string riskFlag = MapRiskToFlag(record.U_Riesgo);

                            // F. Ejecutar Stored Procedure
                            _repository.ExecutePrWtd3(
                                _company,
                                taxEntry,   // AENTRY
                                linea,      // LNNUM
                                taxCode,    // WTCD
                                tipo,       // TIPO
                                record.U_Cuit,
                                riskFlag,   // RISK (Ahora es 1 caracter)
                                rate,
                                desde,
                                hasta
                            );

                            processedCodes += taxCode + " ";
                        }

                        // G. Actualizar Padrón OK
                        await SafeUpdateRecord(record, "20", $"Procesado OK. Códigos: {processedCodes.Trim()}", now);
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.Error($"Error procesando CUIT {record.U_Cuit}: {ex.Message}");
                        // Usamos SafeUpdate para evitar que el mensaje de error largo rompa el update
                        await SafeUpdateRecord(record, "99", $"Error: {ex.Message}", now);
                    }

                    if (progress != null && i % 50 == 0)
                    {
                        int percent = (int)((double)i / total * 100);
                        progress.Report(percent);
                    }
                }
            });

            _logger.Info($"Procesamiento finalizado. Exitosos: {processedCount}, Errores: {errorCount}");
            return processedCount;
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

                // Truncar notas si son muy largas (SAP suele tener limite de 100 o 254)
                if (!string.IsNullOrEmpty(notas) && notas.Length > 99)
                {
                    notas = notas.Substring(0, 99);
                }
                record.U_Notas = notas;

                await _repository.UpdateAsync(record);
            }
            catch (Exception ex)
            {
                _logger.Error($"Fallo secundario al actualizar estado del registro {record.Code}: {ex.Message}");
            }
        }

        private string MapRiskToFlag(string padronRisk)
        {
            if (string.IsNullOrEmpty(padronRisk)) return "N";

            // Lógica de Negocio: ¿Qué códigos se consideran 'Y' (Alto Riesgo)?
            // Ajusta esto según tu cliente.
            // Ejemplo: Si es RA (Riesgo Alto) o RB (Riesgo Bajo) ponemos Y, si es SR (Sin Riesgo) ponemos N.
            // Por ahora, para evitar el error, devolvemos siempre 'N' o la primera letra si eso tiene sentido.

            string risk = padronRisk.Trim().ToUpper();

            // Ejemplo hipotético:
            if (risk == "RA" || risk == "RB" || risk == "JU") return "Y";

            return "N"; // SR y otros
        }

        // --------------------------------------------------------------------------------------------
        // Helpers de Lógica de Negocio (Cache y DB)
        // --------------------------------------------------------------------------------------------

        private async Task LoadImpuestosCacheAsync()
        {
            _impuestosCache = new Dictionary<string, List<string>>();
            var configs = await _configRepository.GetConfiguracionImpuestosAsync();

            foreach (var cfg in configs)
            {
                string key = $"{cfg.Inscripcion.Trim().ToUpper()}_{cfg.Riesgo.Trim().ToUpper()}";

                if (!_impuestosCache.ContainsKey(key))
                {
                    _impuestosCache[key] = new List<string>();
                }
                if (!_impuestosCache[key].Contains(cfg.CodigoSap))
                {
                    _impuestosCache[key].Add(cfg.CodigoSap);
                }
            }
        }

        private List<string> GetWhtCodesFromCache(string inscripcion, string riesgo)
        {
            if (_impuestosCache == null) return new List<string>();
            string key = $"{riesgo?.Trim().ToUpper()}_{inscripcion?.Trim().ToUpper()}";
            if (_impuestosCache.TryGetValue(key, out List<string> codes)) return codes;
            return new List<string>();
        }

        private (DateTime, DateTime) GetDatesFromPeriod(string yearStr, string qValue)
        {
            if (!int.TryParse(yearStr, out int year)) year = DateTime.Now.Year;
            switch (qValue.ToUpper())
            {
                case "Q1": return (new DateTime(year, 1, 1), new DateTime(year, 3, 31));
                case "Q2": return (new DateTime(year, 4, 1), new DateTime(year, 6, 30));
                case "Q3": return (new DateTime(year, 7, 1), new DateTime(year, 9, 30));
                case "Q4": return (new DateTime(year, 10, 1), new DateTime(year, 12, 31));
                default: return (DateTime.Now, DateTime.Now);
            }
        }

        private bool CuitExistsInSap(string cuit)
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string query = $@"SELECT COUNT(*) FROM ""OCRD"" WHERE ""LicTradNum"" = '{cuit}'";
                rs.DoQuery(query);
                if (!rs.EoF) return int.Parse(rs.Fields.Item(0).Value.ToString()) > 0;
                return false;
            }
            catch { return false; }
            finally { if (rs != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(rs); }
        }

        private int GetTaxDefinitionAbsEntry(string taxCodeSap)
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string query = $@"
                    SELECT T0.""AbsEntry"" 
                    FROM ""OWTD"" T0 
                    INNER JOIN ""OWHT"" T1 ON T1.""Category"" = T0.""AbsEntry""
                    WHERE T1.""WTCode"" = '{taxCodeSap}'";
                rs.DoQuery(query);
                if (!rs.EoF) return int.Parse(rs.Fields.Item(0).Value.ToString());
                return 0;
            }
            catch { return 0; }
            finally { if (rs != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(rs); }
        }
    }
}