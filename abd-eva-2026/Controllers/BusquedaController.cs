using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using abd.Services;

[ApiController]
[Route("api/[controller]")]
public class BusquedaController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly EmbeddingService _embeddingService;

    public BusquedaController(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        EmbeddingService embeddingService)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _embeddingService = embeddingService;
    }

    [HttpGet]
    public async Task<IActionResult> BuscarSemantico(
        [FromQuery] string texto,
        [FromQuery] float similitudMinima = 0.25f,
        [FromQuery] int top = 5,
        [FromQuery] string? p_idusu = null)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return BadRequest("Debe enviar un texto");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var url = _config["Supabase:Url"];
            var key = _config["Supabase:Key"];

            var userId = !string.IsNullOrEmpty(p_idusu)
                ? p_idusu
                : HttpContext.Items["userId"]?.ToString();

            var client = _httpClientFactory.CreateClient();
            var embedding = await _embeddingService.GenerarEmbeddingAsync(texto);

            var bodyObj = new
            {
                query_embedding = embedding,
                similitud_minima = similitudMinima,
                cantidad_resultados = top,
                p_idusu = userId
            };

            var json = JsonSerializer.Serialize(bodyObj);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/rpc/buscar_registros_semanticos");
            request.Headers.Add("apikey", key);
            request.Headers.Add("Authorization", $"Bearer {key}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            sw.Stop();

            if (!response.IsSuccessStatusCode)
                return BadRequest(body);

            var resultados = JsonSerializer.Deserialize<List<object>>(body);
            Console.WriteLine($"BÚSQUEDA: '{texto}' | Usuario: {userId} | Encontrados: {resultados?.Count ?? 0}");

            return Ok(new
            {
                consulta = texto,
                tiempo_ms = sw.ElapsedMilliseconds,
                resultados
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }
}