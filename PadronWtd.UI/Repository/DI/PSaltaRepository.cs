using PadronWtd.Domain;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
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