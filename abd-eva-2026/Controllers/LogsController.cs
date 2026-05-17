using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public LogsController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? estado = null,
        [FromQuery] string? accion = null,
        [FromQuery] int limit = 50)
    {
        var rol = HttpContext.Items["userRol"]?.ToString();
        if (rol != "administrador") return Forbid();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        var query = $"{url}/rest/v1/logs?select=*&order=fechalog.desc&limit={limit}";
        if (!string.IsNullOrEmpty(estado)) query += $"&estado=eq.{estado}";
        if (!string.IsNullOrEmpty(accion)) query += $"&accion=eq.{accion}";

        var request = new HttpRequestMessage(HttpMethod.Get, query);
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) return BadRequest(body);

        return Ok(JsonSerializer.Deserialize<object>(body));
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen()
    {
        var rol = HttpContext.Items["userRol"]?.ToString();
        if (rol != "administrador") return Forbid();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/logs?select=estado,accion,latencia_ms,idusu");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) return BadRequest(body);

        var logs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);

        if (logs == null || logs.Count == 0)
            return Ok(new { mensaje = "Sin logs registrados" });

        var porAccion = logs
            .GroupBy(l => l.ContainsKey("accion") && l["accion"].ValueKind != JsonValueKind.Null
                ? l["accion"].GetString()
                : "desconocido")
            .Select(g => new
            {
                accion = g.Key,
                total = g.Count(),
                exitosos = g.Count(l => l.ContainsKey("estado") && l["estado"].GetString() == "exito"),
                errores = g.Count(l => l.ContainsKey("estado") && l["estado"].GetString() == "error"),
                latencia_promedio_ms = Math.Round(g.Average(l =>
                    l.ContainsKey("latencia_ms") && l["latencia_ms"].ValueKind != JsonValueKind.Null
                        ? l["latencia_ms"].GetDouble()
                        : 0), 2)
            }).ToList();

        var erroresPorUsuario = logs
            .Where(l => l.ContainsKey("estado") && l["estado"].GetString() == "error")
            .GroupBy(l => l.ContainsKey("idusu") && l["idusu"].ValueKind != JsonValueKind.Null
                ? l["idusu"].GetString()
                : "desconocido")
            .Select(g => new
            {
                idusu = g.Key,
                total_errores = g.Count()
            }).ToList();

        return Ok(new
        {
            total_logs = logs.Count,
            total_exitosos = logs.Count(l => l.ContainsKey("estado") && l["estado"].GetString() == "exito"),
            total_errores = logs.Count(l => l.ContainsKey("estado") && l["estado"].GetString() == "error"),
            por_accion = porAccion,
            errores_por_usuario = erroresPorUsuario
        });
    }

    [HttpGet("usuario/{idusu}")]
    public async Task<IActionResult> GetLogsPorUsuario(string idusu, [FromQuery] int limit = 20)
    {
        var rol = HttpContext.Items["userRol"]?.ToString();
        if (rol != "administrador") return Forbid();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/logs?select=*&idusu=eq.{idusu}&order=fechalog.desc&limit={limit}");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) return BadRequest(body);

        return Ok(JsonSerializer.Deserialize<object>(body));
    }

    [HttpGet("mios")]
    public async Task<IActionResult> GetMisLogs([FromQuery] int limit = 20)
    {
        var userId = HttpContext.Items["userId"]?.ToString();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/logs?select=*&idusu=eq.{userId}&order=fechalog.desc&limit={limit}");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) return BadRequest(body);

        return Ok(JsonSerializer.Deserialize<object>(body));
    }
}