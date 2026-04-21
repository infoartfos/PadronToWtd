using PadronWtd.Domain;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PadronWtd.Repository.DI
{
    public class PSaltaRepository
    {
        private readonly ILogger _logger;
        private readonly Company _company;
        private const string TABLE_NAME = "PADRON_SALTA_IMP3";
        private const string DB_TABLE_NAME = "@" + TABLE_NAME;

        public PSaltaRepository(Company company)
        {
            _logger = SimpleServiceProvider.Get<ILogger>();
            _company = company ?? throw new ArgumentNullException(nameof(company));
            if (!_company.Connected)
                throw new InvalidOperationException("La conexión a SAP Business One no está activa.");
        }

        // -----------------------------------------------------------------------
        // PROTECCIÓN: Truncar strings para evitar "Value too large for column"
        // -----------------------------------------------------------------------
        private string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("'", "''").Replace("\r", "").Replace("\n", "").Replace("\t", " ").Trim();
        }

        private string SafeSubstring(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string cleaned = Sanitize(text);
            return cleaned.Length <= maxLength ? cleaned : cleaned.Substring(0, maxLength);
        }

        // -----------------------------------------------------------------------
        // GET ALL
        // -----------------------------------------------------------------------
        public async Task<List<PSaltaRecord>> GetAllAsync()
        {
            return await Task.Run(() =>
            {
                var records = new List<PSaltaRecord>();
                Recordset recordset = null;
                try
                {
                    recordset = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                    string query = $@"
                        SELECT 
                            ""Code"", ""Name"", ""DocEntry"", ""U_Anio"", ""U_Padron"", 
                            ""U_Cuit"", ""U_Inscripcion"", ""U_Riesgo"", ""U_Notas"", 
                            ""U_Procesado"", ""U_Estado""
                        FROM ""{DB_TABLE_NAME}"" 
                        ORDER BY ""DocEntry"" DESC";

                    recordset.DoQuery(query);

                    while (!recordset.EoF)
                    {
                        records.Add(new PSaltaRecord
                        {
                            Code = GetValue(recordset, "Code"),
                            Name = GetValue(recordset, "Name"),
                            DocEntry = int.Parse(GetValue(recordset, "DocEntry", "0")),
                            U_Anio = GetValue(recordset, "U_Anio"),
                            U_Padron = GetValue(recordset, "U_Padron"),
                            U_Cuit = GetValue(recordset, "U_Cuit"),
                            U_Inscripcion = GetValue(recordset, "U_Inscripcion"),
                            U_Riesgo = GetValue(recordset, "U_Riesgo"),
                            U_Notas = GetValue(recordset, "U_Notas"),
                            U_Procesado = GetValue(recordset, "U_Procesado"),
                            U_Estado = GetValue(recordset, "U_Estado")
                        });
                        recordset.MoveNext();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en GetAllAsync: {ex.Message}");
                    throw;
                }
                finally { if (recordset != null) Marshal.ReleaseComObject(recordset); }
                return records;
            });
        }


        public async Task<string> UpdateAsync(PSaltaRecord r)
        {
            return await Task.Run(() =>
            {
                UserTable userTable = null;
                try
                {
                    userTable = _company.UserTables.Item(TABLE_NAME);

                    if (userTable.GetByKey(r.Code))
                    {
                        userTable.Name = r.Name;

                        // Actualizar campos UDF
                        userTable.UserFields.Fields.Item("U_Anio").Value = r.U_Anio ?? "";
                        userTable.UserFields.Fields.Item("U_Padron").Value = r.U_Padron ?? "";
                        userTable.UserFields.Fields.Item("U_Cuit").Value = r.U_Cuit ?? "";
                        userTable.UserFields.Fields.Item("U_Inscripcion").Value = r.U_Inscripcion ?? "";
                        userTable.UserFields.Fields.Item("U_Riesgo").Value = r.U_Riesgo ?? "";
                        userTable.UserFields.Fields.Item("U_Notas").Value = r.U_Notas ?? "";
                        userTable.UserFields.Fields.Item("U_Procesado").Value = r.U_Procesado ?? "";
                        userTable.UserFields.Fields.Item("U_Estado").Value = r.U_Estado ?? "";

                        int result = userTable.Update();
                        if (result != 0)
                        {
                            string errMsg = _company.GetLastErrorDescription();
                            throw new Exception($"Error al actualizar ({result}): {errMsg}");
                        }

                        return r.Code;
                    }
                    else
                    {
                        throw new Exception($"Registro con Code '{r.Code}' no encontrado en {DB_TABLE_NAME}.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en UpdateAsync: {ex.Message} {ex.StackTrace}");
                    throw;
                }
                finally
                {
                    if (userTable != null) Marshal.ReleaseComObject(userTable);
                }
            });
        }

        public async Task<List<PSaltaRecord>> GetByAnioAsync(string q_value, string anio)
        {
            return await Task.Run(() =>
            {
                var records = new List<PSaltaRecord>();
                Recordset recordset = null;

                try
                {
                    recordset = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                    // Quitamos DocEntry, Canceled, Object, UserSign, CreateDate, DataSource 
                    // porque no existen en tablas tipo "Ninguno"
                    string query = $@"
                SELECT 
                    ""Code"", ""Name"", 
                    ""U_Anio"", ""U_Padron"", ""U_Cuit"", ""U_Inscripcion"", 
                    ""U_Riesgo"", ""U_Notas"", ""U_Procesado"", ""U_Estado""
                FROM ""{DB_TABLE_NAME}"" 
                WHERE ""U_Anio"" = '{anio}'
                AND ""Name"" = '{q_value}'
                AND (""U_Estado"" = 'Importado' OR ""U_Estado"" = '10' OR ""U_Estado"" = 'Error' OR ""U_Estado"" = '40')
                ORDER BY ""Code"" ASC";

                    _logger.Info($"Ejecutando lectura para procesamiento...");
                    recordset.DoQuery(query);

                    while (!recordset.EoF)
                    {
                        records.Add(new PSaltaRecord
                        {
                            Code = GetValue(recordset, "Code"),
                            Name = GetValue(recordset, "Name"),
                            // Si necesitas el DocEntry pero no existe la columna, 
                            // puedes usar el Code si es numérico
                            U_Anio = GetValue(recordset, "U_Anio"),
                            U_Padron = GetValue(recordset, "U_Padron"),
                            U_Cuit = GetValue(recordset, "U_Cuit"),
                            U_Inscripcion = GetValue(recordset, "U_Inscripcion"),
                            U_Riesgo = GetValue(recordset, "U_Riesgo"),
                            U_Notas = GetValue(recordset, "U_Notas"),
                            U_Procesado = GetValue(recordset, "U_Procesado"),
                            U_Estado = GetValue(recordset, "U_Estado")
                        });
                        recordset.MoveNext();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en GetByAnioAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (recordset != null) Marshal.ReleaseComObject(recordset);
                }
                return records;
            });
        }

        public async Task<bool> ExistsByAnioAndQAsync(string q_value, string anio)
        {
            return await Task.Run(() =>
            {
                Recordset recordset = null;
                try
                {
                    recordset = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                    string query = $@"
                        SELECT TOP 1 ""Code"" 
                        FROM ""{DB_TABLE_NAME}"" 
                        WHERE ""U_Anio"" = '{anio}' 
                        AND ""Name"" = '{q_value}'";

                    recordset.DoQuery(query);

                    return !recordset.EoF;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en ExistsByAnioAndQAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (recordset != null) Marshal.ReleaseComObject(recordset);
                }
            });
        }


        // -----------------------------------------------------------------------
        // BULK INSERT: Con GUID para Code y Período para Name
        // -----------------------------------------------------------------------
        public async Task BulkInsertAsync(List<PSaltaRecord> records, IProgress<int> progress = null)
        {
            if (records == null || !records.Any()) return;

            await Task.Run(() =>
            {
                Recordset oRS = null;
                try
                {
                    oRS = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                    int batchSize = 500;
                    int totalRecords = records.Count;
                    int processed = 0;

                    // 1. Obtenemos el punto de partida real de la base de datos (una sola vez)
                    long currentIdCounter = GetMaxCode() + 1;

                    string periodName = records.First().Name ?? "T01";

                    while (processed < totalRecords)
                    {
                        var batch = records.Skip(processed).Take(batchSize).ToList();
                        if (batch.Any())
                        {
                            // 2. Pasamos el contador actual al constructor del SQL
                            string sql = BuildHanaInsertBatch(batch, periodName, currentIdCounter);
                            oRS.DoQuery(sql);

                            // 3. Incrementamos el contador por la cantidad de registros insertados
                            currentIdCounter += batch.Count;
                        }

                        processed += batch.Count;
                        if (progress != null) progress.Report((processed * 100) / totalRecords);
                        _logger.Info($"Insertados: {processed} / {totalRecords}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en BulkInsert: {ex.Message}");
                    throw;
                }
                finally { if (oRS != null) Marshal.ReleaseComObject(oRS); }
            });
        }

        private string BuildHanaInsertBatch(List<PSaltaRecord> batch, string periodName, long startCode)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"INSERT INTO \"{DB_TABLE_NAME}\" ");
            sb.Append("(\"Code\", \"Name\", \"U_Anio\", \"U_Padron\", \"U_Cuit\", \"U_Inscripcion\", \"U_Riesgo\", \"U_Estado\", \"U_Notas\") ");

            for (int i = 0; i < batch.Count; i++)
            {
                var r = batch[i];

                // GENERACIÓN SECUENCIAL PURA: startCode + i
                string uniqueCode = (startCode + i).ToString();
                string rowName = SafeSubstring(periodName, 50);

                if (i > 0) sb.Append(" UNION ALL ");
                sb.Append(" SELECT ");

                if (i == 0)
                    sb.Append($"CAST('{uniqueCode}' AS NVARCHAR(50)), CAST('{rowName}' AS NVARCHAR(100)), ");
                else
                    sb.Append($"'{uniqueCode}', '{rowName}', ");

                // --- LÍMITES BASADOS EN TU IMAGEN DE SAP ---
                sb.Append($"'{SafeSubstring(r.U_Anio, 4)}', ");
                sb.Append($"'{SafeSubstring(r.U_Padron, 250)}', ");
                sb.Append($"'{SafeSubstring(r.U_Cuit, 11)}', ");
                sb.Append($"'{SafeSubstring(r.U_Inscripcion, 2)}', ");
                sb.Append($"'{SafeSubstring(r.U_Riesgo, 2)}', ");
                sb.Append("'10', "); // Estado: "10" para que quepa en Alfanumérico(2)
                sb.Append($"'{SafeSubstring(r.U_Notas, 50)}' ");

                sb.Append(" FROM DUMMY ");
            }
            return sb.ToString();
        }

        private long GetMaxCode()
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                // Buscamos el Code más alto convertido a número para no chocar
                string sql = $"SELECT MAX(CAST(\"Code\" AS BIGINT)) FROM \"{DB_TABLE_NAME}\"";
                rs.DoQuery(sql);
                if (!rs.EoF && rs.Fields.Item(0).Value != null)
                {
                    return Convert.ToInt64(rs.Fields.Item(0).Value.ToString());
                }
                return 0;
            }
            catch { return 0; }
            finally { if (rs != null) Marshal.ReleaseComObject(rs); }
        }

        // -----------------------------------------------------------------------
        // OTROS MÉTODOS PÚBLICOS
        // -----------------------------------------------------------------------
        public async Task<int> MarkNonExistentProvidersAsync(string qValue, string year)
        {
            return await Task.Run(() =>
            {
                Recordset rs = null;
                try
                {
                    rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                    string updateQuery = $@"
                        UPDATE ""{DB_TABLE_NAME}""
                        SET ""U_Estado"" = '30',
                            ""U_Procesado"" = TO_VARCHAR(CURRENT_TIMESTAMP, 'YYYY-MM-DD HH24:MI'),
                            ""U_Notas"" = 'Proveedor No Existe (pl)'
                        WHERE ""U_Anio"" = '{year}' AND ""Name"" = '{qValue}'
                        AND (""U_Estado"" = 'Importado' OR ""U_Estado"" = '10')
                        AND NOT EXISTS (
                            SELECT 1 FROM ""OCRD"" T0 
                            WHERE T0.""LicTradNum"" = ""{DB_TABLE_NAME}"".""U_Cuit""
                            AND UPPER(T0.""CardCode"") LIKE 'PL%'
                        )";
                    rs.DoQuery(updateQuery);
                    return 0;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en MarkNonExistentProvidersAsync: {ex.Message}");
                    throw;
                }
                finally { if (rs != null) Marshal.ReleaseComObject(rs); }
            });
        }

        public async Task DeleteByAnioAndQAsync(string q_value, string anio)
        {
            await Task.Run(() =>
            {
                Recordset rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                try
                {
                    string sql = $@"DELETE FROM ""{DB_TABLE_NAME}"" WHERE ""U_Anio"" = '{anio}' AND ""Name"" = '{q_value}'";
                    _logger.Info($"Borrando anteriores: {sql}");
                    rs.DoQuery(sql);
                }
                finally { Marshal.ReleaseComObject(rs); }
            });
        }

        public async Task<int> CountErrorsAsync(string qValue, string year)
        {
            return await Task.Run(() =>
            {
                Recordset rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                try
                {
                    string query = $@"SELECT COUNT(*) FROM ""{DB_TABLE_NAME}"" 
                                     WHERE ""U_Anio"" = '{year}' AND ""Name"" = '{qValue}'
                                     AND ""U_Estado"" NOT IN ('30', '20', '10', 'Importado', 'Pendiente')";
                    rs.DoQuery(query);
                    return (!rs.EoF) ? int.Parse(rs.Fields.Item(0).Value.ToString()) : 0;
                }
                catch { return 0; }
                finally { Marshal.ReleaseComObject(rs); }
            });
        }

        public async Task ResetErrorRecordsAsync(string qValue, string year)
        {
            await Task.Run(() =>
            {
                Recordset rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                try
                {
                    string sql = $@"UPDATE ""{DB_TABLE_NAME}"" SET ""U_Estado"" = '10', ""U_Notas"" = 'Reprocesamiento', ""U_Procesado"" = NULL 
                                   WHERE ""U_Anio"" = '{year}' AND ""Name"" = '{qValue}' AND ""U_Estado"" NOT IN ('20', '10')";
                    rs.DoQuery(sql);
                }
                finally { Marshal.ReleaseComObject(rs); }
            });
        }
        public void ExecuteSpInsertWtd3(Company company, int entry, int? linea, string wddCode, string cuit, DateTime desde, DateTime hasta, string part2, string detType)
        {
            Recordset oRecordset = null;

            try
            {
                oRecordset = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);

                string fDesde = desde.ToString("yyyyMMdd");
                string fHasta = hasta.ToString("yyyyMMdd");

                string sqlEntry = entry.ToString();

                string sqlLinea = linea.HasValue ? linea.Value.ToString() : "NULL";


                string query = $@"
                    CALL ""SBP_SIOC_CHAR"".""SP_INSERT_WTD3"" (
                        {entry}, 
                        {sqlLinea}, 
                        '{wddCode}', 
                        '{cuit}', 
                        '{fDesde}', 
                        '{fHasta}', 
                        '{part2}', 
                        '{detType}'
                    )";
                _logger.Info(query);
                oRecordset.DoQuery(query);

            }
            catch (Exception ex)
            {
                _logger.Error($"Error al ejecutar SP_INSERT_WTD3: {ex.Message} {ex.StackTrace}");
                throw new Exception($"Error al ejecutar SP_INSERT_WTD3: {ex.Message}");
            }
            finally
            {
                if (oRecordset != null)
                {
                    Marshal.ReleaseComObject(oRecordset);
                    oRecordset = null;
                }
            }
        }


        public void ExecutePrWtd3(Company company, int absEntry, int lineNum, string wtCode, string tipo, string cuit, string risk, double rate, DateTime desde, DateTime hasta)
        {
            Recordset oRecordset = null;
            try
            {
                oRecordset = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);

                string fDesde = desde.ToString("yyyyMMdd");
                string fHasta = hasta.ToString("yyyyMMdd");

                string sqlRate = rate.ToString(System.Globalization.CultureInfo.InvariantCulture);

                string query = $@"
                CALL ""SBP_SIOC_CHAR"".""PR_WTD3"" (
                    {absEntry}, 
                    {lineNum}, 
                    '{wtCode}', 
                    '{tipo}', 
                    '{cuit}', 
                    '{risk}', 
                    {sqlRate}, 
                    '{fDesde}', 
                    '{fHasta}'
                )";

                // _logger.Info("SP: " + query); 
                oRecordset.DoQuery(query);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error ejecutando PR_WTD3: {ex.Message} {ex.StackTrace}");
                throw new Exception($"Error ejecutando PR_WTD3: {ex.Message} {ex.StackTrace}");
            }
            finally
            {
                if (oRecordset != null) Marshal.ReleaseComObject(oRecordset);
            }
        }


        //public async Task<List<PSaltaRecord>> GetByAnioAsync(string q_value, string anio)
        //{
        //    return await Task.Run(() =>
        //    {
        //        var records = new List<PSaltaRecord>();
        //        Recordset rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
        //        try
        //        {
        //            string query = $@"SELECT * FROM ""{DB_TABLE_NAME}"" WHERE ""U_Anio""='{anio}' AND ""Name""='{q_value}'
        //                             AND ""U_Estado"" IN ('Importado','10','Error','40') ORDER BY ""DocEntry"" ASC";
        //            rs.DoQuery(query);
        //            while (!rs.EoF)
        //            {
        //                records.Add(new PSaltaRecord
        //                {
        //                    Code = GetValue(rs, "Code"),
        //                    Name = GetValue(rs, "Name"),
        //                    U_Anio = GetValue(rs, "U_Anio"),
        //                    U_Padron = GetValue(rs, "U_Padron"),
        //                    U_Cuit = GetValue(rs, "U_Cuit"),
        //                    U_Inscripcion = GetValue(rs, "U_Inscripcion"),
        //                    U_Riesgo = GetValue(rs, "U_Riesgo"),
        //                    U_Notas = GetValue(rs, "U_Notas"),
        //                    U_Procesado = GetValue(rs, "U_Procesado"),
        //                    U_Estado = GetValue(rs, "U_Estado")
        //                });
        //                rs.MoveNext();
        //            }
        //        }
        //        finally { Marshal.ReleaseComObject(rs); }
        //        return records;
        //    });
        //}
        public async Task<Dictionary<string, int>> GetStatsByAnioAsync(string qValue, string year)
        {
            return await Task.Run(() =>
            {
                var stats = new Dictionary<string, int>();
                Recordset rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                try
                {
                    string query = $@"SELECT ""U_Estado"", COUNT(*) FROM ""{DB_TABLE_NAME}"" 
                                     WHERE ""U_Anio"" = '{year}' AND ""Name"" = '{qValue}' GROUP BY ""U_Estado""";
                    _logger.Info(query);
                    rs.DoQuery(query);
                    while (!rs.EoF)
                    {
                        stats.Add(rs.Fields.Item(0).Value.ToString(), int.Parse(rs.Fields.Item(1).Value.ToString()));
                        rs.MoveNext();
                    }
                }
                finally { Marshal.ReleaseComObject(rs); }
                return stats;
            });
        }



        //public async Task<bool> ExistsByAnioAndQAsync(string q_value, string anio)
        //{
        //    return await Task.Run(() =>
        //    {
        //        Recordset rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
        //        try
        //        {
        //            rs.DoQuery($@"SELECT TOP 1 ""Code"" FROM ""{DB_TABLE_NAME}"" WHERE ""U_Anio""='{anio}' AND ""Name""='{q_value}'");
        //            return !rs.EoF;
        //        }
        //        finally { Marshal.ReleaseComObject(rs); }
        //    });
        //}

        //public async Task<int> MarkNonExistentProvidersAsync(string qValue, string year)
        //{
        //    return await Task.Run(() =>
        //    {
        //        Recordset rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
        //        try
        //        {
        //            string sql = $@"UPDATE ""{DB_TABLE_NAME}"" SET ""U_Estado""='30', ""U_Notas""='Proveedor No Existe (pl)', ""U_Procesado""=TO_VARCHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI')
        //                           WHERE ""U_Anio""='{year}' AND ""Name""='{qValue}' AND (""U_Estado""='Importado' OR ""U_Estado""='10')
        //                           AND NOT EXISTS (SELECT 1 FROM ""OCRD"" T0 WHERE T0.""LicTradNum""=""{DB_TABLE_NAME}"".""U_Cuit"" AND UPPER(T0.""CardCode"") LIKE 'PL%')";
        //            rs.DoQuery(sql);
        //            return 0;
        //        }
        //        finally { Marshal.ReleaseComObject(rs); }
        //    });
        //}
        public void InsertWtd3Direct(Company company, int entry, int? linea, string wddCode, string cuit, DateTime desde, DateTime hasta, string part2, string detType)
        {
            Recordset oRecordset = null;
            try
            {
                oRecordset = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);

                string fDesde = desde.ToString("yyyyMMdd");
                string fHasta = hasta.ToString("yyyyMMdd");

                string sqlLinea = linea.HasValue ? linea.Value.ToString() : "NULL";

                string query = $@"
                    INSERT INTO ""WTD3"" 
                    (
                        ""AbsEntry"", 
                        ""LineId"", 
                        ""WTCode"", 
                        ""KeyPart1"", 
                        ""DateFrom"", 
                        ""DateTo"", 
                        ""KeyPart2"", 
                        ""DetailType""
                    )
                    VALUES 
                    (
                        {entry}, 
                        {sqlLinea}, 
                        '{wddCode}', 
                        '{cuit}', 
                        TO_DATE('{fDesde}', 'YYYYMMDD'), 
                        TO_DATE('{fHasta}', 'YYYYMMDD'), 
                        '{part2}', 
                        '{detType}'
                    )";

                _logger.Info(query);
                oRecordset.DoQuery(query);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error al insertar directo en WTD3: {ex.Message} {ex.StackTrace}");
                throw new Exception($"Error al insertar en WTD3: {ex.Message}");
            }
            finally
            {
                if (oRecordset != null)
                {
                    Marshal.ReleaseComObject(oRecordset);
                    oRecordset = null;
                }
            }
        }

        public void UpsertWtd3Direct(Company company, int entry, string wddCode, string cuit, DateTime desde, DateTime hasta, string part2, string detType)
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);

                string fDesde = desde.ToString("yyyyMMdd");
                string fHasta = hasta.ToString("yyyyMMdd");


                string deleteQuery = $@"
                    DELETE FROM ""WTD3"" 
                    WHERE ""AbsEntry"" = {entry} 
                    AND ""KeyPart1"" = '{cuit}' 
                    AND ""DetailType"" = '{detType}'";

                _logger.Info(deleteQuery);
                rs.DoQuery(deleteQuery);

                string maxLineQuery = $@"SELECT IFNULL(MAX(""LineId""), 0) + 1 FROM ""WTD3"" WHERE ""AbsEntry"" = {entry}";
                rs.DoQuery(maxLineQuery);
                int newLineId = int.Parse(rs.Fields.Item(0).Value.ToString());

                string insertQuery = $@"
                    INSERT INTO ""WTD3"" 
                    (""AbsEntry"", ""LineId"", ""WTCode"", ""KeyPart1"", ""DateFrom"", ""DateTo"", ""KeyPart2"", ""DetailType"")
                    VALUES 
                    (
                        {entry}, 
                        {newLineId}, 
                        '{wddCode}', 
                        '{cuit}', 
                        TO_DATE('{fDesde}', 'YYYYMMDD'), 
                        TO_DATE('{fHasta}', 'YYYYMMDD'), 
                        '{part2}', 
                        '{detType}'
                    )";
                _logger.Info(insertQuery);
                rs.DoQuery(insertQuery);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al procesar WTD3 (Delete/Insert): {ex.Message}");
            }
            finally
            {
                if (rs != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(rs);
            }
        }

        public void ExecutePrWtd3Logic(Company company, string entryStr, string wtCode, string tipo, string cuit, string risk, double rate, DateTime desde, DateTime hasta)
        {
            Recordset rs = null;
            try
            {
                if (!int.TryParse(entryStr, out int absEntry)) return;
                rs = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string fHasta = hasta.ToString("yyyyMMdd");
                string fDesde = desde.ToString("yyyyMMdd");
                string sqlRate = rate.ToString(System.Globalization.CultureInfo.InvariantCulture);

                rs.DoQuery($@"SELECT ""LineId"" FROM ""WTD3"" WHERE ""AbsEntry""={absEntry} AND ""KeyPart1""='{cuit}' AND ""DetailType""='{tipo}'");
                if (!rs.EoF)
                {
                    int lid = int.Parse(rs.Fields.Item(0).Value.ToString());
                    rs.DoQuery($@"UPDATE ""WTD3"" SET ""DateTo""=TO_DATE('{fHasta}','YYYYMMDD') WHERE ""AbsEntry""={absEntry} AND ""LineId""={lid}");
                }
                else
                {
                    rs.DoQuery($@"SELECT IFNULL(MAX(""LineId""),0)+1 FROM ""WTD3"" WHERE ""AbsEntry""={absEntry}");
                    int nlid = int.Parse(rs.Fields.Item(0).Value.ToString());
                    rs.DoQuery($@"INSERT INTO ""WTD3"" (""AbsEntry"",""LineId"",""WTCode"",""KeyPart1"",""KeyPart2"",""DetailType"",""U_B1SYS_HighRisk"",""Rate"",""DateFrom"",""DateTo"",""DataSource"",""LogInstanc"")
                                 VALUES ({absEntry},{nlid},'{wtCode}','{cuit}','80','{tipo}','{risk}',{sqlRate},TO_DATE('{fDesde}','YYYYMMDD'),TO_DATE('{fHasta}','YYYYMMDD'),'N',0)");
                }
            }
            finally { if (rs != null) Marshal.ReleaseComObject(rs); }
        }

        private string GetValue(Recordset rs, string fieldName, string defValue = "")
        {
            try { return rs.Fields.Item(fieldName).Value?.ToString() ?? defValue; }
            catch { return defValue; }
        }

        public int GetNextLineId(int absEntry)
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                string sql = $@"SELECT IFNULL(MAX(""LineId""), 0) + 1 FROM ""WTD3"" WHERE ""AbsEntry"" = {absEntry}";

                rs.DoQuery(sql);

                if (!rs.EoF)
                {
                    return int.Parse(rs.Fields.Item(0).Value.ToString());
                }
                return 1;
            }
            catch
            {
                return 1;
            }
            finally
            {
                if (rs != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(rs);
            }
        }

        //public async Task<int> CountErrorsAsync(string qValue, string year)
        //{
        //    return await Task.Run(() =>
        //    {
        //        Recordset rs = null;
        //        try
        //        {
        //            rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

        //            // Contamos registros que no estén en estado '20' (OK) ni '10' (Pendiente)
        //            // Asumiendo que '30', '40', '99' son errores. 
        //            // O si prefieres contar solo un estado específico, ajusta el WHERE.
        //            string query = $@"
        //                    SELECT COUNT(*) 
        //                    FROM ""{DB_TABLE_NAME}""
        //                    WHERE ""U_Anio"" = '{year}' 
        //                    AND ""Name"" = '{qValue}'
        //                    AND ""U_Estado"" NOT IN ('30', '20', '10', 'No Encontrado', 'Importado', 'Pendiente')";
        //            _logger.Info(query);
        //            rs.DoQuery(query);

        //            if (!rs.EoF)
        //            {
        //                int count = int.Parse(rs.Fields.Item(0).Value.ToString());
        //                _logger.Info(rs.Fields.Item(0).Value.ToString());
        //                return count;
        //            }

        //            return 0;
        //        }
        //        catch { return 0; }
        //        finally { if (rs != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(rs); }
        //    });
        //}


    }
}