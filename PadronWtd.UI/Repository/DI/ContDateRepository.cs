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
    public class ContDateRepository
    {
        private readonly Company _company;
        private readonly ILogger _logger;

        public ContDateRepository(Company company)
        {
            _company = company ?? throw new ArgumentNullException(nameof(company));
            _logger = SimpleServiceProvider.Get<ILogger>();
        }

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
                            T1.""U_Activo"" 
                        FROM ""@CONT_DATE_CAB"" T0, ""@CONT_DATE_DET"" T1
                        -- SUGERENCIA: Descomentar la siguiente línea si se duplican los datos
                        -- WHERE T0.""Code"" = T1.""Code""
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

        // -----------------------------------------------------------------------
        // Consulta de Fechas (Idéntica estructura a Impuestos según tu pedido)
        // -----------------------------------------------------------------------
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
                            T0.""Name"" AS ""Year"",
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