using System;
using System.Collections.Generic;
using PadronWtd.Repository.DI;
using PadronWtd.UI.Logging;

namespace PadronWtd.UI.DI
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

        public static void RegisterDefaults(string logFilePath)
        {
            Register<ILogger>(() => new FileLogger(logFilePath));
            Build();
        }

        public static void RegisterRepositories(SAPbobsCOM.Company company)
        {
            var logger = Get<ILogger>();
            Register<IPSaltaRepository>(() => new PSaltaRepository(company, logger));
            Register<ISaltaConfigRepository>(() => new SaltaConfigRepository(company, logger));
            Register<IContDateRepository>(() => new ContDateRepository(company, logger));
        }
    }
}
