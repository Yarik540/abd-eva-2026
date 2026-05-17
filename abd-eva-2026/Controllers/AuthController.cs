using abd.models.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly Supabase.Client _supabase;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public AuthController(Supabase.Client supabase, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _supabase = supabase;
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Validar campos vacíos
        if (string.IsNullOrEmpty(dto.email) || string.IsNullOrEmpty(dto.password))
        {
            sw.Stop();
            await GuardarLog(null, "login", "error", (int)sw.ElapsedMilliseconds,
                "Validación fallida: email y contraseña son obligatorios");

            return BadRequest(new
            {
                errorType = "validacion_fallida",
                message = "Email y contraseña son obligatorios."
            });
        }

        try
        {
            var session = await _supabase.Auth.SignIn(dto.email, dto.password);
            sw.Stop();

            if (session?.User == null)
            {
                await GuardarLog(null, "login", "error", (int)sw.ElapsedMilliseconds,
                    "Login fallido: usuario no registrado");

                return Unauthorized(new
                {
                    errorType = "usuario_no_existe",
                    message = "El usuario no está registrado."
                });
            }

            var rol = session.User.UserMetadata?.GetValueOrDefault("rol")?.ToString() ?? "cliente";
            await GuardarLog(session.User.Id.ToString(), "login", "exito", (int)sw.ElapsedMilliseconds,
                $"Login exitoso: {dto.email}");

            return Ok(new
            {
                token = session.AccessToken,
                usuario = session.User.Email,
                rol = rol
            });
        }
        catch (Exception)
        {
            sw.Stop();

            // Verificar si el usuario existe en auth.users
            var usuarioExiste = await VerificarUsuarioExiste(dto.email);

            if (usuarioExiste)
            {
                var userId = await ObtenerUserId(dto.email);
                await GuardarLog(userId, "login", "error", (int)sw.ElapsedMilliseconds,
                    $"Login fallido: contraseña incorrecta para {dto.email}");

                return Unauthorized(new
                {
                    errorType = "password_incorrecta",
                    message = "La contraseña es incorrecta."
                });
            }
            else
            {
                await GuardarLog(null, "login", "error", (int)sw.ElapsedMilliseconds,
                    "Login fallido: usuario no registrado");

                return Unauthorized(new
                {
                    errorType = "usuario_no_existe",
                    message = "El usuario no está registrado."
                });
            }
        }
    }

    private async Task<bool> VerificarUsuarioExiste(string email)
    {
        try
        {
            var url = _config["Supabase:Url"];
            var key = _config["Supabase:Key"];
            var client = _httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{url}/rest/v1/rpc/verificar_usuario?email={email}");
            request.Headers.Add("apikey", key);
            request.Headers.Add("Authorization", $"Bearer {key}");

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            return body.Contains("true");
        }
        catch { return false; }
    }

    private async Task<string?> ObtenerUserId(string email)
    {
        try
        {
            var url = _config["Supabase:Url"];
            var key = _config["Supabase:Key"];
            var client = _httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{url}/rest/v1/rpc/obtener_userid?email={email}");
            request.Headers.Add("apikey", key);
            request.Headers.Add("Authorization", $"Bearer {key}");

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            return body.Trim('"');
        }
        catch { return null; }
    }

    private async Task GuardarLog(string? userId, string accion, string estado, int latencia, string mensaje)
    {
        try
        {
            var url = _config["Supabase:Url"];
            var key = _config["Supabase:Key"];

            var logJson = JsonSerializer.Serialize(new
            {
                idusu = userId,
                accion = accion,
                estado = estado,
                latencia_ms = latencia,
                mensajelog = mensaje
            });

            var client = _httpClientFactory.CreateClient();
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

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        try
        {
            var authorization = Request.Headers["Authorization"].ToString();

            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
                return Unauthorized(new
                {
                    errorType = "token_requerido",
                    message = "Token requerido."
                });

            var token = authorization.Replace("Bearer ", "");
            var user = await _supabase.Auth.GetUser(token);

            if (user?.UserMetadata == null)
                return Unauthorized(new
                {
                    errorType = "token_invalido",
                    message = "Token inválido."
                });

            var rol = user.UserMetadata.GetValueOrDefault("rol")?.ToString() ?? "cliente";

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                rol = rol
            });
        }
        catch (Exception ex)
        {
            return Unauthorized(new
            {
                errorType = "token_error",
                message = ex.Message
            });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromServices] abd.Middlewares.TokenBlacklist blacklist)
    {
        var authorization = Request.Headers["Authorization"].ToString();
        string? userId = null;
        string? email = null;

        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer "))
        {
            var token = authorization.Replace("Bearer ", "");

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            userId = jwt.Subject;
            email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

            blacklist.Add(token);
        }

        await GuardarLog(userId, "logout", "exito", 0, $"Logout exitoso: {email}");
        return Ok(new { message = "Sesión cerrada" });
    }
}
