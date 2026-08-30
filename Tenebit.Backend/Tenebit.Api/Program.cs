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
using Tenebit.Application.Identity;
using Tenebit.Infrastructure;
using Tenebit.Infrastructure.Services;

// One-time local setup helpers for the platform-admin account (see Admin__* env vars in deploy docs) -
// pure static calls, no DB/host needed, so they run and exit before the web app builds.
if (args.Length >= 2 && string.Equals(args[0], "--admin-hash-password", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(PasswordHasher.Hash(args[1]));
    return;
}
if (args.Length >= 1 && string.Equals(args[0], "--admin-generate-totp", StringComparison.OrdinalIgnoreCase))
{
    var secret = TotpService.GenerateSecret();
    var email = args.Length >= 2 ? args[1] : "admin@tenebit";
    Console.WriteLine($"Secret: {secret}");
    Console.WriteLine($"otpauth URI (manual entry / QR): {TotpService.BuildOtpAuthUri(secret, email)}");
    return;
}

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
builder.Services.AddSingleton<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
// Singleton: the lockout counter must be shared across all admin sign-in attempts, not per request.
builder.Services.AddSingleton<AdminLoginGuard>();
builder.Services.AddScoped<AdminAlertSender>();
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

// Defense-in-depth ceiling na wielkość multipart body (audyt P0.4) - Kestrel ma własny domyślny limit
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

// Tylko jeden zaufany skok proxy (kontener nginx w tym samym compose) łączy się bezpośrednio z Kestrel -
// backend nie jest publicznie eksponowany. KnownNetworks/KnownProxies celowo wyczyszczone (audyt P1.3):
// bez tego ASP.NET domyślnie ufa wyłącznie loopbackowi i middleware po cichu ignorowałby X-Forwarded-For,
// przez co RemoteIpAddress dalej pokazywałby adres kontenera nginx zamiast realnego klienta.
builder.Services.AddTrustedForwardedHeaders(builder.Configuration, builder.Environment);

// Partycjonowanie per-IP (audyt P1.8) - poprzednio "auth"/"public" był jednym globalnym licznikiem
// współdzielonym przez wszystkich klientów, więc jeden agresywny użytkownik/atakujący wyczerpywał limit
// logowania dla całej instancji. RemoteIpAddress jest tu wiarygodny dzięki UseForwardedHeaders powyżej.
static string PartitionKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static FixedWindowRateLimiterOptions Window(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    };

    // These are NAT-friendly safety ceilings. Credential-specific brute-force budgets are enforced
    // separately in PostgreSQL, shared by every replica (IAuthenticationAbuseLimiter).
    options.AddPolicy("auth-login", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(200)));
    options.AddPolicy("auth-register", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(60)));
    options.AddPolicy("auth-refresh", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(1000)));
    options.AddPolicy("auth-recovery", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(120)));
    options.AddPolicy("auth-oauth", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(180)));
    options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(60)));
    // Single account, highest-value target: far tighter than the tenant "auth-login" ceiling (200/min).
    options.AddPolicy("admin-login", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(10)));
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
            IssuerSigningKeyResolver = (_, _, keyId, _) => JwtSigningKey.GetValidationKeys(builder.Configuration, keyId),
            NameClaimType = "name",
            RoleClaimType = "roles"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                // Platform-admin tokens have no backing OrganizationUser row to look up - validated
                // entirely by signature/issuer/audience/expiry (already checked) plus this explicit scope
                // claim. TenebitEndpoints separately forbids this scope on every tenant route.
                if (context.Principal?.FindFirst(PlatformAdminClaims.ScopeClaimType)?.Value == PlatformAdminClaims.ScopeValue)
                {
                    return;
                }

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
                // access JWT would otherwise keep its old roles until expiry. The short-lived cache removes
                // the per-request JOIN; application-managed security changes invalidate the cached entry immediately.
                var cache = context.HttpContext.RequestServices.GetRequiredService<IUserSecurityStateCache>();
                if (!cache.TryGet(userId, out var securityState))
                {
                    var users = context.HttpContext.RequestServices.GetRequiredService<IOrganizationUserRepository>();
                    var loadedState = await users.GetSecurityStateAsync(userId, context.HttpContext.RequestAborted);
                    if (loadedState is null)
                    {
                        context.Fail("Sesja została unieważniona.");
                        return;
                    }

                    securityState = loadedState;
                    cache.Set(userId, securityState, TimeSpan.FromSeconds(30));
                }

                if (!securityState.IsActive || !securityState.IsEmailVerified || securityState.OrganizationId != organizationId || securityState.SecurityStamp != tokenStamp)
                {
                    context.Fail("Sesja została unieważniona.");
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdmin", policy => policy.RequireClaim(PlatformAdminClaims.ScopeClaimType, PlatformAdminClaims.ScopeValue));
});

var app = builder.Build();

Tenebit.Api.Http.RequestLanguageAccessor.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

