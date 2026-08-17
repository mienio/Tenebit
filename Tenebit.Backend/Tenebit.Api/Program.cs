using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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

const string LogOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {CorrelationId}{NewLine}{Exception}";

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: LogOutputTemplate)
    .WriteTo.File("logs/tenebit-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, outputTemplate: LogOutputTemplate));

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
builder.Services.AddInfrastructure(builder.Configuration, enableSingleInstanceGuard: builder.Environment.IsProduction());
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

// Defense-in-depth ceiling na wielkość multipart body (audyt P0.4) — Kestrel ma własny domyślny limit
// (30 MB), ale w hostingu bez tego domyślnego limitu (np. IIS in-process) formularz byłby buforowany
// bez ograniczeń. Per-endpoint limity są ustawiane dodatkowo w handlerach uploadów.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 30 * 1024 * 1024;
});

// Tylko jeden zaufany skok proxy (kontener nginx w tym samym compose) łączy się bezpośrednio z Kestrel —
// backend nie jest publicznie eksponowany. KnownNetworks/KnownProxies celowo wyczyszczone (audyt P1.3):
// bez tego ASP.NET domyślnie ufa wyłącznie loopbackowi i middleware po cichu ignorowałby X-Forwarded-For,
// przez co RemoteIpAddress dalej pokazywałby adres kontenera nginx zamiast realnego klienta.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Partycjonowanie per-IP (audyt P1.8) — poprzednio "auth"/"public" był jednym globalnym licznikiem
// współdzielonym przez wszystkich klientów, więc jeden agresywny użytkownik/atakujący wyczerpywał limit
// logowania dla całej instancji. RemoteIpAddress jest tu wiarygodny dzięki UseForwardedHeaders powyżej.
static string PartitionKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Configurable so E2E test runs (many page loads => many /auth/refresh calls in a short
    // window) don't need to weaken the production default of 10/min to make the suite runnable.
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = authPermitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));
    options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 60,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = JwtIssuerOptions.GetIssuer(builder.Configuration),
            ValidateAudience = true,
            ValidAudience = JwtIssuerOptions.GetAudience(builder.Configuration),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtSigningKey.Get(builder.Configuration),
            NameClaimType = "name",
            RoleClaimType = "roles"
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

Tenebit.Api.Http.RequestLanguageAccessor.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

if (app.Environment.IsProduction())
{
    var signingKey = builder.Configuration["Auth:SigningKey"];
    if (string.IsNullOrWhiteSpace(signingKey) || signingKey == "tenebit-development-signing-key-change-me-32chars" || signingKey.Length < 32)
    {
        app.Logger.LogCritical("BEZPIECZEŃSTWO: Auth:SigningKey jest puste, ma domyślną wartość z repozytorium albo jest za krótkie. Ustaw unikalny sekret (min. 32 znaki) przez zmienną środowiskową Auth__SigningKey.");
        throw new InvalidOperationException("Auth:SigningKey nie jest bezpiecznie skonfigurowany. Aplikacja nie może wystartować w Production.");
    }

    var connectionString = builder.Configuration.GetConnectionString("TenebitDb") ?? string.Empty;
    if (connectionString.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
    {
        app.Logger.LogCritical("BEZPIECZEŃSTWO: ConnectionStrings:TenebitDb nadal używa domyślnego hasła z repozytorium. Ustaw silne hasło przez zmienną środowiskową ConnectionStrings__TenebitDb.");
        throw new InvalidOperationException("ConnectionStrings:TenebitDb używa domyślnego hasła. Aplikacja nie może wystartować w Production.");
    }
}

app.UseForwardedHeaders();
app.UseCorrelationId();
app.UseSerilogRequestLogging();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var id) ? id?.ToString() : null;
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        context.Response.ContentType = "application/json";

        // Współbieżna edycja tego samego rekordu jest przewidywalnym przypadkiem biznesowym, nie awarią —
        // klient dostaje 409 z jasnym kodem zamiast nieodróżnialnego od realnej awarii 500 (audyt AUD-028).
        if (feature?.Error is Tenebit.Domain.Common.ConcurrencyException concurrencyEx)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { message = concurrencyEx.Message, code = "CONCURRENCY_CONFLICT", correlationId });
            return;
        }

        // Naruszenie unikalnego indeksu (np. wyścig dwóch requestów tworzących ten sam rekord) jest
        // przewidywalnym konfliktem biznesowym, nie awarią — wcześniej trafiało w gałąź 500 poniżej,
        // nie ujawniając klientowi że to konflikt do powtórzenia z innymi danymi (audyt P1.10).
        if (feature?.Error is Microsoft.EntityFrameworkCore.DbUpdateException dbUpdateEx &&
            dbUpdateEx.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { message = "Rekord z takimi danymi już istnieje.", code = "DUPLICATE", correlationId });
            return;
        }

        // Body/multipart przekraczające limit ustawiony per-endpoint (audyt P0.4) — Kestrel przerywa odczyt
        // strumienia zanim cały upload trafi do pamięci, zamiast zwracać nieodróżnialny od awarii 500.
        if (feature?.Error is Microsoft.AspNetCore.Http.BadHttpRequestException badRequestEx)
        {
            context.Response.StatusCode = badRequestEx.StatusCode;
            var code = badRequestEx.StatusCode == StatusCodes.Status413PayloadTooLarge ? "PAYLOAD_TOO_LARGE" : "BAD_REQUEST";
            var message = badRequestEx.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Przesłany plik jest za duży." : "Nieprawidłowe żądanie.";
            await context.Response.WriteAsJsonAsync(new { message, code, correlationId });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { message = "Wystąpił nieoczekiwany błąd aplikacji.", code = "INTERNAL_ERROR", correlationId });
    });
});

// OpenAPI schema ujawnia pełną mapę endpointów i kontraktów DTO — w Production to rekonesans za darmo
// dla atakującego (audyt P1.12), więc trasa zostaje wyłącznie w środowiskach nie-produkcyjnych.
if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}
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

// Migracja musi się zakończyć zanim aplikacja zacznie przyjmować ruch/uruchamiać background jobs —
// inaczej requesty i joby trafiają na schemat, którego jeszcze nie ma (patrz audyt AUD-014).
try
{
    await app.Services.InitializeDatabaseAsync();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Nie udało się zainicjalizować bazy danych Tenebit. Aplikacja nie zostanie uruchomiona.");
    throw;
}

await app.RunAsync();

// Odsłania wygenerowaną klasę Program dla WebApplicationFactory<Program> w testach integracyjnych.
public partial class Program;
