// Controllers/AuthController.cs
using abd.models.DTOs;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly Supabase.Client _supabase;

    public AuthController(Supabase.Client supabase) 
    {
        _supabase = supabase;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        try
        {
            var session = await _supabase.Auth.SignIn(dto.email, dto.password);

            if (session?.User == null)
                return Unauthorized("Credenciales incorrectas");

            var rol = session.User.UserMetadata?.GetValueOrDefault("rol")?.ToString() ?? "cliente";

            return Ok(new
            {
                token = session.AccessToken,
                usuario = session.User.Email,
                rol = rol
            });
        }
        catch (Exception ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        try
        {
            var authorization = Request.Headers["Authorization"].ToString();

            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
                return Unauthorized("Token requerido");

            var token = authorization.Replace("Bearer ", "");
            var user = await _supabase.Auth.GetUser(token);

            if (user?.UserMetadata == null)
                return Unauthorized("Token inválido");

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
            return Unauthorized(ex.Message);
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout([FromServices] abd.Middlewares.TokenBlacklist blacklist)
    {
        var authorization = Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer "))
        {
            var token = authorization.Replace("Bearer ", "");
            blacklist.Add(token);
        }
        return Ok("Sesión cerrada");
    }
}