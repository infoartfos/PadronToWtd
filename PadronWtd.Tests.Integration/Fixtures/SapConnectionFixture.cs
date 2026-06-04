using System;
using PadronWtd.UI.DI;
using SAPbobsCOM;

namespace PadronWtd.Tests.Integration.Fixtures
{
    public class SapConnectionFixture : IDisposable
    {
        public Company Company { get; }

        public SapConnectionFixture()
        {
            // Inicializar el service locator con un logger a archivo temporal
            string logPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"PadronWtd_IntegrationTests_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            SimpleServiceProvider.RegisterDefaults(logPath);

            Company = SapConnectionManager.Instance.GetCompany(forceServiceUser: true);

            if (!Company.Connected)
                throw new InvalidOperationException(
                    "No se pudo conectar a SAP Business One. Verifique App.config.");
        }

        public void Dispose()
        {
            if (Company?.Connected == true)
            {
                try { Company.Disconnect(); }
                catch { /* cleanup silencioso */ }
            }
        }
    }
}
