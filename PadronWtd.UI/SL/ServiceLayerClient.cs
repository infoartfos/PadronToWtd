using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PadronWtd.UI.SL { 

        public class ServiceLayerAuthException : Exception
        {
            public ServiceLayerAuthException(string msg) : base(msg) { }
        }
public class ServiceLayerClient : IDisposable
    {
        private readonly Uri _baseUri;
        private HttpClient _client;
        private CookieContainer _cookieContainer;
        private string _sessionId;

        public ServiceLayerClient(string baseUrl)
        {
            _baseUri = new Uri(baseUrl);
        }

        // -----------------------------
        // LOGIN EXACTO COMO CURL
        // -----------------------------
        public async Task<bool> LoginAsync(string user, string pass, string company)
        {
            // 1) El login NO usa cookies
            var handler = new HttpClientHandler
            {
                UseCookies = false,
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true
            };

            var loginClient = new HttpClient(handler)
            {
                BaseAddress = _baseUri
            };

            var loginJson = JsonConvert.SerializeObject(new
            {
                UserName = user,
                Password = pass,
                CompanyDB = company
            });

            var req = new HttpRequestMessage(HttpMethod.Post, "Login")
            {
                Content = new StringContent(loginJson, Encoding.UTF8, "application/json")
            };

            var resp = await loginClient.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"Login ERROR {resp.StatusCode}: {body}");
                return false;
            }

            // 2) Parseo el sessionId desde la respuesta
            dynamic json = JsonConvert.DeserializeObject(body);
            _sessionId = json.SessionId;

            Console.WriteLine("Login OK. SessionId=" + _sessionId);

            // 3) CREO EL CLIENTE REAL con cookies
            _cookieContainer = new CookieContainer();
            _cookieContainer.Add(_baseUri, new Cookie("B1SESSION", _sessionId));

            var realHandler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true,
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true
            };

            _client = new HttpClient(realHandler)
            {
                BaseAddress = _baseUri
            };

            return true;
        }

        // -----------------------------
        // LOGOUT
        // -----------------------------
        public async Task LogoutAsync()
        {
            try
            {
                if (_client == null)
                    return;

                await _client.PostAsync("Logout", null);
            }
            catch { }
        }

        // -----------------------------
        // POST con autorelogin
        // -----------------------------
        public async Task<string> PostAsync(string path, object payload)
        {
            EnsureLogged();

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(path, content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                body.Contains("SessionTimeout"))
            {
                Console.WriteLine("Session expired. Doing auto re-login...");
                await ReloginAsync();
                return await PostAsync(path, payload); // retry
            }

            if (!response.IsSuccessStatusCode)
                throw new Exception($"POST {path} failed: {body}");

            return body;
        }

        // -----------------------------
        // GET con autorelogin
        // -----------------------------
        public async Task<string> GetAsync(string path)
        {
            EnsureLogged();

            var response = await _client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                body.Contains("SessionTimeout"))
            {
                Console.WriteLine("Session expired. Doing auto re-login...");
                await ReloginAsync();
                return await GetAsync(path); // retry
            }

            if (!response.IsSuccessStatusCode)
                throw new Exception($"GET {path} failed: {body}");

            return body;
        }

        // -----------------------------
        // AUTO REL0GIN
        // -----------------------------
        private async Task ReloginAsync()
        {
            Console.WriteLine("Re-login...");

            // Limpio cookies
            _cookieContainer = new CookieContainer();

            // hago login desde cero
            // OJO: aquí deberías guardar user/pass/company en campos privados
            //      para no pedirlos de nuevo
            throw new NotImplementedException("Guardá user/pass/company en variables internas para relogin automático.");
        }

        private void EnsureLogged()
        {
            if (_client == null)
                throw new Exception("No session. Call LoginAsync() first.");
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
