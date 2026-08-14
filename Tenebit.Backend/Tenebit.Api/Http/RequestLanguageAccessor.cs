namespace Tenebit.Api.Http;

/// <summary>
/// Small static "ambient" holder for the app's <see cref="IHttpContextAccessor"/>, set once at
/// startup in Program.cs. Lets static, non-DI code (such as <see cref="ResultExtensions"/>) read the
/// current request's <c>X-Ui-Language</c> header — mirroring <c>CurrentUser.Language</c> — without
/// requiring every <c>.ToHttpResult()</c> call site across the endpoints to thread the language
/// through explicitly.
/// </summary>
public static class RequestLanguageAccessor
{
    private static IHttpContextAccessor? _httpContextAccessor;

    public static void Configure(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public static string CurrentLanguage
    {
        get
        {
            var header = _httpContextAccessor?.HttpContext?.Request.Headers["X-Ui-Language"].ToString();
            return string.IsNullOrWhiteSpace(header) ? "pl" : header.Trim().ToLowerInvariant();
        }
    }
}
