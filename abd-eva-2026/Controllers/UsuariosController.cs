using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public UsuariosController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    // GET api/usuarios — lista de usuarios con stats (solo admin)
    [HttpGet]
    public async Task<IActionResult> GetUsuarios()
    {
        var rol = HttpContext.Items["userRol"]?.ToString();

        if (rol != "administrador")
            return Forbid();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        // Traer usuarios desde auth.users via admin API
        var requestUsuarios = new HttpRequestMessage(HttpMethod.Get, $"{url}/auth/v1/admin/users");
        requestUsuarios.Headers.Add("apikey", key);
        requestUsuarios.Headers.Add("Authorization", $"Bearer {key}");

        var responseUsuarios = await client.SendAsync(requestUsuarios);
        var bodyUsuarios = await responseUsuarios.Content.ReadAsStringAsync();

        if (!responseUsuarios.IsSuccessStatusCode)
            return BadRequest(bodyUsuarios);

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bodyUsuarios);
        var usuarios = data?["users"].EnumerateArray().ToList() ?? new();

        // Traer registros
        var requestRegistros = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/registros?select=idusu");
        requestRegistros.Headers.Add("apikey", key);
        requestRegistros.Headers.Add("Authorization", $"Bearer {key}");
        var responseRegistros = await client.SendAsync(requestRegistros);
        var bodyRegistros = await responseRegistros.Content.ReadAsStringAsync();
        var registros = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(bodyRegistros) ?? new();

        // Traer logs
        var requestLogs = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/logs?select=idusu,estado");
        requestLogs.Headers.Add("apikey", key);
        requestLogs.Headers.Add("Authorization", $"Bearer {key}");
        var responseLogs = await client.SendAsync(requestLogs);
        var bodyLogs = await responseLogs.Content.ReadAsStringAsync();
        var logs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(bodyLogs) ?? new();

        // Traer consultas agente
        var requestConsultas = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/consultas_agente?select=idusu,exito");
        requestConsultas.Headers.Add("apikey", key);
        requestConsultas.Headers.Add("Authorization", $"Bearer {key}");
        var responseConsultas = await client.SendAsync(requestConsultas);
        var bodyConsultas = await responseConsultas.Content.ReadAsStringAsync();
        var consultas = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(bodyConsultas) ?? new();

        // Combinar todo por usuario
        var resultado = usuarios.Select(u =>
        {
            var id = u.GetProperty("id").GetString();
            var email = u.TryGetProperty("email", out var e) ? e.GetString() : "sin email";
            var rol_usuario = u.TryGetProperty("user_metadata", out var meta)
                && meta.TryGetProperty("rol", out var r) ? r.GetString() : "cliente";
            var createdAt = u.TryGetProperty("created_at", out var ca) ? ca.GetString() : null;

            var totalRegistros = registros.Count(r => r["idusu"].GetString() == id);
            var totalErrores = logs.Count(l => l["idusu"].GetString() == id && l["estado"].GetString() == "error");
            var totalConsultas = consultas.Count(c => c["idusu"].GetString() == id);
            var consultasExitosas = consultas.Count(c => c["idusu"].GetString() == id && c["exito"].GetBoolean());

            return new
            {
                idusu = id,
                email,
                rol = rol_usuario,
                created_at = createdAt,
                stats = new
                {
                    total_registros = totalRegistros,
                    total_errores = totalErrores,
                    total_consultas_agente = totalConsultas,
                    consultas_exitosas = consultasExitosas
                }
            };
        }).ToList();

        return Ok(resultado);
    }

    // GET api/usuarios/{idusu} — detalle de un usuario (solo admin)
    [HttpGet("{idusu}")]
    public async Task<IActionResult> GetUsuario(string idusu)
    {
        var rol = HttpContext.Items["userRol"]?.ToString();

        if (rol != "administrador")
            return Forbid();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        // Registros del usuario
        var reqRegistros = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/registros?select=*&idusu=eq.{idusu}&order=fechareg.desc");
        reqRegistros.Headers.Add("apikey", key);
        reqRegistros.Headers.Add("Authorization", $"Bearer {key}");
        var resRegistros = await client.SendAsync(reqRegistros);
        var registros = JsonSerializer.Deserialize<object>(await resRegistros.Content.ReadAsStringAsync());

        // Logs del usuario
        var reqLogs = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/logs?select=*&idusu=eq.{idusu}&order=fechalog.desc&limit=10");
        reqLogs.Headers.Add("apikey", key);
        reqLogs.Headers.Add("Authorization", $"Bearer {key}");
        var resLogs = await client.SendAsync(reqLogs);
        var logs = JsonSerializer.Deserialize<object>(await resLogs.Content.ReadAsStringAsync());

        // Consultas del usuario
        var reqConsultas = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/consultas_agente?select=*&idusu=eq.{idusu}&order=fecha.desc&limit=10");
        reqConsultas.Headers.Add("apikey", key);
        reqConsultas.Headers.Add("Authorization", $"Bearer {key}");
        var resConsultas = await client.SendAsync(reqConsultas);
        var consultas = JsonSerializer.Deserialize<object>(await resConsultas.Content.ReadAsStringAsync());

        return Ok(new
        {
            idusu,
            registros,
            logs,
            consultas
        });
    }

    // GET api/usuarios/perfil — datos del cliente autenticado
    [HttpGet("perfil")]
    public async Task<IActionResult> GetPerfil()
    {
        var userId = HttpContext.Items["userId"]?.ToString();

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        // Sus registros
        var reqRegistros = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/registros?select=*&idusu=eq.{userId}&order=fechareg.desc&limit=5");
        reqRegistros.Headers.Add("apikey", key);
        reqRegistros.Headers.Add("Authorization", $"Bearer {key}");
        var resRegistros = await client.SendAsync(reqRegistros);
        var registros = JsonSerializer.Deserialize<List<JsonElement>>(await resRegistros.Content.ReadAsStringAsync()) ?? new();

        // Sus consultas al agente
        var reqConsultas = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/consultas_agente?select=*&idusu=eq.{userId}&order=fecha.desc&limit=5");
        reqConsultas.Headers.Add("apikey", key);
        reqConsultas.Headers.Add("Authorization", $"Bearer {key}");
        var resConsultas = await client.SendAsync(reqConsultas);
        var consultas = JsonSerializer.Deserialize<List<JsonElement>>(await resConsultas.Content.ReadAsStringAsync()) ?? new();

        return Ok(new
        {
            idusu = userId,
            stats = new
            {
                total_registros = registros.Count,
                total_consultas = consultas.Count
            },
            ultimos_registros = registros,
            ultimas_consultas = consultas
        });
    }
}