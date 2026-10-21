using Marketplace.API.Domain;
using Marketplace.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Marketplace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    //[Authorize]

    // REGISTER
    [HttpPost("register")]
    public IActionResult Register(User user)
    {
        // Hash the password before saving 👇
        user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

        _context.Users.Add(user);
        _context.SaveChanges();
        return Ok(user);
    }

    // LOGIN
    [HttpPost("login")]
    public IActionResult Login(LoginRequest request)
    {
        var existingUser = _context.Users
            .FirstOrDefault(u => u.Email == request.Email);

        if (existingUser == null)
            return Unauthorized("Invalid email or password");

        // Verify password against hash 👇
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            existingUser.Password
        );

        if (!isPasswordValid)
            return Unauthorized("Invalid email or password");

        var token = GenerateToken(existingUser);
        return Ok(new { token });
    }

    //// LOGIN
    //[HttpPost("login")]
    //public IActionResult Login(LoginRequest request)
    //{
    //    var existingUser = _context.Users
    //        .FirstOrDefault(u => u.Email == request.Email && u.Password == request.Password);

    //    if (existingUser == null)
    //        return Unauthorized("Invalid email or password");

    //    var token = GenerateToken(existingUser);

    //    return Ok(new { token });
    //}

    // TOKEN GENERATOR
    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}