using abd.models.DTOs;
using abd.Services;


using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class RegistrosController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly abd.Services.EmbeddingService _embeddingService;

    public RegistrosController(IHttpClientFactory httpClientFactory, IConfiguration config, abd.Services.EmbeddingService embeddingService)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _embeddingService = embeddingService;
    }
    [HttpPost]
    public async Task<IActionResult> CrearRegistro([FromBody] RegistroCreateDTO dto)
    {
        var userId = HttpContext.Items["userId"]?.ToString();

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("Usuario no autenticado");

        if (string.IsNullOrEmpty(dto.titulolibro))
            return BadRequest("El título del libro es obligatorio");
        if (string.IsNullOrEmpty(dto.tipo))
            return BadRequest("El tipo de operación es obligatorio");

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var json = JsonSerializer.Serialize(new
        {
            contenidoreg = dto.contenidoreg,
            titulolibro = dto.titulolibro,
            autor = dto.autor,
            tipo = dto.tipo,
            idusu = userId
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/registros");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        request.Headers.Add("Prefer", "return=representation");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        sw.Stop();

        var body = await response.Content.ReadAsStringAsync();
        var estado = response.IsSuccessStatusCode ? "exito" : "error";
        var mensaje = response.IsSuccessStatusCode ? $"Registro insertado: {dto.titulolibro}" : body;

        // Guardar log
        await GuardarLog(client, url, key, userId, "insertar_registro", estado, (int)sw.ElapsedMilliseconds, mensaje);

        if (!response.IsSuccessStatusCode)
            return BadRequest(body);

        var lista = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);
        var nuevo = lista?.FirstOrDefault();

        // Guardar embedding
        if (nuevo != null && nuevo.ContainsKey("idreg"))
        {
            var idreg = nuevo["idreg"].GetInt32();
            var textoParaEmbedding =
    $"{dto.titulolibro} {dto.autor} {dto.contenidoreg}";

            await GuardarEmbedding(
                client,
                url,
                key,
                idreg,
                textoParaEmbedding
            );
        }

        return Ok(new
        {
            registro = nuevo,
            latencia_ms = sw.ElapsedMilliseconds
        });
    }
    private async Task GuardarEmbedding(HttpClient client, string url, string key, int idreg, string texto)
    {
        var embedding = await _embeddingService.GenerarEmbeddingAsync(texto);

        // ← mandar como array, no como string
        var jsonObj = new
        {
            idreg = idreg,
            embedding = embedding
        };

        var json = JsonSerializer.Serialize(jsonObj);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/registros_vectores");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Embedding guardado: {response.StatusCode} - {body}");
    }

    private async Task GuardarLog(HttpClient client, string url, string key, string userId, string accion, string estado, int latencia, string mensaje)
    {
        var logJson = JsonSerializer.Serialize(new
        {
            idusu = userId,
            accion = accion,
            estado = estado,
            latencia_ms = latencia,
            mensajelog = mensaje
        });

        var logRequest = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/logs");
        logRequest.Headers.Add("apikey", key);
        logRequest.Headers.Add("Authorization", $"Bearer {key}");
        logRequest.Content = new StringContent(logJson, Encoding.UTF8, "application/json");

        await client.SendAsync(logRequest);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerRegistros([FromQuery] string? p_idusu)
    {
        var userId = HttpContext.Items["userId"]?.ToString();
        var rol = HttpContext.Items["userRol"]?.ToString();

        // Si viene p_idusu de n8n o similar, lo priorizamos
        var finalUserId = !string.IsNullOrEmpty(p_idusu) ? p_idusu : userId;

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        var query = rol == "administrador" && string.IsNullOrEmpty(p_idusu)
            ? $"{url}/rest/v1/registros?select=*&order=idreg.desc"
            : $"{url}/rest/v1/registros?select=*&idusu=eq.{finalUserId}&order=idreg.desc";

        var request = new HttpRequestMessage(HttpMethod.Get, query);
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return BadRequest(body);

        return Ok(JsonSerializer.Deserialize<object>(body));
    }

    [HttpGet("conteo")]
    public async Task<IActionResult> ObtenerConteo([FromQuery] string? p_idusu)
    {
        var userId = HttpContext.Items["userId"]?.ToString();
        var finalUserId = !string.IsNullOrEmpty(p_idusu) ? p_idusu : userId;

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        var query = $"{url}/rest/v1/registros?select=idreg&idusu=eq.{finalUserId}";
        var request = new HttpRequestMessage(HttpMethod.Get, query);
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return BadRequest(body);

        var lista = JsonSerializer.Deserialize<List<object>>(body);
        return Ok(new { total_registros = lista?.Count ?? 0 });
    }
}