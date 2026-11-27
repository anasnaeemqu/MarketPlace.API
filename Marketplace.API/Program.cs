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

// Database: "Sqlite" on your laptop (default), "Postgres" online.
// Chosen by the setting Database:Provider (environment variable Database__Provider).
var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var usePostgres = string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase);

if (!usePostgres && !string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException(
        $"Database:Provider must be 'Sqlite' or 'Postgres', but it is '{dbProvider}'.");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Render gives a URL like postgresql://user:pass@host:5432/dbname.
// Npgsql wants "Host=...;Database=...;..." so we convert it when needed.
static string ToNpgsqlConnectionString(string value)
{
    if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return value; // already in the keyword style

    var uri = new Uri(value);
    var userInfo = uri.UserInfo.Split(':', 2);

    var csb = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        SslMode = Npgsql.SslMode.Prefer
    };
    return csb.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgres)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required when Database:Provider is Postgres.");

        options.UseNpgsql(ToNpgsqlConnectionString(connectionString));
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=app.db");
    }
});

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

app.Logger.LogInformation("Database provider: {Provider}", usePostgres ? "Postgres" : "Sqlite");

// Create the database and tables automatically if they are missing
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (usePostgres)
    {
        // The SQLite migration files do not work on Postgres,
        // so online the tables are built straight from the models.
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }

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