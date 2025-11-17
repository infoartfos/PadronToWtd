using System;
using System.Collections.Generic;
using PadronSaltaAddOn.UI.Logging;
using PadronSaltaAddOn.UI.Services;

namespace PadronSaltaAddOn.UI.DI
{
    public static class SimpleServiceProvider
    {
        private static readonly Dictionary<Type, Func<object>> _factories = new Dictionary<Type, Func<object>>();
        private static bool _built = false;

        public static void Register<TService>(Func<TService> factory) where TService : class
        {
            _factories[typeof(TService)] = () => factory();
        }

        public static void Build()
        {
            _built = true;
        }

        public static TService Get<TService>() where TService : class
        {
            if (!_built)
                throw new InvalidOperationException("SimpleServiceProvider no fue inicializado. Llamá Build() después de registrar servicios.");

            if (_factories.TryGetValue(typeof(TService), out var fac))
            {
                return (TService)fac();
            }

            throw new InvalidOperationException($"Servicio no registrado: {typeof(TService).FullName}");
        }

        // Helper preconfigurado
        public static void RegisterDefaults(string logFilePath)
        {
            Register<ILogger>(() => new FileLogger(logFilePath));
            Register<IImportService>(() => new FrmImportarService(Get<ILogger>()));
            Build();
        }
    }
}
