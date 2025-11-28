using PadronWtd.Domain;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const string TABLE_NAME = "PADRON_SALTA_IMP";
        private const string DB_TABLE_NAME = "@PADRON_SALTA_IMP";

        public PSaltaRepository(Company company)
        {
            _logger = SimpleServiceProvider.Get<ILogger>();
            _company = company ?? throw new ArgumentNullException(nameof(company));
            if (!_company.Connected)
                throw new InvalidOperationException("La conexión a SAP Business One no está activa.");
        }

        // -----------------------------------------------------------------------
        // GET ALL: Lee todos los registros
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

                    // Query explícito para traer solo lo necesario y castear fechas si fuera necesario en HANA/SQL
                    string query = $@"
                        SELECT 
                            ""Code"", ""Name"", ""DocEntry"", ""Canceled"", ""Object"", 
                            ""UserSign"", ""CreateDate"", ""DataSource"",
                            ""U_Anio"", ""U_Padron"", ""U_Cuit"", ""U_Inscripcion"", 
                            ""U_Riesgo"", ""U_Notas"", ""U_Procesado"", ""U_Estado""
                        FROM ""{DB_TABLE_NAME}"" 
                        ORDER BY CAST(""Code"" AS INT) ASC";

                    recordset.DoQuery(query);

                    while (!recordset.EoF)
                    {
                        var rec = new PSaltaRecord
                        {
                            Code = GetValue(recordset, "Code"),
                            Name = GetValue(recordset, "Name"),
                            DocEntry = int.Parse(GetValue(recordset, "DocEntry", "0")),
                            Canceled = GetValue(recordset, "Canceled"),
                            Object = GetValue(recordset, "Object"),
                            DataSource = GetValue(recordset, "DataSource"),

                            // Campos de Usuario
                            U_Anio = GetValue(recordset, "U_Anio"),
                            U_Padron = GetValue(recordset, "U_Padron"),
                            U_Cuit = GetValue(recordset, "U_Cuit"),
                            U_Inscripcion = GetValue(recordset, "U_Inscripcion"),
                            U_Riesgo = GetValue(recordset, "U_Riesgo"),
                            U_Notas = GetValue(recordset, "U_Notas"),
                            U_Procesado = GetValue(recordset, "U_Procesado"),
                            U_Estado = GetValue(recordset, "U_Estado")
                        };

                        // Manejo seguro de fechas
                        var createDateVal = recordset.Fields.Item("CreateDate").Value;
                        //if (createDateVal != null && createDateVal is DateTime dt)
                        //{
                        //    rec.CreateDate = dt;
                        //}

                        records.Add(rec);
                        recordset.MoveNext();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error en GetAllAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (recordset != null) Marshal.ReleaseComObject(recordset);
                }

                return records;
            });
        }

        // -----------------------------------------------------------------------
        // CREATE: Inserta un nuevo registro usando UserTable (Datos), no MD
        // -----------------------------------------------------------------------
        public async Task<string> CreateAsync(PSaltaRecord r)
        {
            return await Task.Run(() =>
            {
                UserTable userTable = null;
                try
                {
                    // CORRECCIÓN CRÍTICA: Usamos UserTables.Item, no GetBusinessObject(oUserTables)
                    // oUserTables es para crear la estructura de la tabla, UserTables.Item es para insertar datos.
                    userTable = _company.UserTables.Item(TABLE_NAME);

                    // Obtenemos el próximo código manual (ya que es UDT estándar)
                    string nextCode = GetNextCode();

                    userTable.Code = nextCode;
                    userTable.Name = string.IsNullOrWhiteSpace(r.Name) ? nextCode : r.Name;

                    // Asignar campos UDF
                    userTable.UserFields.Fields.Item("U_Anio").Value = r.U_Anio ?? "";
                    userTable.UserFields.Fields.Item("U_Padron").Value = r.U_Padron ?? "";
                    userTable.UserFields.Fields.Item("U_Cuit").Value = r.U_Cuit ?? "";
                    userTable.UserFields.Fields.Item("U_Inscripcion").Value = r.U_Inscripcion ?? "";
                    userTable.UserFields.Fields.Item("U_Riesgo").Value = r.U_Riesgo ?? "";
                    userTable.UserFields.Fields.Item("U_Notas").Value = r.U_Notas ?? "";
                    userTable.UserFields.Fields.Item("U_Procesado").Value = r.U_Procesado ?? "";
                    userTable.UserFields.Fields.Item("U_Estado").Value = r.U_Estado ?? "";

                    int result = userTable.Add();

                    if (result != 0)
                    {
                        string errMsg = _company.GetLastErrorDescription();
                        throw new Exception($"Error SAP ({result}): {errMsg}");
                    }

                    return nextCode;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error en CreateAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (userTable != null) Marshal.ReleaseComObject(userTable);
                }
            });
        }

        // -----------------------------------------------------------------------
        // UPDATE: Actualiza un registro existente
        // -----------------------------------------------------------------------
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
                    Debug.WriteLine($"Error en UpdateAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (userTable != null) Marshal.ReleaseComObject(userTable);
                }
            });
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private string GetNextCode()
        {
            Recordset recordset = null;
            try
            {
                recordset = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                // Consulta optimizada para obtener el último código numérico
                // Compatible con SQL Server y HANA si el campo Code es numérico
                string sql = $@"SELECT TOP 1 ""Code"" FROM ""{DB_TABLE_NAME}"" ORDER BY CAST(""Code"" AS INT) DESC";

                recordset.DoQuery(sql);

                if (!recordset.EoF && recordset.Fields.Item("Code").Value != null)
                {
                    string lastCode = recordset.Fields.Item("Code").Value.ToString();
                    if (int.TryParse(lastCode, out int num))
                    {
                        return (num + 1).ToString();
                    }
                }

                return "1";
            }
            catch
            {
                return "1"; // Fallback en caso de error o tabla vacía
            }
            finally
            {
                if (recordset != null) Marshal.ReleaseComObject(recordset);
            }
        }

        private string GetValue(Recordset rs, string fieldName, string defValue = "")
        {
            try
            {
                var val = rs.Fields.Item(fieldName).Value;
                if (val == null) return defValue;
                return val.ToString();
            }
            catch
            {
                return defValue;
            }
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
                    string query = $@"
                SELECT 
                    ""Code"", ""Name"", ""DocEntry"", ""Canceled"", ""Object"", 
                    ""UserSign"", ""CreateDate"", ""DataSource"",
                    ""U_Anio"", ""U_Padron"", ""U_Cuit"", ""U_Inscripcion"", 
                    ""U_Riesgo"", ""U_Notas"", ""U_Procesado"", ""U_Estado""
                FROM ""{DB_TABLE_NAME}"" 
                WHERE ""U_Anio"" = '{anio}'
                AND  ""Name"" = '{q_value}'
                ORDER BY CAST(""Code"" AS INT) ASC";

                    recordset.DoQuery(query);

                    while (!recordset.EoF)
                    {
                        var rec = new PSaltaRecord
                        {
                            Code = GetValue(recordset, "Code"),
                            Name = GetValue(recordset, "Name"),
                            DocEntry = int.Parse(GetValue(recordset, "DocEntry", "0")),
                            Canceled = GetValue(recordset, "Canceled"),
                            Object = GetValue(recordset, "Object"),
                            DataSource = GetValue(recordset, "DataSource"),
                            U_Anio = GetValue(recordset, "U_Anio"),
                            U_Padron = GetValue(recordset, "U_Padron"),
                            U_Cuit = GetValue(recordset, "U_Cuit"),
                            U_Inscripcion = GetValue(recordset, "U_Inscripcion"),
                            U_Riesgo = GetValue(recordset, "U_Riesgo"),
                            U_Notas = GetValue(recordset, "U_Notas"),
                            U_Procesado = GetValue(recordset, "U_Procesado"),
                            U_Estado = GetValue(recordset, "U_Estado")
                        };

                        var createDateVal = recordset.Fields.Item("CreateDate").Value;
                        records.Add(rec);
                        recordset.MoveNext();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en GetByAnioAsync: {ex.Message}{ex.StackTrace}");
                    Debug.WriteLine($"Error en GetByAnioAsync: {ex.Message}");
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

        //public async Task DeleteByAnioAndQSqlAsync(string q_value, string anio)
        //{
        //    await Task.Run(() =>
        //    {
        //        Recordset recordset = null;
        //        try
        //        {
        //            recordset = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
        //            string query = $@"DELETE FROM ""{DB_TABLE_NAME}"" WHERE ""U_Anio"" = '{anio}' AND ""Name"" = '{q_value}'";
        //            recordset.DoQuery(query);
        //        }
        //        finally
        //        {
        //            if (recordset != null) Marshal.ReleaseComObject(recordset);
        //        }
        //    });
        //}
        // -----------------------------------------------------------------------
        // DELETE BATCH: Borra todos los registros de un Q y Año específicos
        // -----------------------------------------------------------------------
        public async Task DeleteByAnioAndQAsync(string q_value, string anio)
        {
            await Task.Run(() =>
            {
                Recordset recordset = null;
                UserTable userTable = null;

                try
                {
                    recordset = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                    string query = $@"
                        SELECT ""Code"" 
                        FROM ""{DB_TABLE_NAME}"" 
                        WHERE ""U_Anio"" = '{anio}' 
                        AND ""Name"" = '{q_value}'";

                    recordset.DoQuery(query);

                    if (recordset.EoF) return; // No hay nada que borrar

                    userTable = _company.UserTables.Item(TABLE_NAME);

                    recordset.MoveFirst();

                    while (!recordset.EoF)
                    {
                        string codeToDelete = recordset.Fields.Item("Code").Value.ToString();
                        if (userTable.GetByKey(codeToDelete))
                        {
                            int result = userTable.Remove();
                            if (result != 0)
                            {
                                string errorMsg = _company.GetLastErrorDescription();
                                _logger.Error($"Error al borrar registro Code {codeToDelete}: {errorMsg}");
                                // Opcional: throw new Exception(...) si quieres detener todo el proceso
                            }
                        }

                        recordset.MoveNext();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en DeleteByAnioAndQAsync: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (recordset != null) Marshal.ReleaseComObject(recordset);
                    if (userTable != null) Marshal.ReleaseComObject(userTable);
                }
            });
        }


        public async Task BulkInsertAsync(List<PSaltaRecord> records)
        {
            await Task.Run(() =>
            {
                Recordset oRS = null;
                try
                {
                    oRS = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                    // Bajamos el tamaño del lote a 200 para evitar errores de complejidad en HANA
                    int batchSize = 200;
                    int totalRecords = records.Count;
                    int processed = 0;

                    // 1. Obtener el DocEntry inicial UNA SOLA VEZ al principio (o por lote si hay concurrencia)
                    // Para ser más seguros ante concurrencia, lo ideal es obtenerlo justo antes de cada insert,
                    // pero para imports masivos únicos, tomarlo al inicio y sumar en memoria es mucho más rápido.
                    int currentDocEntryBase = GetNextDocEntry();

                    while (processed < totalRecords)
                    {
                        var batch = records.Skip(processed).Take(batchSize).ToList();

                        if (batch.Any())
                        {
                            // Pasamos el ID base para que el método asigne IDs consecutivos
                            string sql = BuildHanaInsertBatch(batch, currentDocEntryBase);
                            _logger.Info("SQL: " + sql);
                            oRS.DoQuery(sql);

                            // Actualizamos la base para el siguiente lote
                            currentDocEntryBase += batch.Count;
                        }

                        processed += batchSize;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error en BulkInsert: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (oRS != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(oRS);
                }
            });
        }

        private string BuildHanaInsertBatch(List<PSaltaRecord> batch, int startDocEntry)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("INSERT INTO \"@PADRON_SALTA_IMP\" ");
            sb.Append("(\"DocEntry\", \"Code\", \"Name\", \"DataSource\", \"UserSign\", \"Object\", \"CreateDate\", \"Canceled\", ");
            sb.Append("\"U_Anio\", \"U_Padron\", \"U_Cuit\", \"U_Inscripcion\", \"U_Riesgo\", \"U_Estado\", \"U_Notas\") ");

            string objectType = "PADRON_SALTA_IMP";
            string userSign = "1";

            for (int i = 0; i < batch.Count; i++)
            {
                var r = batch[i];
                int rowDocEntry = startDocEntry + i;

                if (string.IsNullOrEmpty(r.Code)) r.Code = SequentialId.Generate();
                string name = string.IsNullOrEmpty(r.Name) ? r.Code : r.Name;

                if (i > 0) sb.Append(" UNION ALL ");

                sb.Append("SELECT ");

                // --- COLUMNAS DEL SISTEMA ---
                sb.Append($"{rowDocEntry}, ");                 // DocEntry (Entero)
                sb.Append($"'{r.Code}', ");                    // Code
                sb.Append($"'{Sanitize(name)}', ");            // Name
                sb.Append("'I', ");                            // DataSource
                sb.Append($"{userSign}, ");                    // UserSign
                sb.Append($"'{objectType}', ");                // Object

                // CORRECCIÓN PARA FECHA DE SISTEMA:
                // Usamos CAST(CURRENT_DATE AS DATE) para que HANA no tenga dudas.
                sb.Append("CAST(CURRENT_DATE AS DATE), ");     // CreateDate

                sb.Append("'N', ");                            // Canceled

                // --- COLUMNAS DE USUARIO (Revisar tipos en SAP si falla) ---
                sb.Append($"'{Sanitize(r.U_Anio)}', ");
                sb.Append($"'{Sanitize(r.U_Padron)}', ");
                sb.Append($"'{Sanitize(r.U_Cuit)}', ");
                sb.Append($"'{Sanitize(r.U_Inscripcion)}', ");
                sb.Append($"'{Sanitize(r.U_Riesgo)}', ");
                sb.Append($"'{Sanitize(r.U_Estado)}', ");
                // sb.Append($"'{Sanitize(r.U_Procesado)}', ");
                sb.Append($"'{Sanitize(r.U_Notas)}' ");

                sb.Append("FROM DUMMY ");
            }

            return sb.ToString();
        }
        private int GetNextDocEntry()
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                // Usamos IFNULL para que devuelva 0 si la tabla está vacía
                string sql = "SELECT IFNULL(MAX(\"DocEntry\"), 0) FROM \"@PADRON_SALTA_IMP\"";
                rs.DoQuery(sql);

                if (!rs.EoF)
                {
                    return int.Parse(rs.Fields.Item(0).Value.ToString()) + 1;
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

        private string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("'", "''");
        }

        public void ExecuteSpInsertWtd3(Company company, int entry, int linea, int wddCode, string cuit, DateTime desde, DateTime hasta, string part2, string detType)
            {
                Recordset oRecordset = null;
                try
                {
                    oRecordset = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);

                    string fDesde = desde.ToString("yyyyMMdd");
                    string fHasta = hasta.ToString("yyyyMMdd");

                    string query = $@"
                    CALL ""SBP_SIOC_CHAR"".""SP_INSERT_WTD3"" (
                        {entry}, 
                        {linea}, 
                        {wddCode}, 
                        '{cuit}', 
                        '{fDesde}', 
                        '{fHasta}', 
                        '{part2}', 
                        '{detType}'
                    )";

                    oRecordset.DoQuery(query);

                }
                catch (Exception ex)
                {
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


}
}