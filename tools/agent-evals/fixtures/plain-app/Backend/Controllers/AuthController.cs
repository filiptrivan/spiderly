using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Controllers;

// Thin baseline auth: a single test account (from the "Jwt" config section) exchanges credentials
// for a JWT. Enough for the eval to authenticate against [Authorize] endpoints the agent adds — it
// is NOT a real user store. The no-Spiderly baseline ships working auth so the agent doesn't
// rebuild it (see fixtures/plain-app README).
[ApiController]
[Route("api/[controller]")]
public class AuthController(IConfiguration config) : ControllerBase
{
    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string Token);

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var jwt = config.GetSection("Jwt");
        if (request.Username != jwt["TestUser"] || request.Password != jwt["TestPassword"])
            return Unauthorized();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: [new Claim(ClaimTypes.Name, request.Username)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token));
    }
}
