namespace Tenebit.Domain.Identity;

public sealed class OAuthTransaction
{
    private OAuthTransaction() { }

    public OAuthTransaction(string stateHash, string provider, string codeVerifier, string returnPath, string correlationHash, string nonce, DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        StateHash = stateHash;
        Provider = provider;
        CodeVerifier = codeVerifier;
        ReturnPath = returnPath;
        CorrelationHash = correlationHash;
        Nonce = nonce;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string StateHash { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string CodeVerifier { get; private set; } = string.Empty;
    public string ReturnPath { get; private set; } = string.Empty;
    public string CorrelationHash { get; private set; } = string.Empty;
    public string Nonce { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
}
