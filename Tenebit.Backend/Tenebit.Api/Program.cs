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
using Tenebit.Application.Common;
using Tenebit.Infrastructure;
using Tenebit.Infrastructure.Services;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.ValidateProductionSecurityConfiguration(builder.Environment);

const string LogOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {CorrelationId}{NewLine}{Exception}";

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: LogOutputTemplate);

    // Production logs only to stdout/stderr so the platform can enforce centralized retention and
    // the source/runtime filesystem never accumulates PII-bearing diagnostic files.
    if (!context.HostingEnvironment.IsProduction())
    {
        loggerConfig.WriteTo.File("logs/tenebit-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, outputTemplate: LogOutputTemplate);
    }
});

builder.WebHost.UseUrls(builder.Environment.IsDevelopment()
    ? ["http://0.0.0.0:8080", "http://0.0.0.0:5000"]
    : ["http://0.0.0.0:8080"]);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = RequestSizeLimits.MaxMultipartBodyBytes;
});

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
builder.Services.AddHttpClient();
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection(OAuthOptions.SectionName));
builder.Services.AddScoped<OAuthStateStore>();
builder.Services.AddScoped<TwoFactorChallengeStore>();
builder.Services.AddScoped<ExternalAuthService>();

// Defense-in-depth ceiling na wielkość multipart body (audyt P0.4) — Kestrel ma własny domyślny limit
// (30 MB), ale w hostingu bez tego domyślnego limitu (np. IIS in-process) formularz byłby buforowany
// bez ograniczeń. Per-endpoint limity są ustawiane dodatkowo w handlerach uploadów.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = RequestSizeLimits.MaxMultipartBodyBytes;
    // ASP.NET Core spools file sections larger than this threshold to a temp file instead of retaining
    // the entire multipart body in RAM. Application code then creates only the one byte[] it actually needs.
    options.MemoryBufferThreshold = 64 * 1024;
    options.ValueLengthLimit = RequestLimits.Json;
    options.KeyLengthLimit = 256;
    options.ValueCountLimit = 1024;
    options.MultipartBoundaryLengthLimit = 128;
    options.MultipartHeadersCountLimit = 16;
    options.MultipartHeadersLengthLimit = 8 * 1024;
});

// Tylko jeden zaufany skok proxy (kontener nginx w tym samym compose) łączy się bezpośrednio z Kestrel —
// backend nie jest publicznie eksponowany. KnownNetworks/KnownProxies celowo wyczyszczone (audyt P1.3):
// bez tego ASP.NET domyślnie ufa wyłącznie loopbackowi i middleware po cichu ignorowałby X-Forwarded-For,
// przez co RemoteIpAddress dalej pokazywałby adres kontenera nginx zamiast realnego klienta.
builder.Services.AddTrustedForwardedHeaders(builder.Configuration, builder.Environment);

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
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var subjectClaim = context.Principal?.FindFirst("sub")?.Value;
                var organizationClaim = context.Principal?.FindFirst("organization_id")?.Value;
                var stampClaim = context.Principal?.FindFirst("security_stamp")?.Value;
                if (!Guid.TryParse(subjectClaim, out var userId) ||
                    !Guid.TryParse(organizationClaim, out var organizationId) ||
                    !Guid.TryParse(stampClaim, out var tokenStamp))
                {
                    context.Fail("Token nie zawiera aktualnego stanu sesji.");
                    return;
                }

                // Refresh/device tokens are revoked on security-sensitive changes, but an already issued
                // access JWT would otherwise keep its old roles until expiry. Comparing a per-user stamp
                // makes role, activity, password and 2FA changes effective on the very next request.
                var users = context.HttpContext.RequestServices.GetRequiredService<IOrganizationUserRepository>();
                var user = await users.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive || user.OrganizationId != organizationId || user.SecurityStamp != tokenStamp)
                {
                    context.Fail("Sesja została unieważniona.");
                }
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

Tenebit.Api.Http.RequestLanguageAccessor.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

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

app.UseMiddleware<RequestBodyLimitMiddleware>();

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

var verifyEncryptedDataOnly = args.Any(arg => string.Equals(arg, "--verify-encrypted-data", StringComparison.OrdinalIgnoreCase));
if (verifyEncryptedDataOnly)
{
    await app.Services.VerifyEncryptedDataAsync();
    app.Logger.LogInformation("Encrypted-data verification completed successfully without exposing plaintext.");
    return;
}

var migrateOnly = args.Any(arg => string.Equals(arg, "--migrate-only", StringComparison.OrdinalIgnoreCase));
if (migrateOnly)
{
    try
    {
        await app.Services.InitializeDatabaseAsync(forceMigrate: true);
        app.Logger.LogInformation("Migracje bazy Tenebit zakończone poprawnie.");
        return;
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Migrator Tenebit zakończył się błędem.");
        throw;
    }
}

// Development/test can auto-migrate for ergonomics. Production API is deliberately schema-read-only:
// the deployment pipeline runs the same image once with --migrate-only before starting replicas.
if (!app.Environment.IsProduction())
{
    await app.Services.InitializeDatabaseAsync();
}

await app.RunAsync();

// Odsłania wygenerowaną klasę Program dla WebApplicationFactory<Program> w testach integracyjnych.
public partial class Program;
