using PadronWtd.Domain;
using PadronWtd.UI.DI; // Para App.Company si lo necesitas o usa Inyección
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PadronWtd.Repository.DI
{
    public class ContDateRepository : IContDateRepository
    {
        private readonly Company _company;
        private readonly ILogger _logger;

        public ContDateRepository(Company company, ILogger logger)
        {
            _company = company ?? throw new ArgumentNullException(nameof(company));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ContDateRepository(Company company) : this(company, SimpleServiceProvider.Get<ILogger>()) { }

        // -----------------------------------------------------------------------
        // Consulta de Impuestos
        // -----------------------------------------------------------------------
        public async Task<List<ContDateRecord>> GetImpuestosAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<ContDateRecord>();
                Recordset rs = null;

                try
                {
                    rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                    string query = @"
                        SELECT 
                            T0.""Code"" AS ""HeaderCode"", 
                            T1.""Code"" AS ""DetailCode"", 
                            T1.""U_Periodo"", 
                            T1.""U_Desde"", 
                            T1.""U_Hasta"", 
                            T1.""U_Activo"",
                            T0.""U_Detalle"" AS ""Year""
                        FROM ""@CONT_DATE_CAB"" T0, ""@CONT_DATE_DET"" T1
                        WHERE T0.""Code"" = T1.""Code""
                    ";

                    rs.DoQuery(query);

                    while (!rs.EoF)
                    {
                        var rec = new ContDateRecord
                        {
                            HeaderCode = GetValue(rs, "HeaderCode"),
                            DetailCode = GetValue(rs, "DetailCode"),
                            U_Periodo = GetValue(rs, "U_Periodo"),
                            U_Activo = GetValue(rs, "U_Activo"),
                            U_Desde = GetDateValue(rs, "U_Desde"),
                            U_Hasta = GetDateValue(rs, "U_Hasta")
                        };

                        list.Add(rec);
                        rs.MoveNext();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en GetImpuestosAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (rs != null) Marshal.ReleaseComObject(rs);
                }

                return list;
            });
        }

        public async Task<List<ContDateRecord>> GetFechasAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<ContDateRecord>();
                Recordset rs = null;

                try
                {
                    rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                    string query = @"
                        SELECT 
                            T0.""Code"" AS ""HeaderCode"", 
                            T0.""U_Detalle"" AS ""Year"",
                            T1.""Code"" AS ""DetailCode"", 
                            T1.""U_Periodo"", 
                            T1.""U_Desde"", 
                            T1.""U_Hasta"", 
                            T1.""U_Activo"" 
                        FROM ""@CONT_DATE_CAB"" T0, ""@CONT_DATE_DET"" T1
                        WHERE T0.""Code"" = T1.""Code""
                        ORDER BY 2,4 DESC
                    ";

                    rs.DoQuery(query);

                    while (!rs.EoF)
                    {
                        var rec = new ContDateRecord
                        {
                            HeaderCode = GetValue(rs, "HeaderCode"),
                            Year = GetValue(rs, "Year"),
                            DetailCode = GetValue(rs, "DetailCode"),
                            U_Periodo = GetValue(rs, "U_Periodo"),
                            U_Activo = GetValue(rs, "U_Activo"),
                            U_Desde = GetDateValue(rs, "U_Desde"),
                            U_Hasta = GetDateValue(rs, "U_Hasta")
                        };

                        list.Add(rec);
                        rs.MoveNext();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en GetFechasAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (rs != null) Marshal.ReleaseComObject(rs);
                }

                return list;
            });
        }

        // -----------------------------------------------------------------------
        // Desactivar Periodo: Pone U_Activo = 'NO' para un Año y Q específicos
        // -----------------------------------------------------------------------
        public async Task DeactivatePeriodAsync(string year, string qValue)
        {
            await Task.Run(() =>
            {
                Recordset rs = null;
                try
                {
                    rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                    string findQuery = $@"
                        SELECT T1.""Code"", T1.""LineId""
                        FROM ""@CONT_DATE_CAB"" T0
                        INNER JOIN ""@CONT_DATE_DET"" T1 ON T0.""Code"" = T1.""Code""
                        WHERE T0.""U_Detalle"" = '{year}' 
                        AND T1.""U_Periodo"" = '{qValue}'";

                    rs.DoQuery(findQuery);

                    if (!rs.EoF)
                    {
                        string codeToUpdate = rs.Fields.Item("Code").Value.ToString();
                        int lineIdToUpdate = int.Parse(rs.Fields.Item("LineId").Value.ToString());

                        string updateQuery = $@"
                            UPDATE ""@CONT_DATE_DET""
                            SET ""U_Activo"" = 'NO'
                            WHERE ""Code"" = '{codeToUpdate}' 
                            AND ""LineId"" = {lineIdToUpdate}";

                        rs.DoQuery(updateQuery);

                        _logger.Info($"Periodo desactivado: Año {year}, Q {qValue}");
                    }
                    else
                    {
                        _logger.Warn($"No se encontró el periodo para desactivar: Año {year}, Q {qValue}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en DeactivatePeriodAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (rs != null) Marshal.ReleaseComObject(rs);
                }
            });
        }

        // --- Helpers Privados ---

        private string GetValue(Recordset rs, string colName)
        {
            try { return rs.Fields.Item(colName).Value?.ToString() ?? ""; }
            catch { return ""; }
        }

        private DateTime? GetDateValue(Recordset rs, string colName)
        {
            try
            {
                var val = rs.Fields.Item(colName).Value;
                if (val is DateTime dt) return dt;
                return null;
            }
            catch { return null; }
        }
    }
}