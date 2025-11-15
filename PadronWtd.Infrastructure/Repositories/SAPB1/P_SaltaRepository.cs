using PadronWtd.Infrastructure.Logging;
using PadronWtd.Application.Dto;
using PadronWtd.Domain.Constants;
using SAPbobsCOM;
using SAPbouiCOM;
using System;
using Company = SAPbobsCOM.Company;
using PadronWtd.Infrastucture.Logging;

namespace PadronWtd.Infrastructure.Repositories
{
    public class PSaltaRepository
    {
        private readonly Company _company;
        private readonly ILogger _logger;

        public PSaltaRepository(Company company, ILogger logger)
        {
            _company = company;
            _logger = logger;
        }

        public string Add(PSaltaDto dto)
        {
            try
            {
                _logger.Info($"Insertando registro en P_Salta: Code={dto.Code}, Cuit={dto.Cuit}");

                // Obtener servicio general
                CompanyService companyService = _company.GetCompanyService();
                GeneralService generalService = companyService.GetGeneralService(PSaltaTable.TableName);

                // Crear estructura de datos
                GeneralData data = (GeneralData)generalService.GetDataInterface(GeneralServiceDataInterfaces.gsGeneralData);

                // Campos nativos
                data.SetProperty("Code", dto.Code);
                data.SetProperty("Name", dto.Name);

                // Campos UDF
                data.SetProperty(PSaltaTable.U_Anio, dto.Anio);
                data.SetProperty(PSaltaTable.U_Padron, dto.Padron);
                data.SetProperty(PSaltaTable.U_Cuit, dto.Cuit);
                data.SetProperty(PSaltaTable.U_Inscripcion, dto.Inscripcion);
                data.SetProperty(PSaltaTable.U_Riesgo, dto.Riesgo);
                data.SetProperty(PSaltaTable.U_Notas, dto.Notas ?? "");
                data.SetProperty(PSaltaTable.U_Procesado, dto.Procesado ?? "");

                // Ejecutar ADD
                GeneralDataParams result =
                    (GeneralDataParams)generalService.Add(data);

                string newKey = result.GetProperty("Code").ToString();

                _logger.Info($"Insert OK en P_Salta. Nuevo Code={newKey}");

                return newKey;
            }
            catch (Exception ex)
            {
                string msg = $"ERROR al insertar en P_Salta: {ex.Message}";
                _logger.Error(msg);

                throw new Exception(msg, ex);
            }
        }
    }
}
