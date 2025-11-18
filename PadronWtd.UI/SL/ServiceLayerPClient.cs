using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PadronWtd.ServiceLayer
{
    public class ServiceLayerPClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly CookieContainer _cookies;
        private readonly string _baseUrl;

        private string _sessionId;
        private DateTime _sessionExpires;

        public ServiceLayerPClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');

            _cookies = new CookieContainer();

            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                {
                    // Aceptar certificados self-signed
                    return true;
                },
                UseCookies = true
            };

            _http = new HttpClient(handler);
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // =====================================
        // LOGIN
        // =====================================
        public async Task LoginAsync(string user, string password, string company)
        {
            var body = new
            {
                UserName = user,
                Password = password,
                CompanyDB = company
            };

            var json = JsonConvert.SerializeObject(body);

            var response = await _http.PostAsync(
                $"{_baseUrl}/Login",
                new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            );

            string content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Login failed: {response.StatusCode} - {content}");
            }

            dynamic o = JsonConvert.DeserializeObject(content);

            _sessionId = o.SessionId;
            int timeout = o.SessionTimeout;

            _sessionExpires = DateTime.Now.AddMinutes(timeout - 1); // margen

            // A partir del login la cookie B1SESSION queda guardada en el CookieContainer
        }

        // =====================================
        // LOGOUT
        // =====================================
        public async Task LogoutAsync()
        {
            await _http.PostAsync($"{_baseUrl}/Logout", null);
        }

        // =====================================
        // Petición común con retry por SessionTimeout
        // =====================================
        public async Task<string> GetAsync(string relativeUrl)
        {
            if (SessionExpired())
            {
                await ReloginAsync();
            }

            HttpResponseMessage response = await _http.GetAsync($"{_baseUrl}/{relativeUrl}");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Sesión expirada → volver a loguear
                await ReloginAsync();

                response = await _http.GetAsync($"{_baseUrl}/{relativeUrl}");
            }

            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"GET failed: {response.StatusCode} - {content}");
            }

            return content;
        }

        // =====================================
        // Verifica si expira
        // =====================================
        private bool SessionExpired()
        {
            if (string.IsNullOrEmpty(_sessionId))
                return true;

            return DateTime.Now >= _sessionExpires;
        }

        // =====================================
        // Relogin automático
        // =====================================
        private async Task ReloginAsync()
        {
            if (string.IsNullOrEmpty(_lastUser) ||
                string.IsNullOrEmpty(_lastPass) ||
                string.IsNullOrEmpty(_lastCompany))
            {
                throw new Exception("Cannot re-login: missing stored credentials.");
            }

            await LoginAsync(_lastUser, _lastPass, _lastCompany);
        }

        // =====================================
        // Guardar credenciales para el re-login
        // =====================================
        private string _lastUser;
        private string _lastPass;
        private string _lastCompany;

        public async Task LoginWithRetryAsync(string user, string pass, string company)
        {
            _lastUser = user;
            _lastPass = pass;
            _lastCompany = company;

            await LoginAsync(user, pass, company);
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
