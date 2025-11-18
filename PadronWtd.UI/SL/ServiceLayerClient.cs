using Newtonsoft.Json;
using PadronWtd
    .UI.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PadronWtd.UI.SL
{
    public class ServiceLayerAuthException : Exception
    {
        public ServiceLayerAuthException(string msg) : base(msg) { }
    }

    public class ServiceLayerClient
    {
        private HttpClient _http;
        private string _sessionId;
        private string _routeId;

        private readonly string _baseUrl = "https://contreras-hanadb.sbo.contreras.com.ar:50000/b1s/v1/";
        private readonly string _user = "gschneider";
        private readonly string _pass = "TzLt3#MA";
        private readonly string _company = "SBP_SIOC_CHAR";

        private readonly ILogger _logger;

        private readonly HttpClientHandler _handler;

        private bool _disposed = false;

        public ServiceLayerClient(ILogger logger)
        {
            _logger = logger;
            _handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            // HttpClient inicial solo para login
            _http = new HttpClient(_handler)
            {
                BaseAddress = new Uri(_baseUrl)
            };

            _http.DefaultRequestHeaders.Connection.Add("keep-alive");
        }


        //public ServiceLayerClient(string baseUrl, ILogger logger)
        //    {
        //        _baseUrl = baseUrl.TrimEnd('/') + "/b1s/v1/";
        //        _logger = logger;

        //        _handler = new HttpClientHandler
        //        {
        //            CookieContainer = new CookieContainer(),
        //            UseCookies = true,
        //            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        //        };

        //        // HttpClient inicial solo para login
        //        _http = new HttpClient(_handler)
        //        {
        //            BaseAddress = new Uri(_baseUrl)
        //        };

        //        _http.DefaultRequestHeaders.Connection.Add("keep-alive");
        //    }

        // =============================================================
        // LOGIN
        // =============================================================
        public async Task LoginAsync()
        {
            _logger.Info("ServiceLayerClient: Login SL...");

            var body = new
            {
                UserName = _user,
                Password = _pass,
                CompanyDB = _company
            };

            _logger.Info("Login: " + _baseUrl + "Login".TrimStart('/'));
            _logger.Info("body: " + body);

            var response = await _http.PostAsync(
                _baseUrl + "Login".TrimStart('/') ,
                new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
            );

            var txt = await response.Content.ReadAsStringAsync();

            _logger.Info("respose: " + txt);
                
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error("Error conectando SL: " + body);
                _logger.Error(response.ToString());
                throw new Exception("Login SL FAILED. Status=" + response.StatusCode);
            }


            dynamic o = JsonConvert.DeserializeObject(txt);
            _sessionId = o.SessionId;
            _logger.Info("Login OK. SessionId = " + _sessionId);
            _routeId = o.RouteId;
            LogCookies();

            //-----------------------------------------------
            // RECREAR HTTPCLIENT ANCLADO AL SessionId
            //-----------------------------------------------
            string sessionUrl = $"{_baseUrl}SessionId('{_sessionId}')/";

            _logger.Info("Fijando BaseAddress → " + sessionUrl);

            _http.Dispose(); // importante
            _http = new HttpClient(_handler)
            {
                BaseAddress = new Uri(sessionUrl)
            };

            _http.DefaultRequestHeaders.Connection.Add("keep-alive");


        }




        // =============================================================
        // POST
        // =============================================================
        public async Task<string> PostAsync(string endpoint, object data)
        {
            if (_sessionId == null)
                await LoginAsync();

            var json = JsonConvert.SerializeObject(data);
            var resp = await _http.PostAsync(
                _baseUrl + endpoint.TrimStart('/'),
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            var txt = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                throw new ServiceLayerAuthException("Sesión expirada");

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Error SL: {txt}");

            return txt;
        }

            // =============================================================
            // GET
            // =============================================================
            public async Task<string> GetAsync(string endpoint)
            {
                var response = await _http.GetAsync(endpoint);
                var body = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.Warn("ServiceLayerClient: 401/301 recibido. Sesión inválida.");
                    throw new Exception("Session expired");
                }

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Error SL: " + body);

                return body;
            }

            // =============================================================
            // LOGOUT
            // =============================================================
            public async Task LogoutAsync()
            {
                try
                {
                    _logger.Info("ServiceLayerClient: Logout SL...");
                    await _http.PostAsync("Logout", null);
                }
                catch (Exception ex)
                {
                    _logger.Warn("Logout SL falló: " + ex.Message);
                }
            }

            // =============================================================
            // LOG COOKIES
            // =============================================================
            private void LogCookies()
            {
                var cookies = _handler.CookieContainer.GetCookies(new Uri(_baseUrl));

                foreach (Cookie c in cookies)
                {
                    _logger.Info($"Cookie: {c.Name} = {c.Value}");
                }
            }

            // =============================================================
            // DISPOSE
            // =============================================================
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                _http?.Dispose();
            }
        }
    
}