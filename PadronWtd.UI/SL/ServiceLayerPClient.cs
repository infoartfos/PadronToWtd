using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

public class ServiceLayerPClient
{
    private readonly string _baseUrl;
    private readonly string _user;
    private readonly string _password;
    private readonly string _companyDb;

    private HttpClient _client;
    private CookieContainer _cookies = new CookieContainer();

    public ServiceLayerPClient(string baseUrl, string user, string password, string companyDb)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _user = user;
        _password = password;
        _companyDb = companyDb;

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        _client = new HttpClient(handler);
        _client.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<bool> LoginAsync()
    {
        try
        {
            var json = $"{{\"UserName\":\"{_user}\",\"Password\":\"{_password}\",\"CompanyDB\":\"{_companyDb}\"}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(_baseUrl + "/Login", content);

            if (response.IsSuccessStatusCode)
            {
                // Guarda cookies (SessionId y ROUTEID)
                var setCookies = response.Headers.GetValues("Set-Cookie");
                foreach (string cookie in setCookies)
                {
                    Console.WriteLine("COOKIE => " + cookie);
                }

                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Login error: " + e.Message);
        }

        return false;
    }

    public async Task<string> GetAsync(string path)
    {
        return await SendWithFailoverAsync(() =>
            _client.GetAsync(_baseUrl + path)
        );
    }

    private async Task<string> SendWithFailoverAsync(Func<Task<HttpResponseMessage>> action)
    {
        HttpResponseMessage response = null;

        try
        {
            response = await action();

            // si el nodo está caído → SL devuelve 500/502/503
            if ((int)response.StatusCode >= 500)
            {
                Console.WriteLine("Nodo caído. Reintentando login y redirigiendo...");

                var ok = await LoginAsync();
                if (!ok) throw new Exception("No se pudo reloguear en SL.");

                response = await action(); // reintento
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error SL: " + ex.Message);
            throw;
        }
    }
}
