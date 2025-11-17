using Newtonsoft.Json;
using PadronSaltaAddOn.UI.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PadronSaltaAddOn.UI.SL
{
    public class ServiceLayerClient : IDisposable
    {
        private static HttpClient _http; // singleton HttpClient (shared handler)
        private readonly HttpClientHandler _handler;
        private readonly ILogger _logger;
        private readonly string _baseUrl;
        private readonly CookieContainer _cookies = new CookieContainer();

        private readonly string _user;
        private readonly string _password;
        private readonly string _company;
        private CancellationTokenSource _keepAliveCts;

        public ServiceLayerClient(string baseUrl, string user, string password, string company, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? throw new ArgumentNullException(nameof(baseUrl)) : baseUrl.TrimEnd('/') + "/";
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _company = company ?? throw new ArgumentNullException(nameof(company));

            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            
            _handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = _cookies,
                AllowAutoRedirect = true,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            if (_http == null)
            {
                _http = new HttpClient(_handler)
                {
                    BaseAddress = new Uri(_baseUrl),
                    Timeout = TimeSpan.FromMinutes(10)
                };

                _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                _http.DefaultRequestHeaders.ExpectContinue = false;
            }
            _logger.Info($"BaseAddress = {_http.BaseAddress}");

            StartKeepAlive();
        }

        public async Task LoginAsync()
        {
            _logger.Info("ServiceLayerClient: Login SL...");
            var payload = new
            {
                UserName = _user,
                Password = _password,
                CompanyDB = _company
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, new UTF8Encoding(false), "application/json");

            HttpResponseMessage resp;
            try
            {
                resp = await _http.PostAsync("Login", content).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error("ServiceLayerClient: error en POST Login", ex);
                throw;
            }

            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.Error($"ServiceLayerClient: Login FALLÓ. Status={resp.StatusCode} Body={body}");
                throw new Exception("Login SL FAILED: " + body);
            }

            dynamic obj = JsonConvert.DeserializeObject(body);
            string sid = obj.SessionId;

            // Cambiar la URL para fijar la sesion
            _http.BaseAddress = new Uri($"{_baseUrl}SessionId('{sid}')/");

            _logger.Info("SessionId FIX aplicado. Nueva BaseAddress = " + _http.BaseAddress);


            _logger.Info("ServiceLayerClient: Login OK.");

            var cookies = _cookies.GetCookies(new Uri(_baseUrl));

            foreach (Cookie ck in cookies)
                _logger.Info($"Cookie: {ck.Name} = {ck.Value}");

            if (cookies.Count == 0)
                _logger.Error("⚠ NO SE RECIBIERON COOKIES DESDE EL SL (NO HAY SESIÓN)");

            // LOG DE COOKIES DEL SERVICE LAYER
            try
            {

                _logger.Info("Login cookies: " + string.Join(" | ",
                    cookies.Cast<Cookie>().Select(c => $"{c.Name}={c.Value}")
                ));
            }
            catch (Exception ex)
            {
                _logger.Warn("No se pudieron leer las cookies del login: " + ex.Message);
            }

        }

        public async Task<string> PostAsync(string resource, object data)
        {
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, new UTF8Encoding(false), "application/json");

            var resp = await _http.PostAsync(resource, content).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.Warn("ServiceLayerClient: 401/301 recibido (sesión inválida). Re-login automático...");
                await LoginAsync().ConfigureAwait(false);

                // reintento
                content = new StringContent(json, new UTF8Encoding(false), "application/json");
                resp = await _http.PostAsync(resource, content).ConfigureAwait(false);
                body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            if (!resp.IsSuccessStatusCode)
            {
                _logger.Error($"ServiceLayerClient: Error SL ({resp.StatusCode}) -> {body}");
                throw new Exception("Error SL (" + resp.StatusCode + "): " + body);
            }

            return body;
        }

        private void StartKeepAlive()
        {
            // keep-alive p/evitar timeouts cortos de session
            _keepAliveCts = new CancellationTokenSource();
            var ct = _keepAliveCts.Token;

            Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                        // Ping endpoint (no obliga a existir; SL devuelve 200)
                        await _http.GetAsync("Ping").ConfigureAwait(false);
                    }
                    catch { /* ignorar */ }
                }
            }, ct);
        }

        public void Dispose()
        {
            try
            {
                _logger.Info("ServiceLayerClient: Logout SL...");
                _http.PostAsync("Logout", new StringContent("")).Wait(1000);
            }
            catch { /* ignorar */ }

            try { _keepAliveCts?.Cancel(); } catch { }
        }
    }
}
