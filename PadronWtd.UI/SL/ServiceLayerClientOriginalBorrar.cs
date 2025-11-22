using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PadronWtd.UI.SL
{
    public class ServiceLayerAuthException : Exception
    {
        public ServiceLayerAuthException(string msg) : base(msg) { }
    }

    public class ServiceLayerClientOriginalBorrar : IDisposable
    {
        private readonly Uri _baseUri;

        private HttpClient _client;
        private CookieContainer _cookies;

        private string _user;
        private string _pass;
        private string _company;

        private string _sessionId;
        private string _routeId; // para sticky session

        public ServiceLayerClientOriginalBorrar(string baseUrl)
        {
            _baseUri = new Uri(baseUrl);
        }

        // ============================================================
        // LOGIN (con manejo de cluster)
        // ============================================================
        public async Task<bool> LoginAsync(string user, string pass, string company)
        {
            _user = user;
            _pass = pass;
            _company = company;

            int intentos = 0;
            const int maxIntentos = 5;

            while (intentos < maxIntentos)
            {
                intentos++;

                Console.WriteLine("Intento de login #" + intentos);

                _cookies = new CookieContainer();

                var handler = new HttpClientHandler();
                handler.CookieContainer = _cookies;
                handler.UseCookies = true;
                handler.ServerCertificateCustomValidationCallback =
                    (a, b, c, d) => true;

                var loginClient = new HttpClient(handler);
                loginClient.BaseAddress = _baseUri;

                var body = new
                {
                    UserName = user,
                    Password = pass,
                    CompanyDB = company
                };

                string json = JsonConvert.SerializeObject(body);

                HttpResponseMessage response = null;
                try
                {
                    response = await loginClient.PostAsync(
                        "Login",
                        new StringContent(json, Encoding.UTF8, "application/json")
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR login: " + ex.Message);
                    continue;
                }

                var text = await response.Content.ReadAsStringAsync();

                Console.WriteLine("STATUS = " + response.StatusCode);
                Console.WriteLine("BODY = " + text);

                // ERROR CLÁSICO DE CLUSTER:
                // 500 + BODY vacío = nodo SL roto
                if (response.StatusCode == HttpStatusCode.InternalServerError &&
                    string.IsNullOrEmpty(text))
                {
                    Console.WriteLine("Nodo SL falló. Reintentando con otro nodo...");
                    await Task.Delay(400);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Login fallo en intento " + intentos);
                    await Task.Delay(300);
                    continue;
                }

                dynamic js = JsonConvert.DeserializeObject(text);
                _sessionId = js.SessionId;

                // Leer ROUTEID (si existe)
                _routeId = null;
                var col = _cookies.GetCookies(_baseUri);
                foreach (Cookie ck in col)
                    if (ck.Name.ToUpper().Contains("ROUTE"))
                        _routeId = ck.Value;

                Console.WriteLine("Login OK con SessionId " + _sessionId +
                    " en nodo " + _routeId);

                // Crear cliente real
                _client = new HttpClient(new HttpClientHandler
                {
                    CookieContainer = _cookies,
                    UseCookies = true,
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true
                });

                _client.BaseAddress = _baseUri;
                return true;
            }

            Console.WriteLine("NO SE LOGUEO tras " + maxIntentos + " intentos.");
            return false;
        }

        // ============================================================
        // Re-login automático
        // ============================================================
        private async Task Relogin()
        {
            Console.WriteLine("Haciendo re-login automático…");

            bool ok = await LoginAsync(_user, _pass, _company);
            if (!ok)
                throw new ServiceLayerAuthException("Re-login falló.");
        }

        private void EnsureLogged()
        {
            if (_client == null)
                throw new Exception("Debe llamar LoginAsync() primero");
        }

        // ============================================================
        // POST genérico con reintento + manejo cluster
        // ============================================================
        public async Task<string> PostAsync(string path, object data)
        {
            EnsureLogged();

            string json = JsonConvert.SerializeObject(data);

            HttpResponseMessage resp = null;
            string txt = null;

            try
            {
                resp = await _client.PostAsync(
                    path,
                    new StringContent(json, Encoding.UTF8, "application/json")
                );

                txt = await resp.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR POST: " + ex.Message);
                throw;
            }

            // Ver si expira sesión
            if (resp.StatusCode == HttpStatusCode.Unauthorized ||
                (txt != null && txt.Contains("SessionTimeout")))
            {
                Console.WriteLine("POST → SessionTimeout. Haciendo Re-login...");
                await Relogin();
                return await PostAsync(path, data);
            }

            // Si SL devuelve 500 pero sigue con cookie ROUTEID,
            // puede ser un nodo caído → reintentar 1 vez.
            if ((int)resp.StatusCode == 500)
            {
                Console.WriteLine("POST → Error 500. Reintentando 1 vez...");
                await Relogin();
                return await PostAsync(path, data);
            }

            if (!resp.IsSuccessStatusCode)
                throw new Exception("SL Error: " + txt);

            return txt;
        }

        public void Dispose()
        {
            if (_client != null)
                _client.Dispose();
        }
    }
}
