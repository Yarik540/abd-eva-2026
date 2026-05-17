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
    public async Task<IActionResult> GetDashboard()
    {
        var rol = HttpContext.Items["userRol"]?.ToString();
        var userId = HttpContext.Items["userId"]?.ToString();

        if (rol != "administrador")
            return Forbid();

        var url = _config["Supabase:Url"];
        var key = _config["Supabase:Key"];
        var client = _httpClientFactory.CreateClient();

        // ── RESUMEN GENERAL ──
        var totalRegistros = await ContarRegistros(client, url, key);
        var serviceRoleKey = _config["Supabase:ServiceRoleKey"];
        var totalUsuarios = await ContarUsuarios(client, url, serviceRoleKey);
        var totalErrores = await ContarErrores(client, url, key);
        var tasaExito = await GetTasaExito(client, url, key);
        var totalConsultas = await ContarConsultasAgente(client, url, key);

        // ── RENDIMIENTO VECTORIAL ──
        var latenciaPromedio = await GetLatenciaPromedio(client, url, key);
        var tiempoConsultaSemantica = await GetTiempoPromedioConsultaSemantica(client, url, key);
        var tiempoEmbeddings = await GetTiempoPromedioEmbeddings(client, url, key);
        var totalVectores = await ContarVectores(client, url, key);

        // ── ACTIVIDAD DE USUARIOS ──
        var registrosPorUsuario = await GetRegistrosPorUsuario(client, url, key);
        var ultimosRegistros = await GetUltimosRegistros(client, url, key);

        // ── CALIDAD DE DATOS ──
        var registrosIncompletos = await ContarRegistrosIncompletos(client, url, key);
        var duplicadosOSimilares = await ContarDuplicadosOSimilares(client, url, key);
        var nivelPromedioSimilitud = await GetNivelPromedioSimilitud(client, url, key);
        var registrosRechazados = await ContarRegistrosRechazados(client, url, key);
        var ultimosErrores = await GetUltimosErrores(client, url, key);

        return Ok(new
        {
            resumen = new
            {
                total_registros = totalRegistros,
                total_usuarios = totalUsuarios,
                total_errores = totalErrores,
                tasa_exito = tasaExito,
                total_consultas_agente = totalConsultas
            },
            rendimiento_vectorial = new
            {
                latencia_promedio_ms = latenciaPromedio,
                tiempo_promedio_consulta_semantica_ms = tiempoConsultaSemantica,
                tiempo_promedio_generacion_embeddings_ms = tiempoEmbeddings,
                total_vectores_almacenados = totalVectores
            },
            actividad_usuarios = new
            {
                registros_por_usuario = registrosPorUsuario,
                ultimos_registros = ultimosRegistros
            },
            calidad_datos = new
            {
                registros_incompletos = registrosIncompletos,
                registros_duplicados_o_similares = duplicadosOSimilares,
                nivel_promedio_similitud = nivelPromedioSimilitud,
                registros_rechazados = registrosRechazados,
                ultimos_errores = ultimosErrores
            }
        });
    }

    // ── RESUMEN ──

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
    private async Task<int> ContarUsuarios(HttpClient client, string url, string serviceRoleKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/rpc/contar_usuarios");
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        return int.TryParse(body, out var count) ? count : 0;
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

    private async Task<double> GetLatenciaPromedio(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/logs?select=latencia_ms&estado=eq.exito&accion=eq.insertar_registro");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var logs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);
        if (logs == null || logs.Count == 0) return 0;
        return Math.Round(logs.Average(l => l["latencia_ms"].GetDouble()), 2);
    }

    // ── RENDIMIENTO VECTORIAL ──

    private async Task<double> GetTiempoPromedioConsultaSemantica(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/logs?select=latencia_ms&accion=eq.busqueda_semantica&estado=eq.exito");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var logs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);
        if (logs == null || logs.Count == 0) return 0;
        return Math.Round(logs.Average(l => l["latencia_ms"].GetDouble()), 2);
    }


    private async Task<int> ContarConsultasExitosas(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/consultas_agente?select=*&exito=eq.true");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var lista = JsonSerializer.Deserialize<List<JsonElement>>(body);
        return lista?.Count ?? 0;
    }

    private async Task<int> ContarConsultasSinResultado(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/consultas_agente?select=*&exito=eq.false");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var lista = JsonSerializer.Deserialize<List<JsonElement>>(body);
        return lista?.Count ?? 0;
    }

    private async Task<int> ContarVectores(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/registros_vectores?select=*");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var lista = JsonSerializer.Deserialize<List<JsonElement>>(body);
        return lista?.Count ?? 0;
    }

    private async Task<double> GetTiempoPromedioEmbeddings(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/logs?select=tiempo_embedding_ms&accion=eq.generar_embedding&estado=eq.exito");

        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        var registros = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);
        if (registros == null || registros.Count == 0) return 0;

        return Math.Round(registros.Average(r => r["tiempo_embedding_ms"].GetDouble()), 2);
    }


    // ── ACTIVIDAD USUARIOS ──

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
            .ToList() ?? new();
    }

    private async Task<object> GetUltimosRegistros(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/registros?select=*&order=fechareg.desc&limit=5");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<object>(body) ?? new();
    }

    // ── CALIDAD DE DATOS ──

    private async Task<object> GetUltimosErrores(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/rest/v1/logs?select=*&estado=eq.error&order=fechalog.desc&limit=5");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<object>(body) ?? new();
    }
    private async Task<int> ContarRegistrosIncompletos(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/logs?select=*&accion=eq.insertar_registro&estado=eq.error");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var lista = JsonSerializer.Deserialize<List<JsonElement>>(body);
        return lista?.Count ?? 0;
    }
    private async Task<int> ContarDuplicadosOSimilares(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/logs?select=*&accion=in.(registro_duplicado,registro_similar_alto,registro_similar_medio)");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var lista = JsonSerializer.Deserialize<List<JsonElement>>(body);
        return lista?.Count ?? 0;
    }

    private async Task<double> GetNivelPromedioSimilitud(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/logs?select=mensajelog&accion=eq.busqueda_semantica&estado=eq.exito");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var logs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(body);

        if (logs == null || logs.Count == 0) return 0;

        var valores = logs
            .Select(l => {
                var msg = l["mensajelog"].GetString();
                if (msg != null && msg.Contains("Promedio de similitud"))
                {
                    var partes = msg.Split(':');
                    if (partes.Length == 2 && double.TryParse(partes[1], out var val))
                        return val;
                }
                return (double?)null;
            })
            .Where(v => v.HasValue)
            .Select(v => v.Value)
            .ToList();

        return valores.Count > 0 ? Math.Round(valores.Average(), 2) : 0;
    }
    private async Task<int> ContarRegistrosRechazados(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{url}/rest/v1/logs?select=*&accion=eq.insertar_registro&estado=eq.error");
        request.Headers.Add("apikey", key);
        request.Headers.Add("Authorization", $"Bearer {key}");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var lista = JsonSerializer.Deserialize<List<JsonElement>>(body);
        return lista?.Count ?? 0;
    }

}