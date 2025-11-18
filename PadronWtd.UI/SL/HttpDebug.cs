using System;
using System.Net.Http;
using System.Threading.Tasks;

public static class HttpDebug
{
    public static void DumpRequest(HttpRequestMessage req)
    {
        Console.WriteLine("===== HTTP REQUEST =====");
        Console.WriteLine($"{req.Method} {req.RequestUri}");

        foreach (var h in req.Headers)
            Console.WriteLine($"{h.Key}: {string.Join(",", h.Value)}");

        if (req.Content != null)
        {
            foreach (var h in req.Content.Headers)
                Console.WriteLine($"{h.Key}: {string.Join(",", h.Value)}");

            var body = req.Content.ReadAsStringAsync().Result;
            Console.WriteLine("--- BODY ---");
            Console.WriteLine(body);
        }
        Console.WriteLine("========================");
    }

    public static async Task DumpResponse(HttpResponseMessage resp)
    {
        Console.WriteLine("===== HTTP RESPONSE =====");
        Console.WriteLine($"Status: {(int)resp.StatusCode} {resp.ReasonPhrase}");

        foreach (var h in resp.Headers)
            Console.WriteLine($"{h.Key}: {string.Join(",", h.Value)}");

        if (resp.Content != null)
        {
            foreach (var h in resp.Content.Headers)
                Console.WriteLine($"{h.Key}: {string.Join(",", h.Value)}");

            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine("--- BODY ---");
            Console.WriteLine(body);
        }
        Console.WriteLine("========================");
    }
}
