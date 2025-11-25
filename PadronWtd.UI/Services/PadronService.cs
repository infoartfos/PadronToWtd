using SAPbobsCOM;
// using SAPbouiCOM;
using System;
using System.Threading.Tasks;
using System.Windows.Forms; // Si usas WinForms o SAP Forms
using PadronWtd.Domain;
using PadronWtd.Repository.DI;

namespace PadronWtd.UI.Services
{
    public class PadronService
    {
        private readonly Company _company;

        public PadronService(Company company)
        {
            _company = company;
        }

        public async Task ProcesarPadron2025()
        {
            try
            {
                // 1. Instanciar el repositorio
                var repository = new PSaltaRepository(_company);

                Console.WriteLine("Iniciando búsqueda de registros del 2025...");

                // 2. Llamar al método asíncrono filtrando por año
                var listaResultados = await repository.GetByAnioAsync("Q1", "2025");

                if (listaResultados.Count == 0)
                {
                    MessageBox.Show("No se encontraron registros para el año 2025.");
                    return;
                }

                // 3. Iterar sobre los resultados
                foreach (var registro in listaResultados)
                {
                    // Lógica de negocio, ejemplo: mostrar en consola o log
                    string info = $"Código: {registro.Code} | CUIT: {registro.U_Cuit} | Riesgo: {registro.U_Riesgo}";

                    // Ejemplo: Si quieres actualizar el estado a "Procesado"
                    if (registro.U_Procesado != "Y")
                    {
                        registro.U_Procesado = "Y";
                        registro.U_Notas = "Procesado automátiamente el " + DateTime.Now.ToString();

                        // Actualizar en base de datos
                        await repository.UpdateAsync(registro);
                    }
                }

                MessageBox.Show($"Proceso finalizado. Se procesaron {listaResultados.Count} registros.");
            }
            catch (Exception ex)
            {
                // Manejo de errores
                MessageBox.Show($"Ocurrió un error: {ex.Message}");
            }
        }
    }
}