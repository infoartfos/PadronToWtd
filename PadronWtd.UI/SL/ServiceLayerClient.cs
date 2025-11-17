using Newtonsoft.Json;
using PadronSaltaAddOn.UI.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PadronSaltaAddOn.UI.SL
{
    public class ServiceLayerClient : IDisposable
    {
        private static HttpClient _http; // singleton
        private readonly ILogger _logger;

        private readonly string _baseUrl;
        private readonly string _user;
        private readonly string _password;
        private readonly string _company;

        private readonly CookieContainer _cookies;
        private CancellationTokenSource _keepAliveToken;

        public ServiceLayerClient(string baseUrl, string user, string password, string company, ILogger logger)
        {
            _logger = logger;
            _baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
            _user = user;
            _password = password;
            _company = company;

            _logger.Info("Inicializando ServiceLayerClient…");

            // Ignorar certificados SSL (SAP SL con cert self-signed)
            ServicePointManager.ServerCertificateValidationCallback =
                (sender, cert, chain, sslPolicyErrors) => true;

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls;

            _cookies = new CookieContainer();

            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = _cookies,
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            if (_http == null)
            {
                _http = new HttpClient(handler)
                {
                    BaseAddress = new Uri(_baseUrl),
                    Timeout = TimeSpan.FromMinutes(10)
                };

                _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
            }

            StartKeepAlive();
        }

        // ---------------------------------------------------------------------
        // LOGIN
        // ---------------------------------------------------------------------
        public async Task LoginAsync()
        {
            _logger.Info("Haciendo LOGIN en Service Layer…");

            var payload = new
            {
                UserName = _user,
                Password = _password,
                CompanyDB = _company
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync("Login", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.Error("LOGIN SL FALLÓ: " + body);
                throw new Exception("Login SL FAILED: " + body);
            }

            _logger.Info("Login SL OK.");
        }

        // ---------------------------------------------------------------------
        // POST GENÉRICO CON RETRY AUTOMÁTICO DE ERROR 301
        // ---------------------------------------------------------------------
        public async Task<string> PostAsync(string resource, object data)
        {
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync(resource, content);
            string body = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.Warn("SL devolvió 301 (Invalid session). Haciendo RE-LOGIN automáticamente…");

                await LoginAsync();

                // Reintento
                content = new StringContent(json, Encoding.UTF8, "application/json");
                resp = await _http.PostAsync(resource, content);
                body = await resp.Content.ReadAsStringAsync();
            }

            if (!resp.IsSuccessStatusCode)
            {
                _logger.Error($"Error SL ({resp.StatusCode}): {body}");
                throw new Exception($"Error SL ({resp.StatusCode}): {body}");
            }

            return body;
        }

        // ---------------------------------------------------------------------
        // KEEP-ALIVE
        // ---------------------------------------------------------------------
        private void StartKeepAlive()
        {
            _keepAliveToken = new CancellationTokenSource();
            var ct = _keepAliveToken.Token;

            Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(10000, ct); // cada 10s
                        await _http.GetAsync("Ping");
                    }
                    catch { /* No importa, es keep-alive */ }
                }
            }, ct);
        }

        // ---------------------------------------------------------------------
        // LOGOUT + STOP
        // ---------------------------------------------------------------------
        public void Dispose()
        {
            try
            {
                _logger.Info("Cerrando sesión SL…");
                _http.PostAsync("Logout", new StringContent("")).Wait(3000);
            }
            catch { }

            try
            {
                _keepAliveToken?.Cancel();
            }
            catch { }
        }
    }
}
