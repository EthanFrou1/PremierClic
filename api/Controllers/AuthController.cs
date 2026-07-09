using Microsoft.AspNetCore.Mvc;
using PremierClic.Api.Data;
using PremierClic.Api.Models;
using PremierClic.Api.Services;

namespace PremierClic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDto dto)
    {
        var user = _db.Users.SingleOrDefault(u => u.Email == dto.Email);
        if (user == null) return Unauthorized();
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash)) return Unauthorized();
        var token = _jwt.GenerateToken(user);
        return Ok(new { token });
    }

    public class LoginDto { public string Email { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; }
}
