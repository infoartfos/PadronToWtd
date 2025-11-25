using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

public class ServiceLayerClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly CookieContainer _cookies;
    private readonly string _baseUrl;
    private readonly HttpClientHandler _handler;
    private readonly Uri _cookieUri;

    public ServiceLayerClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');

        // --- URI base para las cookies (solo protocolo + host + puerto) ---
        var full = new Uri(_baseUrl);
        _cookieUri = new Uri($"{full.Scheme}://{full.Host}:{full.Port}");

        _cookies = new CookieContainer();

        _handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            ServerCertificateCustomValidationCallback = (m, cert, chain, errors) => true
        };

        _http = new HttpClient(_handler);
        _http.DefaultRequestHeaders.ExpectContinue = false;
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );
    }

    // ============================================================
    // LOGIN
    // ============================================================
    public async Task LoginAsync(string user, string pass, string db)
    {
        var bodyObj = new { UserName = user, Password = pass, CompanyDB = db };
        string json = JsonConvert.SerializeObject(bodyObj);

        var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/Login");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(json);

        HttpDebug.DumpRequest(req);

        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        FixCookies(resp);

        await HttpDebug.DumpResponse(resp);

        var text = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"LOGIN FAILED {resp.StatusCode}: {text}");
    }

    // ============================================================
    // MÉTODO GENERAL
    // ============================================================
    private async Task<string> SendAsync(HttpMethod method, string relativeUrl, object body = null)
    {
        // --- DEBUG: mostrar cookies reales almacenadas ---
        foreach (Cookie c in _cookies.GetCookies(_cookieUri))
            Console.WriteLine($"COOKIE => {c.Name} = {c.Value}");

        // Construir URL final
        var url = $"{_baseUrl}/{relativeUrl.TrimStart('/')}";
        var req = new HttpRequestMessage(method, url);

        // Si tiene body
        if (body != null)
        {
            string json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.ContentLength = Encoding.UTF8.GetByteCount(json);
            req.Content = content;
        }

        HttpDebug.DumpRequest(req);

        HttpResponseMessage resp;

        try
        {
            resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            FixCookies(resp);
        }
        catch (Exception ex)
        {
            Console.WriteLine("EXCEPCIÓN EN REQUEST: " + ex);
            throw;
        }

        await HttpDebug.DumpResponse(resp);

        string respText = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"{method} FAILED {resp.StatusCode}: {respText}");

        return respText;
    }

    // Wrappers
    public Task<string> GetAsync(string relativeUrl)
        => SendAsync(HttpMethod.Get, relativeUrl);

    public Task<string> PostAsync(string relativeUrl, object body)
        => SendAsync(HttpMethod.Post, relativeUrl, body);

    public Task<string> PutAsync(string relativeUrl, object body)
        => SendAsync(HttpMethod.Put, relativeUrl, body);

    public Task<string> PatchAsync(string relativeUrl, object body)
        => SendAsync(new HttpMethod("PATCH"), relativeUrl, body);

    public Task<string> DeleteAsync(string relativeUrl)
        => SendAsync(HttpMethod.Delete, relativeUrl);

    public void Dispose() => _http.Dispose();



    private void FixCookies(HttpResponseMessage resp)
    {
        if (!resp.Headers.Contains("Set-Cookie"))
            return;

        foreach (var raw in resp.Headers.GetValues("Set-Cookie"))
        {
            // SAP manda algo como:
            // B1SESSION=xxx;HttpOnly;;Secure;SameSite=None,ROUTEID=.node3; path=/;Secure;SameSite=None

            var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var p in parts)
            {
                var cookie = p.Trim();
                if (cookie.StartsWith("B1SESSION") || cookie.StartsWith("ROUTEID"))
                {
                    try
                    {
                        _cookies.SetCookies(_cookieUri, cookie);
                    }
                    catch
                    {
                        // fallback manual
                        var kv = cookie.Split(';')[0];
                        var kvp = kv.Split('=');

                        if (kvp.Length == 2)
                        {
                            var c = new Cookie(kvp[0].Trim(), kvp[1].Trim(), "/", _cookieUri.Host);
                            _cookies.Add(c);
                        }
                    }
                }
            }
        }
    }


}
