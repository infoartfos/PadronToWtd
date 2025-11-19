using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PadronWtd.UI.Repository.DI
{
    public class PSaltaRepository
{
    private readonly Company _company; // Instancia activa de SAPbobsCOM.Company

    public PSaltaRepository(Company company)
    {
        _company = company ?? throw new ArgumentNullException(nameof(company));
        if (_company.Connected == false)
            throw new InvalidOperationException("La conexión a SAP Business One no está activa.");
    }

    // -----------------------------------------------------------------------
    // GET ALL: Lee todos los registros de @P_Salta
    // -----------------------------------------------------------------------
    public async Task<List<PSaltaRecord>> GetAllAsync()
    {
        return await Task.Run(() =>
        {
            var records = new List<PSaltaRecord>();
            try
            {
                var recordset = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                // Asegúrate de seleccionar todos los campos que necesitas
                recordset.DoQuery("SELECT \"Code\", \"Name\", \"U_Campo1\", \"U_Campo2\" FROM \"@P_SALTA\" ORDER BY \"Code\"");

                while (!recordset.EoF)
                {
                    records.Add(new PSaltaRecord
                    {
                        Code = recordset.Fields.Item("Code").Value?.ToString() ?? "",
                        Name = recordset.Fields.Item("Name").Value?.ToString() ?? "",
                        U_Campo1 = recordset.Fields.Item("U_Campo1").Value?.ToString() ?? "",
                        U_Campo2 = recordset.Fields.Item("U_Campo2").Value?.ToString() ?? ""
                        // Mapea otros campos según tu tabla
                    });
                    recordset.MoveNext();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en GetAllAsync: {ex.Message}");
                throw;
            }

            return records;
        });
    }

    // -----------------------------------------------------------------------
    // CREATE: Inserta un nuevo registro en @P_Salta
    // -----------------------------------------------------------------------
    public async Task<string> CreateAsync(PSaltaRecord r)
    {
        return await Task.Run(() =>
        {
            try
            {
                r.Code = GetNextCode();

                var userTable = (UserTablesMD)_company.GetBusinessObject(BoObjectTypes.oUserTables);
                userTable.TableName = "@P_SALTA";
                userTable.Code = r.Code;
                userTable.Name = r.Name;
                // Asignar campos UDF
                userTable.UserFields.Fields.Item("U_Campo1").Value = r.U_Campo1 ?? "";
                userTable.UserFields.Fields.Item("U_Campo2").Value = r.U_Campo2 ?? "";

                int result = userTable.Add();
                if (result != 0)
                {
                    string errMsg = _company.GetLastErrorDescription();
                    throw new Exception($"Error al crear registro: {errMsg}");
                }

                return r.Code;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en CreateAsync: {ex.Message}");
                throw;
            }
        });
    }

    // -----------------------------------------------------------------------
    // UPDATE: Actualiza un registro existente en @P_Salta
    // -----------------------------------------------------------------------
    public async Task<string> UpdateAsync(PSaltaRecord r)
    {
        return await Task.Run(() =>
        {
            try
            {
                var userTable = (UserTablesMD)_company.GetBusinessObject(BoObjectTypes.oUserTables);
                userTable.TableName = "@P_SALTA";

                if (userTable.GetByKey(r.Code))
                {
                    userTable.Name = r.Name;
                    userTable.UserFields.Fields.Item("U_Campo1").Value = r.U_Campo1 ?? "";
                    userTable.UserFields.Fields.Item("U_Campo2").Value = r.U_Campo2 ?? "";

                    int result = userTable.Update();
                    if (result != 0)
                    {
                        string errMsg = _company.GetLastErrorDescription();
                        throw new Exception($"Error al actualizar: {errMsg}");
                    }

                    return r.Code;
                }
                else
                {
                    throw new Exception($"Registro con Code '{r.Code}' no encontrado.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en UpdateAsync: {ex.Message}");
                throw;
            }
        });
    }

    // -----------------------------------------------------------------------
    // Obtiene el próximo CODE incremental (último + 1)
    // -----------------------------------------------------------------------
    private string GetNextCode()
    {
        try
        {
            var recordset = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
            recordset.DoQuery("SELECT TOP 1 \"Code\" FROM \"@P_SALTA\" ORDER BY CAST(\"Code\" AS INT) DESC");

            if (!recordset.EoF && recordset.Fields.Item("Code").Value != null)
            {
                string lastCode = recordset.Fields.Item("Code").Value.ToString();
                if (int.TryParse(lastCode, out int num))
                {
                    return (num + 1).ToString();
                }
            }

            return "1"; // Primer código si la tabla está vacía
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error en GetNextCode: {ex.Message}");
            return "1";
        }
    }
}

}