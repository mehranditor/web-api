using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly ILogger<AdminController> _logger;
        private readonly IConfiguration _configuration;

        public AdminController(ILogger<AdminController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost("auth")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminAuthResponse))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Auth([FromBody] AdminAuthRequest request)
        {
            var secretKey = _configuration["AdminAuth:SecretKey"];
            if (string.IsNullOrEmpty(request.Key) || request.Key != secretKey)
            {
                _logger.LogWarning("Invalid admin key provided");
                return Unauthorized(new { error = "Invalid admin key" });
            }

            var token = GenerateJwtToken();
            _logger.LogInformation("Admin JWT token generated successfully");
            return Ok(new AdminAuthResponse { Token = token });
        }

        private string GenerateJwtToken()
        {
            var jwtKey = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(JwtRegisteredClaimNames.Sub, "admin"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class AdminAuthRequest
    {
        public string Key { get; set; } = string.Empty;
    }

    public class AdminAuthResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}