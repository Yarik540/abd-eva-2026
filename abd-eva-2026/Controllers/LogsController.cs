using abd_eva_2026.Models.DTOs;
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
        Console.WriteLine("===== LOGS =====");

        var auth = Request.Headers["Authorization"].ToString();
        Console.WriteLine($"Authorization: {auth}");

        var userId = HttpContext.Items["userId"]?.ToString();
        var rol = HttpContext.Items["userRol"]?.ToString();

        Console.WriteLine($"UserId: {userId}");
        Console.WriteLine($"Rol: {rol}");

        if (rol != "administrador")
            return Forbid();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];

        var client = _httpClientFactory.CreateClient();

        // ==========================
        // Obtener logs
        // ==========================

        var query = $"{url}/rest/v1/logs?select=*&order=fechalog.desc&limit={limit}";

        if (!string.IsNullOrWhiteSpace(estado))
            query += $"&estado=eq.{estado}";

        if (!string.IsNullOrWhiteSpace(accion))
            query += $"&accion=eq.{accion}";

        var requestLogs = new HttpRequestMessage(HttpMethod.Get, query);

        requestLogs.Headers.Add("apikey", key);
        requestLogs.Headers.Add("Authorization", $"Bearer {key}");

        var responseLogs = await client.SendAsync(requestLogs);
        var bodyLogs = await responseLogs.Content.ReadAsStringAsync();

        if (!responseLogs.IsSuccessStatusCode)
            return BadRequest(bodyLogs);

        var logs = JsonSerializer.Deserialize<List<LogDTO>>(
            bodyLogs,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<LogDTO>();

        // ==========================
        // Obtener usuarios
        // ==========================

        var requestUsers = new HttpRequestMessage(
            HttpMethod.Get,
            $"{url}/rest/v1/usuarios?select=id,nombre,email");

        requestUsers.Headers.Add("apikey", key);
        requestUsers.Headers.Add("Authorization", $"Bearer {key}");

        var responseUsers = await client.SendAsync(requestUsers);
        var bodyUsers = await responseUsers.Content.ReadAsStringAsync();

        if (!responseUsers.IsSuccessStatusCode)
            return BadRequest(bodyUsers);

        var usuarios = JsonSerializer.Deserialize<List<UsuarioDTO>>(
            bodyUsers,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<UsuarioDTO>();

        var dicUsuarios = usuarios
            .Where(u => !string.IsNullOrEmpty(u.id))
            .ToDictionary(
                u => u.id!,
                u => !string.IsNullOrWhiteSpace(u.nombre)
                    ? u.nombre!
                    : u.email ?? "desconocido"
            );

        // ==========================
        // Combinar logs + usuarios
        // ==========================

        var resultado = logs.Select(l => new
        {
            idlog = l.idlog,
            accion = l.accion,
            estado = l.estado,
            mensajelog = l.mensajelog,
            fechalog = l.fechalog,
            latencia_ms = l.latencia_ms ?? 0,
            idusu = l.idusu,

            nombre =
                !string.IsNullOrEmpty(l.idusu) &&
                dicUsuarios.TryGetValue(l.idusu, out var nombreUsuario)
                    ? nombreUsuario
                    : "desconocido"
        });

        return Ok(resultado);
    }
    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen()
    {
        var rol = HttpContext.Items["userRol"]?.ToString();

        if (rol != "administrador")
            return Forbid();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];

        var client = _httpClientFactory.CreateClient();

        // ==========================
        // Obtener logs
        // ==========================

        var requestLogs = new HttpRequestMessage(
            HttpMethod.Get,
            $"{url}/rest/v1/logs?select=estado,accion,latencia_ms,idusu");

        requestLogs.Headers.Add("apikey", key);
        requestLogs.Headers.Add("Authorization", $"Bearer {key}");

        var responseLogs = await client.SendAsync(requestLogs);
        var bodyLogs = await responseLogs.Content.ReadAsStringAsync();

        if (!responseLogs.IsSuccessStatusCode)
            return BadRequest(bodyLogs);

        var logs = JsonSerializer.Deserialize<List<LogDTO>>(
            bodyLogs,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<LogDTO>();

        if (logs.Count == 0)
        {
            return Ok(new
            {
                total_logs = 0,
                total_exitosos = 0,
                total_errores = 0,
                por_accion = new List<object>(),
                errores_por_usuario = new List<object>()
            });
        }

        // ==========================
        // Obtener usuarios
        // ==========================

        var requestUsers = new HttpRequestMessage(
            HttpMethod.Get,
            $"{url}/rest/v1/usuarios?select=id,nombre,email,rol");

        requestUsers.Headers.Add("apikey", key);
        requestUsers.Headers.Add("Authorization", $"Bearer {key}");

        var responseUsers = await client.SendAsync(requestUsers);
        var bodyUsers = await responseUsers.Content.ReadAsStringAsync();

        if (!responseUsers.IsSuccessStatusCode)
            return BadRequest(bodyUsers);

        var usuarios = JsonSerializer.Deserialize<List<UsuarioDTO>>(
            bodyUsers,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<UsuarioDTO>();

        var dicUsuarios = usuarios
            .Where(u => !string.IsNullOrEmpty(u.id))
            .ToDictionary(
                u => u.id!,
                u => !string.IsNullOrWhiteSpace(u.nombre)
                    ? u.nombre!
                    : u.email ?? "desconocido"
            );

        // ==========================
        // Resumen por acción
        // ==========================

        var porAccion = logs
            .GroupBy(x => x.accion ?? "desconocido")
            .Select(g => new
            {
                accion = g.Key,
                total = g.Count(),
                exitosos = g.Count(x => x.estado == "exito"),
                errores = g.Count(x => x.estado == "error"),
                latencia_promedio_ms = Math.Round(
                    g.Average(x => (double)(x.latencia_ms ?? 0)),
                    2
                )
            })
            .ToList();

        // ==========================
        // Errores por usuario
        // ==========================

        var erroresPorUsuario = logs
            .Where(x => x.estado == "error")
            .GroupBy(x => x.idusu ?? "desconocido")
            .Select(g => new
            {
                idusu = g.Key,
                nombre =
                    dicUsuarios.TryGetValue(g.Key, out var nombreUsuario)
                        ? nombreUsuario
                        : "desconocido",
                total_errores = g.Count()
            })
            .ToList();

        // ==========================
        // Respuesta final
        // ==========================

        return Ok(new
        {
            total_logs = logs.Count,
            total_exitosos = logs.Count(x => x.estado == "exito"),
            total_errores = logs.Count(x => x.estado == "error"),
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