using Newtonsoft.Json;
using PadronSaltaAddOn.UI.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PadronSaltaAddOn.UI.SL
{
    public class ServiceLayerClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ILogger _logger;
        public string SessionId { get; private set; }

        public ServiceLayerClient(string baseUrl, ILogger logger)
        {
            ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => true;
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var cookies = new CookieContainer();

            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = cookies,
                AllowAutoRedirect = true,
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _logger = logger;
            _logger.Info("URL -> " + baseUrl);
            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl)     // OJO: sin "/" al final
            };

            _http.DefaultRequestHeaders.ExpectContinue = false;
            _http.DefaultRequestHeaders.ConnectionClose = false;
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/4.0");
        }

        public async Task<string> LoginAsync(string user, string pass, string company)
        {
            var payload = new
            {
                UserName = user,
                Password = pass,
                CompanyDB = company
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("Login", content);
            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Login SL FAILED. Status={response.StatusCode} BODY={body}");

            // EXTRAER COOKIE B1SESSION
            if (response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
            {
                foreach (var c in cookieHeaders)
                {
                    if (c.StartsWith("B1SESSION"))
                    {
                        SessionId = c.Split('=')[1].Split(';')[0];
                    }
                }
            }

            // AGREGAR HEADER PARA TODAS LAS LLAMADAS
            if (!string.IsNullOrWhiteSpace(SessionId))
            {
                _http.DefaultRequestHeaders.Remove("B1S-Session");
                _http.DefaultRequestHeaders.Add("B1S-Session", SessionId);
            }

            return body;
        }


        public async Task<string> PostAsync(string resource, object data)
        {
            if (string.IsNullOrWhiteSpace(SessionId))
                throw new Exception("SL Session not initialized.");

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync(resource, content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception("Error SL (" + resp.StatusCode + "): " + body);

            return body;
        }

        public void Dispose()
        {
            try
            {
                _http.PostAsync("Logout", new StringContent("")).Wait(500);
            }
            catch { }
        }
    }
}
