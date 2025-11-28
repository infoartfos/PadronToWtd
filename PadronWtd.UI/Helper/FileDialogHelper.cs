using System;
using System.Threading;
using System.Windows.Forms; // Referencia a System.Windows.Forms necesaria

namespace PadronWtd.UI.Helpers
{
    public static class FileDialogHelper
    {
        /// <summary>
        /// Abre un diálogo de selección de archivo en un hilo STA seguro.
        /// </summary>
        /// <param name="onFileSelected">Acción a ejecutar cuando se selecciona un archivo (recibe el path).</param>
        public static void OpenFileDialog(Action<string> onFileSelected)
        {
            var t = new Thread(() =>
            {
                try
                {
                    using (var dialog = new OpenFileDialog())
                    {
                        dialog.Filter = "Archivos de Padrón (*.csv;*.txt)|*.csv;*.txt|Todos los archivos (*.*)|*.*";
                        dialog.Title = "Seleccionar archivo";
                        dialog.RestoreDirectory = true;

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            onFileSelected?.Invoke(dialog.FileName);
                        }
                    }
                }
                catch (Exception)
                {
                    // Manejar error silenciosamente o loguear si es necesario
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }
    }
}