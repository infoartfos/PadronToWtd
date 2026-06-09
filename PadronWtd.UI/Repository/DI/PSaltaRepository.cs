using PadronWtd.Domain;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PadronWtd.Repository.DI
{
    public class PSaltaRepository
    {
        public const int MaxLenNotas = 253;
        //
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
                    throw(ex);
                }
                finally
                {
                    if (userTable != null) Marshal.ReleaseComObject(userTable);
                }
            });
        }

        public async Task<List<PSaltaRecord>> GetImportadosYErrorByPeriodoAnioAsync(string q_value, string anio)
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
                    _logger.Error($"Error en GetImportadosYErrorByPeriodoAnioAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (recordset != null) Marshal.ReleaseComObject(recordset);
                }
                return records;
            });
        }

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
                sb.Append($"'{SafeSubstring(r.U_Notas, MaxLenNotas)}' ");

                sb.Append(" FROM DUMMY ");
            }
            return sb.ToString();
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

        public (bool success, string error) InsertWtd3Direct(
            Company company,
            int entry,
            string wddCode,
            string cuit,
            DateTime desde,
            DateTime hasta,
            string part2,
            string detType)
        {
            Recordset oRS = null;
            try
            {
                oRS = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string fDesde = desde.ToString("yyyyMMdd");
                string fHasta = hasta.ToString("yyyyMMdd");

                // Paso 1: verificar si ya existe ese CUIT en ese período
                string queryCheck = $@"
                        SELECT COUNT(*) AS CANT 
                        FROM ""WTD3""
                        WHERE ""AbsEntry"" = {entry}
                          AND ""WTCode""   = '{wddCode}'
                          AND ""KeyPart1"" = '{cuit}'
                          AND ""DateFrom"" = TO_DATE('{fDesde}', 'YYYYMMDD')";

                oRS.DoQuery(queryCheck);
                int existe = int.Parse(oRS.Fields.Item("CANT").Value.ToString());

                if (existe > 0)
                {
                    string msgSkip = $"WTD3 ya existe para CUIT:{cuit} WTCode:{wddCode} Desde:{fDesde} - Skipping";
                    _logger.Info(msgSkip);
                    return (true, string.Empty);
                }

                // Paso 2 : INSERT con LineId como subquery (atómico)
                string queryInsert = $@"
                        INSERT INTO ""WTD3"" 
                        (
                            ""AbsEntry"", 
                            ""LineId"",
                            ""WTCode"", 
                            ""KeyPart1"",
                            ""KeyPart2"",
                            ""DateFrom"", 
                            ""DateTo"",
                            ""DetailType"",
                            ""DataSource"",
                            ""UpdateDate""
                        )
                        VALUES 
                        (
                            {entry}, 
                            (SELECT COALESCE(MAX(""LineId""), -1) + 1 FROM ""WTD3"" WHERE ""AbsEntry"" = {entry}),
                            '{wddCode}', 
                            '{cuit}',
                            '{part2}',
                            TO_DATE('{fDesde}', 'YYYYMMDD'), 
                            TO_DATE('{fHasta}', 'YYYYMMDD'),
                            '{detType}',
                            'M',
                            NOW()
                        )";

                string queryLimpia = Regex.Replace(queryInsert, @"\s+", " ").Trim();
                _logger.Info(queryLimpia);
                oRS.DoQuery(queryInsert);
                _logger.Info($"WTD3 insertado OK - CUIT:{cuit}, {wddCode} , Desd:{fDesde}");

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                string error = $"{wddCode}/{entry}: {ex.Message}";
                _logger.Error($"{error}\n{ex.StackTrace}");
                return (false, error);
            }
            finally
            {
                if (oRS != null)
                {
                    Marshal.ReleaseComObject(oRS);
                    oRS = null;
                }
            }
        }

        public (bool alreadyExists, bool previousOK) CheckWtd3Exists(int entry, string wddCode, string cuit, DateTime desde, DateTime hasta)
        {
            bool alreadyExists = CheckWtd3AlreadyExists(entry, wddCode, cuit, desde);
            bool previousOK = false;
            if (alreadyExists) 
            {
                previousOK = CheckWtd3ExistsPreviouslyOK(entry, wddCode, cuit, desde, hasta);
            }
            return (alreadyExists, previousOK);
        }

        private bool CheckWtd3AlreadyExists(int entry, string wddCode, string cuit, DateTime desde)
        {
            Recordset oRS = null;
            try
            {
                oRS = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string fDesde = desde.ToString("yyyyMMdd");
                string query = $@"
                        SELECT COUNT(*) AS CANT 
                        FROM ""WTD3""
                        WHERE ""AbsEntry"" = {entry}
                          AND ""WTCode""   = '{wddCode}'
                          AND ""KeyPart1"" = '{cuit}'
                          AND ""DateFrom"" = TO_DATE('{fDesde}', 'YYYYMMDD')";
                oRS.DoQuery(query);
                int cant = int.Parse(oRS.Fields.Item("CANT").Value.ToString());
                return cant > 0 ;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error en CheckWtd3AlreadyExists: {ex.Message}");
                return false;
            }
            finally
            {
                if (oRS != null) Marshal.ReleaseComObject(oRS);
            }
        }



        private bool CheckWtd3ExistsPreviouslyOK(int entry, string wddCode, string cuit, DateTime desde, DateTime hasta)
        {
            Recordset oRS = null;
            try
            {
                oRS = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string fDesde = desde.ToString("yyyyMMdd");
                string fHasta = hasta.ToString("yyyyMMdd");
                string query = $@"
                        SELECT COUNT(*) AS CANT 
                        FROM ""WTD3""
                        WHERE ""AbsEntry"" = {entry}
                          AND ""WTCode""   = '{wddCode}'
                          AND ""KeyPart1"" = '{cuit}'
                          AND ""DateFrom"" = TO_DATE('{fDesde}', 'YYYYMMDD')
                          AND ""DateTo"" = TO_DATE('{fHasta}', 'YYYYMMDD')";
                oRS.DoQuery(query);
                int cant = int.Parse(oRS.Fields.Item("CANT").Value.ToString());
                return cant > 0 ;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error en CheckWtd3ExistsPreviouslyOK: {ex.Message}");
                return false;
            }
            finally
            {
                if (oRS != null) Marshal.ReleaseComObject(oRS);
            }
        }


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

        private string GetValue(Recordset rs, string fieldName, string defValue = "")
        {
            try { return rs.Fields.Item(fieldName).Value?.ToString() ?? defValue; }
            catch { return defValue; }
        }

    }
}