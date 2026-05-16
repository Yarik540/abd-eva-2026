using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;

namespace abd.Middlewares
{
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();

            // Rutas públicas que no requieren token
            if (path != null && (
                path.Contains("/api/auth/login") ||
                path.Contains("/api/auth/logout") ||
                path.Contains("/swagger")))
            {
                await _next(context);
                return;
            }

            var authorization = context.Request.Headers["Authorization"].ToString();

            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Token requerido" });
                return;
            }

            try
            {
                var token = authorization.Replace("Bearer ", "");

                // Verificar blacklist
                var blacklist = context.RequestServices.GetRequiredService<TokenBlacklist>();
                if (blacklist.Contains(token))
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { error = "Sesión cerrada" });
                    return;
                }

                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                // Verificar expiración
                if (jwt.ValidTo < DateTime.UtcNow)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { error = "Token expirado" });
                    return;
                }

                // Extraer rol desde user_metadata
                var rol = "cliente";
                var userMetadataRaw = jwt.Claims.FirstOrDefault(c => c.Type == "user_metadata")?.Value;
                if (!string.IsNullOrEmpty(userMetadataRaw))
                {
                    var metadata = System.Text.Json.JsonDocument.Parse(userMetadataRaw);
                    if (metadata.RootElement.TryGetProperty("rol", out var rolElement))
                        rol = rolElement.GetString() ?? "cliente";
                }

                // Guardar datos en HttpContext.Items
                context.Items["userId"] = jwt.Subject; // el ID del usuario (sub)
                context.Items["userEmail"] = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                context.Items["userRol"] = rol;

                await _next(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR MIDDLEWARE: {ex.Message}");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Token inválido o expirado" });
            }
        }
    }
}
