using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class ConsultasController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public ConsultasController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    [HttpPost]
    public async Task<IActionResult> GuardarConsulta([FromBody] JsonElement rawBody)
    {
        try
        {
            var url = _config["Supabase:Url"];
            var key = _config["Supabase:Key"];
            var client = _httpClientFactory.CreateClient();
            var userId = HttpContext.Items["userId"]?.ToString();

            // Extraemos los datos del JSON de forma manual para evitar errores de mapeo
            string? pregunta = rawBody.TryGetProperty("pregunta", out var p) ? p.GetString() : null;
            string? respuesta = rawBody.TryGetProperty("respuesta", out var r) ? r.GetString() : null;
            string? idusu = rawBody.TryGetProperty("idusu", out var u) ? u.GetString() : userId;
            double similitud = rawBody.TryGetProperty("similitud", out var s) ? s.GetDouble() : 0.9;
            int tiempo = rawBody.TryGetProperty("tiempo_consulta_ms", out var t) ? t.GetInt32() : 150;
            bool exito = rawBody.TryGetProperty("exito", out var e) ? e.GetBoolean() : true;

            var datosParaGuardar = new
            {
                pregunta = pregunta,
                respuesta = respuesta,
                fecha = DateTime.UtcNow,
                idusu = idusu,
                similitud = similitud,
                tiempo_consulta_ms = tiempo,
                exito = exito
            };

            var json = JsonSerializer.Serialize(datosParaGuardar);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/consultas_agente");
            request.Headers.Add("apikey", key);
            request.Headers.Add("Authorization", $"Bearer {key}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return BadRequest(body);

            return Ok(new { message = "Consulta guardada exitosamente" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    // GET api/consultas/metricas — métricas de uso (solo admin)
    [HttpGet("metricas")]
    public async Task<IActionResult> GetMetricas()
    {
        var rol = HttpContext.Items["userRol"]?.ToString();

        if (rol != "administrador")
            return Forbid(); // Bloquea acceso a clientes

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        // Traer todas las consultas
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/consultas_agente?select=*");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return BadRequest(body);

        var consultas = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body) ?? new();

        // 1. Total de preguntas realizadas
        var totalPreguntas = consultas.Count;

        // 2. Preguntas frecuentes (Top 5)
        var preguntasFrecuentes = consultas
            .GroupBy(c => c["pregunta"].GetString())
            .Select(g => new { pregunta = g.Key, frecuencia = g.Count() })
            .OrderByDescending(x => x.frecuencia)
            .Take(5)
            .ToList();

        // 3. Consultas exitosas
        var consultasExitosas = consultas.Count(c => c["exito"].GetBoolean());

        // 4. Consultas sin resultados relevantes
        var consultasSinResultados = consultas.Count(c =>
            (!c.ContainsKey("respuesta") || string.IsNullOrEmpty(c["respuesta"].GetString())) ||
            (c.ContainsKey("similitud") && c["similitud"].GetDecimal() < 0.3M)
        );

        return Ok(new
        {
            total_preguntas = totalPreguntas,
            preguntas_frecuentes = preguntasFrecuentes,
            consultas_exitosas = consultasExitosas,
            consultas_sin_resultados = consultasSinResultados
        });
    }

}
