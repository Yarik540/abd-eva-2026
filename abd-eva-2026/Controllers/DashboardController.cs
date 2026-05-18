using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public DashboardController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboardAPI()
    {
        var rol = HttpContext.Items["userRol"]?.ToString();
        var userId = HttpContext.Items["userId"]?.ToString();

        if (rol != "administrador")
        {
            return Forbid();
        }

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        // 1. Total registros
        var totalRegistros = await ContarRegistros(client, url, key);

        // 2. Registros por usuario
        var registrosPorUsuario = await GetRegistrosPorUsuario(client, url, key);

        // 3. Latencia promedio
        var latenciaPromedio = await GetLatenciaPromedio(client, url, key);

        // 4. Total errores
        var totalErrores = await ContarErrores(client, url, key);

        // 5. Tasa de éxito
        var tasaExito = await GetTasaExito(client, url, key);

        // 6. Total consultas al agente
        var totalConsultas = await ContarConsultasAgente(client, url, key);

        // 7. Últimos registros
        var ultimosRegistros = await GetUltimosRegistros(client, url, key);

        // 8. Últimos errores
        var ultimosErrores = await GetUltimosErrores(client, url, key);

        return Ok(new
        {
            resumen = new
            {
                total_registros = totalRegistros,
                total_errores = totalErrores,
                tasa_exito = tasaExito,
                total_consultas_agente = totalConsultas,
                latencia_promedio_ms = latenciaPromedio
            },
            registros_por_usuario = registrosPorUsuario,
            ultimos_registros = ultimosRegistros,
            ultimos_errores = ultimosErrores
        });
    }

    private async Task<int> ContarRegistros(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/registros?select=*");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var lista = JsonSerializer.Deserialize<List<JsonElement>>(body);
        return lista?.Count ?? 0;
    }

    private async Task<object> GetRegistrosPorUsuario(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/registros?select=idusu&order=idusu");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var registros = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);
        return registros?
            .GroupBy(r => r["idusu"].GetString())
            .Select(g => new { idusu = g.Key, total = g.Count() })
            .ToList();
    }

    private async Task<double> GetLatenciaPromedio(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/logs?select=latencia_ms&estado=eq.exito&accion=eq.insertar_registro");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var logs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);
        if (logs == null || logs.Count == 0) return 0;
        return logs.Average(l => l["latencia_ms"].GetDouble());
    }

    private async Task<int> ContarErrores(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/logs?select=*&estado=eq.error");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var lista = JsonSerializer.Deserialize<List<JsonElement>>(body);
        return lista?.Count ?? 0;
    }

    private async Task<double> GetTasaExito(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/logs?select=estado");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var logs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);
        if (logs == null || logs.Count == 0) return 100;
        var exitosos = logs.Count(l => l["estado"].GetString() == "exito");
        return Math.Round((double)exitosos / logs.Count * 100, 2);
    }

    private async Task<int> ContarConsultasAgente(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/consultas_agente?select=*");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var lista = JsonSerializer.Deserialize<List<JsonElement>>(body);
        return lista?.Count ?? 0;
    }

    private async Task<object> GetUltimosRegistros(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/registros?select=*&order=fechareg.desc&limit=5");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<object>(body);
    }

    private async Task<object> GetUltimosErrores(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/logs?select=*&estado=eq.error&order=fechalog.desc&limit=5");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<object>(body);
    }
}