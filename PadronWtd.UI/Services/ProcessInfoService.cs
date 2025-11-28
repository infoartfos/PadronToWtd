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
        private readonly Company _company;
        private Task<Task<string>> code;

        public ProcessInfoService()
        {
            _logger = SimpleServiceProvider.Get<ILogger>();

            if (App.Company == null || !App.Company.Connected)
                throw new InvalidOperationException("No hay conexión con DI API.");

            _company = App.Company;
            _repository = new PSaltaRepository(_company);
        }

        public async Task<int> ProcessRecordsAsync(string qValue, string year, IProgress<int> progress = null)
        {
            _logger.Info($"Iniciando procesamiento para {year} - {qValue}...");

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
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                for (int i = 0; i < total; i++)
                {
                    var record = records[i];

                    try
                    {
                        // A. Validar que tengamos CUIT
                        if (string.IsNullOrEmpty(record.U_Cuit))
                            throw new Exception("El registro no tiene CUIT.");

                        string bpEntry = GetBpAbsEntryByCuit(record.U_Cuit);

                        if (bpEntry == "0")
                        {
                            _logger.Warn($"Proveedor con CUIT {record.U_Cuit} no encontrado en SAP. Omitiendo.");
                            record.U_Procesado = now;
                            record.U_Notas = "Procesado OK - No estaba cuit";
                            record.U_Estado = "30";
                            string cod = await _repository.UpdateAsync(record);
                            continue;
                        }


                        int wddCode = GetWhtCodeByRisk(record.U_Riesgo);

                        int linea = 1; // ¿Es secuencial por proveedor? ¿O fijo? (Ajustar según necesidad)
                        string part2 = "80"; // Valor fijo solicitado
                        string detType = "A"; // Valor fijo solicitado

                        _repository.ExecuteSpInsertWtd3(
                            _company,
                            "123123",// bpEntry, 
                            linea,
                            wddCode,
                            record.U_Cuit,
                            desde,
                            hasta,
                            part2,
                            detType
                        );

                        record.U_Procesado = now;
                        record.U_Estado = "20";
                        record.U_Notas = "Procesado OK";
                        string code = await _repository.UpdateAsync(record);

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        record.U_Procesado = now;
                        record.U_Estado = "40";
                        record.U_Notas = "Error";
                        string code = await _repository.UpdateAsync(record);
                        _logger.Error($"Error procesando CUIT {record.U_Cuit}: {ex.Message}");
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
        // Helpers de Lógica de Negocio
        // --------------------------------------------------------------------------------------------

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

        /// <summary>
        /// Busca el AbsEntry (Clave Primaria Interna) del Socio de Negocio dado su CUIT.
        /// Tabla: OCRD, Campo: LicTradNum (CUIT)
        /// </summary>
        private string GetBpAbsEntryByCuit(string cuit)
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                // Limpiamos el CUIT de guiones por si acaso en SAP está sin ellos o viceversa
                // Asumimos que viene limpio o coincide exacto.
                string query = $@"SELECT ""LicTradNum"" FROM ""OCRD"" WHERE ""LicTradNum"" = '{cuit}' AND Substring(""CardCode"",1,2)='PL' ";
                _logger.Info(query);
                rs.DoQuery(query);

                if (!rs.EoF)
                {
                    return rs.Fields.Item(0).Value.ToString();
                }
                return "0"; // No encontrado
            }
            catch (Exception ex)
            {
                _logger.Error($"Error buscando BP por CUIT {cuit}: {ex.Message} {ex.StackTrace}");
                return "0";
            }
            finally
            {
                if (rs != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(rs);
            }
        }

        /// <summary>
        /// Mapea el valor de Riesgo/Categoría del padrón a un código interno de retención de SAP (OWHT).
        /// </summary>
        private int GetWhtCodeByRisk(string riesgo)
        {
            // TODO: Implementar lógica real de negocio.
            // Ejemplo: Buscar en una tabla de configuración o hardcodear según requerimiento.

            switch (riesgo?.Trim().ToUpper())
            {
                case "JU": return 1; // Ejemplo: ID 1 en OWHT
                case "SR": return 2;
                case "RA": return 3;
                case "RB": return 4;
                default: return 5;   // Valor por defecto
            }
        }
    }
}