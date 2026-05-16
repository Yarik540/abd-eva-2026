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

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        // Validaciones con log
        if (string.IsNullOrEmpty(dto.titulolibro))
        {
            await GuardarLog(client, url, key, userId, "insertar_registro", "error", 0, "Validación fallida: título del libro obligatorio");
            return BadRequest("El título del libro es obligatorio");
        }

        if (string.IsNullOrEmpty(dto.tipo))
        {
            await GuardarLog(client, url, key, userId, "insertar_registro", "error", 0, "Validación fallida: tipo de operación obligatorio");
            return BadRequest("El tipo de operación es obligatorio");
        }

        if (string.IsNullOrEmpty(dto.autor))
        {
            await GuardarLog(client, url, key, userId, "insertar_registro", "error", 0, "Validación fallida: autor obligatorio");
            return BadRequest("El autor es obligatorio");
        }

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

        await GuardarLog(client, url, key, userId, "insertar_registro", estado, (int)sw.ElapsedMilliseconds, mensaje);

        if (!response.IsSuccessStatusCode)
            return BadRequest(body);

        var lista = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);
        var nuevo = lista?.FirstOrDefault();

        // Guardar embedding
        if (nuevo != null && nuevo.ContainsKey("idreg"))
        {
            var idreg = nuevo["idreg"].GetInt32();
            var textoParaEmbedding = $"{dto.titulolibro} {dto.autor} {dto.contenidoreg}";
            await GuardarEmbedding(client, url, key, idreg, textoParaEmbedding);
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

    private async Task GuardarLog(HttpClient client, string url, string key, string? userId, string accion, string estado, int latencia, string mensaje)
    {
        try
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
        catch (Exception ex)
        {
            Console.WriteLine($"Error guardando log: {ex.Message}");
        }
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerRegistros()
    {
        var userId = HttpContext.Items["userId"]?.ToString();
        var rol = HttpContext.Items["userRol"]?.ToString();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        var query = rol == "administrador"
            ? $"{url}/rest/v1/registros?select=*&order=idreg.desc"
            : $"{url}/rest/v1/registros?select=*&idusu=eq.{userId}&order=idreg.desc";

        var request = new HttpRequestMessage(HttpMethod.Get, query);
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return BadRequest(body);

        return Ok(JsonSerializer.Deserialize<object>(body));
    }
}