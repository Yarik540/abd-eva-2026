using abd.Services;
using abd_eva_2026.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

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


        if (string.IsNullOrEmpty(dto.autor))
        {
            await GuardarLog(client, url, key, userId, "insertar_registro", "error", 0, "Validación fallida: autor obligatorio");
            return BadRequest("El autor es obligatorio");
        }

        if (string.IsNullOrEmpty(dto.tipo))
        {
            await GuardarLog(client, url, key, userId, "insertar_registro", "error", 0, "Validación fallida: tipo de operación obligatorio");
            return BadRequest("El tipo de operación es obligatorio");
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

        // --- BLOQUE DE SIMILITUD ---
        if (nuevo != null && nuevo.ContainsKey("idreg"))
        {
            var idreg = nuevo["idreg"].GetInt32();
            var textoParaEmbedding = $"{dto.titulolibro} {dto.autor} {dto.contenidoreg}";

            // Generar embedding para comparación
            var embedding = await _embeddingService.GenerarEmbeddingAsync(textoParaEmbedding);

            var bodyObj = new
            {
                query_embedding = embedding,
                similitud_minima = 0.75f, // el más bajo de los umbrales
                cantidad_resultados = 5
            };

            var jsonBusqueda = JsonSerializer.Serialize(bodyObj);
            var requestBusqueda = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/rpc/buscar_registros_semanticos");
            requestBusqueda.Headers.Add("apikey", key);
            requestBusqueda.Headers.Add("Authorization", $"Bearer {key}");
            requestBusqueda.Content = new StringContent(jsonBusqueda, Encoding.UTF8, "application/json");

            var responseBusqueda = await client.SendAsync(requestBusqueda);
            var bodyBusqueda = await responseBusqueda.Content.ReadAsStringAsync();
            var similares = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(bodyBusqueda);

            if (similares != null)
            {
                similares = similares
                    .Where(r => r["idreg"].GetInt32() != idreg)
                    .ToList();

                if (similares.Count > 0)
                {
                    var maxSimilitud = similares.Max(r => r["similitud"].GetDouble());

                    if (maxSimilitud >= 0.95)
                    {
                        await GuardarLog(client, url, key, userId,
                            "registro_duplicado",
                            "detectado",
                            (int)sw.ElapsedMilliseconds,
                            $"Duplicado exacto detectado con similitud {maxSimilitud}");
                    }
                    else if (maxSimilitud >= 0.85)
                    {
                        await GuardarLog(client, url, key, userId,
                            "registro_similar_alto",
                            "detectado",
                            (int)sw.ElapsedMilliseconds,
                            $"Registro muy similar detectado con similitud {maxSimilitud}");
                    }
                    else if (maxSimilitud >= 0.75)
                    {
                        await GuardarLog(client, url, key, userId,
                            "registro_similar_medio",
                            "detectado",
                            (int)sw.ElapsedMilliseconds,
                            $"Registro algo similar detectado con similitud {maxSimilitud}");
                    }
                    var promedioSimilitud = similares.Average(r => r["similitud"].GetDouble());

                    await GuardarLog(client, url, key, userId,
                        "busqueda_semantica",
                        "exito",
                        (int)sw.ElapsedMilliseconds,
                        $"Promedio de similitud: {promedioSimilitud:F2}");
                }

            }

            // Guardar embedding en registros_vectores
            await GuardarEmbedding(client, url, key, idreg, textoParaEmbedding);
        }
        // --- FIN BLOQUE DE SIMILITUD ---

        return Ok(new
        {
            registro = nuevo,
            latencia_ms = sw.ElapsedMilliseconds
        });
    }


    private async Task GuardarEmbedding(HttpClient client, string url, string key, int idreg, string texto)
    {
        var embedding = await _embeddingService.GenerarEmbeddingAsync(texto, true);

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