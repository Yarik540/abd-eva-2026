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
    [FromQuery] int top = 5)
    {
        var userId = HttpContext.Items["userId"]?.ToString();
        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        if (string.IsNullOrWhiteSpace(texto))
        {
            await GuardarLog(client, url, key, userId, "busqueda_semantica", "error", 0, "Búsqueda fallida: texto vacío");
            return BadRequest("Debe enviar un texto");
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var embedding = await _embeddingService.GenerarEmbeddingAsync(texto);

            var bodyObj = new
            {
                query_embedding = embedding,
                similitud_minima = similitudMinima,
                cantidad_resultados = top
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
            {
                await GuardarLog(client, url, key, userId, "busqueda_semantica", "error", (int)sw.ElapsedMilliseconds, $"Búsqueda fallida: {body}");
                return BadRequest(body);
            }

            await GuardarLog(client, url, key, userId, "busqueda_semantica", "exito", (int)sw.ElapsedMilliseconds, $"Búsqueda exitosa: '{texto}'");

            var resultados = JsonSerializer.Deserialize<object>(body);
            return Ok(new
            {
                consulta = texto,
                tiempo_ms = sw.ElapsedMilliseconds,
                resultados
            });
        }
        catch (Exception ex)
        {
            await GuardarLog(client, url, key, userId, "busqueda_semantica", "error", 0, $"Error: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
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

            var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/logs");
            request.Headers.Add("apikey", key);
            request.Headers.Add("Authorization", $"Bearer {key}");
            request.Content = new StringContent(logJson, Encoding.UTF8, "application/json");

            await client.SendAsync(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error guardando log: {ex.Message}");
        }
    }
}