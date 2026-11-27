using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Marketplace.API.Domain;
using Marketplace.API.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// CORS: only your own app's address may call the API from a browser
var allowedOrigins = builder.Configuration
    .GetSection("Cors:Origins").Get<string[]>()
    ?? new[] { "http://localhost:5000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowApp", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Database (path can be overridden on a server)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=app.db"));

// JWT: the secret key must come from user-secrets or an environment variable
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Create the database and tables automatically if they are missing
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // First Admin: created from secret settings, only if no Admin exists yet
    var adminEmail = builder.Configuration["Admin:Email"];
    var adminPassword = builder.Configuration["Admin:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        if (db.Users.Any(u => u.Role == "Admin"))
        {
            app.Logger.LogInformation("Admin seed skipped: an Admin already exists.");
        }
        else if (adminPassword.Length < 10)
        {
            app.Logger.LogWarning("Admin seed skipped: Admin:Password must be at least 10 characters.");
        }
        else if (db.Users.Any(u => u.Email == adminEmail))
        {
            app.Logger.LogWarning("Admin seed skipped: {Email} is already registered as a non-Admin.", adminEmail);
        }
        else
        {
            db.Users.Add(new User
            {
                Name = "Admin",
                Email = adminEmail,
                Password = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = "Admin"
            });
            db.SaveChanges();
            app.Logger.LogInformation("Admin account created for {Email}.", adminEmail);
        }
    }
}

app.UseCors("AllowApp");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();