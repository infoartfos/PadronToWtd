using PadronWtd.Domain;
using PadronWtd.UI.DI;
using PadronWtd.UI.Logging;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PadronWtd.Repository.DI
{
    public class SaltaConfigRepository
    {
        private readonly Company _company;
        private readonly ILogger _logger;

        public SaltaConfigRepository(Company company)
        {
            _company = company;
            _logger = SimpleServiceProvider.Get<ILogger>();
        }

        public async Task<List<ImpuestoRecord>> GetConfiguracionImpuestosAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<ImpuestoRecord>();
                Recordset rs = null;

                try
                {
                    rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);

                    // JOIN entre Cabecera y Detalle para obtener el Código SAP
                    string query = @"
                        SELECT  T0.""U_Tipo_Insc"" , T0.""U_Riesgo"" , T1.""U_CodigoSAP"" -- , T1.""U_Codigo"" AS ""Codigo Interno""
                        FROM  ""@COD_SALTA_CAB"" T0
                        INNER JOIN ""@COD_SALTA_DET"" T1 ON T0.""DocEntry"" = T1.""DocEntry""
                        WHERE T0.""U_Activo"" = 'SI' 
                        AND T1.""U_Activo"" = 'SI'";

                    rs.DoQuery(query);

                    while (!rs.EoF)
                    {
                        var rec = new ImpuestoRecord
                        {
                            Inscripcion = GetValue(rs, "U_Tipo_Insc"),
                            Riesgo = GetValue(rs, "U_Riesgo"),
                            CodigoSap = GetValue(rs, "U_CodigoSAP")
                        };

                        // Validar que no vengan vacíos para evitar claves nulas
                        if (!string.IsNullOrEmpty(rec.Inscripcion) && !string.IsNullOrEmpty(rec.CodigoSap))
                        {
                            list.Add(rec);
                        }

                        rs.MoveNext();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error obteniendo configuración impuestos: {ex.Message}");
                }
                finally
                {
                    if (rs != null) Marshal.ReleaseComObject(rs);
                }

                return list;
            });
        }

        private string GetValue(Recordset rs, string fieldName)
        {
            try { 
                return rs.Fields.Item(fieldName).Value?.ToString() ?? ""; }
            catch (Exception ex) {
                _logger.Error($"Error leyendo campo {fieldName} {ex.Message} {ex.StackTrace}");
                return ""; 
            }
        }
    }
}