app.UseForwardedHeaders();
app.UseCorrelationId();
// Public capability endpoints intentionally do not create a request-log event. Besides the new cookie
// architecture, this prevents legacy /api/public/.../<secret> requests from ever persisting the bearer
// credential as RequestPath in structured sinks during the migration window.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api/public"),
    branch => branch.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} endpoint {EndpointName} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnostic, context) =>
            diagnostic.Set("EndpointName", context.GetEndpoint()?.DisplayName ?? "unmatched");
    }));

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var id) ? id?.ToString() : null;
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        context.Response.ContentType = "application/json";

        // Współbieżna edycja tego samego rekordu jest przewidywalnym przypadkiem biznesowym, nie awarią -
        // klient dostaje 409 z jasnym kodem zamiast nieodróżnialnego od realnej awarii 500 (audyt AUD-028).
        if (feature?.Error is Tenebit.Domain.Common.ConcurrencyException concurrencyEx)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { message = concurrencyEx.Message, code = "CONCURRENCY_CONFLICT", correlationId });
            return;
        }

        // Naruszenie unikalnego indeksu (np. wyścig dwóch requestów tworzących ten sam rekord) jest
        // przewidywalnym konfliktem biznesowym, nie awarią - wcześniej trafiało w gałąź 500 poniżej,
        // nie ujawniając klientowi że to konflikt do powtórzenia z innymi danymi (audyt P1.10).
        if (feature?.Error is Microsoft.EntityFrameworkCore.DbUpdateException dbUpdateEx &&
            dbUpdateEx.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            var message = Tenebit.Application.Common.ErrorMessageTranslator.Translate("Rekord z takimi danymi już istnieje.", Tenebit.Api.Http.RequestLanguageAccessor.CurrentLanguage);
            await context.Response.WriteAsJsonAsync(new { message, code = "DUPLICATE", correlationId });
            return;
        }

        // Naruszenie klucza obcego (np. usuwanie rekordu, który jest wciąż gdzieś przypisany) jest
        // przewidywalnym konfliktem biznesowym, nie awarią - serwisy powinny to sprawdzać wcześniej
        // (IsUsedAsync), ale to jest siatka bezpieczeństwa dla przypadków, których jeszcze nie pokryto.
        if (feature?.Error is Microsoft.EntityFrameworkCore.DbUpdateException fkUpdateEx &&
            fkUpdateEx.InnerException is Npgsql.PostgresException { SqlState: "23503" })
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            var message = Tenebit.Application.Common.ErrorMessageTranslator.Translate("Tego rekordu nie można usunąć, ponieważ jest używany w innym miejscu.", Tenebit.Api.Http.RequestLanguageAccessor.CurrentLanguage);
            await context.Response.WriteAsJsonAsync(new { message, code = "IN_USE", correlationId });
            return;
        }

        // Body/multipart przekraczające limit ustawiony per-endpoint (audyt P0.4) - Kestrel przerywa odczyt
        // strumienia zanim cały upload trafi do pamięci, zamiast zwracać nieodróżnialny od awarii 500.
        if (feature?.Error is Microsoft.AspNetCore.Http.BadHttpRequestException badRequestEx)
        {
            context.Response.StatusCode = badRequestEx.StatusCode;
            var code = badRequestEx.StatusCode == StatusCodes.Status413PayloadTooLarge ? "PAYLOAD_TOO_LARGE" : "BAD_REQUEST";
            var rawMessage = badRequestEx.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Przesłany plik jest za duży." : "Nieprawidłowe żądanie.";
            var message = Tenebit.Application.Common.ErrorMessageTranslator.Translate(rawMessage, Tenebit.Api.Http.RequestLanguageAccessor.CurrentLanguage);
            await context.Response.WriteAsJsonAsync(new { message, code, correlationId });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var fallbackMessage = Tenebit.Application.Common.ErrorMessageTranslator.Translate("Wystąpił nieoczekiwany błąd aplikacji.", Tenebit.Api.Http.RequestLanguageAccessor.CurrentLanguage);
        await context.Response.WriteAsJsonAsync(new { message = fallbackMessage, code = "INTERNAL_ERROR", correlationId });
    });
});

app.UseMiddleware<RequestBodyLimitMiddleware>();

// OpenAPI schema ujawnia pełną mapę endpointów i kontraktów DTO - w Production to rekonesans za darmo
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

// Każda odpowiedź /api niesie dane jednej organizacji - protokoły, zdjęcia dowodowe, listy pracowników.
// Bez jawnego no-store pośrednik albo współdzielona przeglądarka mogą przetrzymać taką odpowiedź i oddać
// ją komuś innemu. Nagłówek ustawiamy w OnStarting, żeby nie nadpisać go po wysłaniu nagłówków i żeby
// obejmował także pliki zwracane przez Results.File (dowody, PDF-y protokołów).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, private";
            context.Response.Headers.Pragma = "no-cache";
            return Task.CompletedTask;
        });
    }

    await next(context);
});

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapTenebitApi();
app.MapAdminEndpoints();

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
