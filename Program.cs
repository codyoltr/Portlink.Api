using System.Text;
using System.Text.Json;
using AspNetCoreRateLimit;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Portlink.Api.Data;
using Portlink.Api.Helpers;
using Portlink.Api.Modules.Agent;
using Portlink.Api.Modules.Auth;
using Portlink.Api.Modules.Chatbot.Interfaces;
using Portlink.Api.Modules.Chatbot.Services;
using Portlink.Api.Modules.Chatbot.Settings;
using Portlink.Api.Modules.Messaging;
using Portlink.Api.Modules.Messaging.Interfaces;
using Portlink.Api.Modules.Subcontractor;
using Portlink.Api.Modules.Subcontractor.Interfaces;
using Portlink.Api.Modules.Storage.Interfaces;
using Portlink.Api.Modules.Storage.Services;
using Portlink.Api.Modules.Storage.Settings;
using Serilog;
using Portlink.Api.Database.Seeds;

// ─── Serilog Bootstrap ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/portlink-.log", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

// Load .env before configuration is built so env vars are available.
Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ─── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Authentication / JWT ─────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key eksik!");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ─── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://localhost:3000" };
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ─── Rate Limiting ────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// ─── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<ISubcontractorService, SubcontractorService>();
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
var storageSettings = StorageSettings.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(storageSettings);
builder.Services.AddScoped<IS3StorageProvider, S3StorageProvider>();
builder.Services.AddScoped<IFileValidationService, FileValidationService>();
builder.Services.AddScoped<IStorageService, StorageService>();
var chatbotSettings = ChatbotSettings.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(chatbotSettings);
builder.Services.AddHttpClient<ILlmProviderService, GeminiLlmProviderService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(25);
});
builder.Services.AddScoped<IChatbotService, ChatbotService>();

// ─── Controllers + JSON ───────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opt.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ─── Swagger / OpenAPI ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Portlink API",
        Version = "v1",
        Description = "Portlink B2B Denizcilik Platformu — Backend API"
    });

    // JWT Bearer tanımı
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "JWT token girin: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─── Static Files (uploads) ───────────────────────────────────────────────────
builder.Services.AddDirectoryBrowser();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

// Global Exception Middleware
app.UseMiddleware<Portlink.Api.Middlewares.ExceptionMiddleware>();

// ─── Development Middleware ───────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Portlink API v1");
        c.RoutePrefix = "swagger";
    });
}

// ─── Pipeline ────────────────────────────────────────────────────────────────
app.UseCors("Frontend");
app.UseIpRateLimiting();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "uploads")),
    RequestPath = "/uploads"
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ─── DB Migration (otomatik) ─────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await AgencyAdminUserSeeder.SeedAsync(db);
    await SubcontractorAdminUserSeeder.SeedAsync(db);
    Log.Information("Varsayilan admin kullanici seed islemi tamamlandi.");
    Log.Information("Veritabanı migration tamamlandı.");
}

Log.Information("Portlink API başlatıldı.");
await app.RunAsync();
