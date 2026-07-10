using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Tenebit.Api.Auth;
using Tenebit.Api.Auth.OAuth;
using Tenebit.Api.Endpoints;
using Tenebit.Api.Http;
using Tenebit.Application;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/tenebit-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

// Mikrus/docker reverse proxy compatibility: old compose configs may point to 5000,
// newer .NET container defaults use 8080. Bind both so the backend stays reachable.
builder.WebHost.UseUrls("http://0.0.0.0:8080", "http://0.0.0.0:5000");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddScoped<TokenIssuer>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection(OAuthOptions.SectionName));
builder.Services.AddSingleton<OAuthStateStore>();
builder.Services.AddSingleton<TwoFactorChallengeStore>();
builder.Services.AddScoped<ExternalAuthService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtSigningKey.Get(builder.Configuration),
            NameClaimType = "name",
            RoleClaimType = "roles"
        };
    });
builder.Services.AddAuthorization();

if (builder.Environment.IsProduction())
{
    var signingKey = builder.Configuration["Auth:SigningKey"];
    if (string.IsNullOrWhiteSpace(signingKey) || signingKey == "tenebit-development-signing-key-change-me-32chars")
    {
        throw new InvalidOperationException("Auth:SigningKey musi być ustawiony na unikalny sekret w środowisku produkcyjnym (zmienna środowiskowa Auth__SigningKey).");
    }

    var connectionString = builder.Configuration.GetConnectionString("TenebitDb") ?? string.Empty;
    if (connectionString.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("ConnectionStrings:TenebitDb używa domyślnego hasła z repozytorium. Ustaw silne hasło w środowisku produkcyjnym (zmienna środowiskowa ConnectionStrings__TenebitDb).");
    }
}

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ErrorResponse("Wystąpił nieoczekiwany błąd aplikacji.", "INTERNAL_ERROR"));
    });
});

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Tenebit API");
    });
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapTenebitApi();

await app.StartAsync();
try
{
    await app.Services.InitializeDatabaseAsync();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Nie udało się zainicjalizować bazy danych Tenebit. Aplikacja pozostaje uruchomiona, żeby /api/health i logi były dostępne.");
}

await app.WaitForShutdownAsync();
