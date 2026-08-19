using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Tenebit.Infrastructure.Services;

public sealed class FieldEncryptionKeyRing
{
    public const string SectionPath = "Auth:FieldEncryption";
    public const string DevelopmentKeyId = "development";
    public const string DevelopmentKey = "tenebit-development-field-encryption-key-change-me";
    private static readonly Regex KeyIdPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant);

    private FieldEncryptionKeyRing(string activeKeyId, IReadOnlyDictionary<string, byte[]> keys, string? legacyV1KeyId, bool allowLegacyPlaintext)
    { ActiveKeyId=activeKeyId; Keys=keys; LegacyV1KeyId=legacyV1KeyId; AllowLegacyPlaintext=allowLegacyPlaintext; }

    public string ActiveKeyId { get; }
    public IReadOnlyDictionary<string, byte[]> Keys { get; }
    public string? LegacyV1KeyId { get; }
    public bool AllowLegacyPlaintext { get; }
    public byte[] GetActiveKey()=>Keys[ActiveKeyId];
    public bool TryGetKey(string id,out byte[] key)=>Keys.TryGetValue(id,out key!);

    public static FieldEncryptionKeyRing Load(IConfiguration configuration)
    {
        var section=configuration.GetSection(SectionPath);
        var raw=section.GetSection("Keys").GetChildren().Where(x=>!string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x=>x.Key,x=>x.Value!,StringComparer.Ordinal);
        var active=section["ActiveKeyId"]?.Trim();
        var legacy=section["LegacyV1KeyId"]?.Trim();
        var scalar=configuration["Auth:FieldEncryptionKey"];
        if(raw.Count==0 && !string.IsNullOrWhiteSpace(scalar)){raw["legacy"]=scalar; active??="legacy"; legacy??="legacy";}
        if(raw.Count==0){raw[DevelopmentKeyId]=DevelopmentKey; active??=DevelopmentKeyId; legacy??=DevelopmentKeyId;}
        active ??= raw.Count==1 ? raw.Keys.Single() : throw new InvalidOperationException($"{SectionPath}:ActiveKeyId is required.");
        ValidateId(active);
        if(!raw.ContainsKey(active)) throw new InvalidOperationException($"Active field-encryption key '{active}' is missing.");
        if(!string.IsNullOrWhiteSpace(legacy)){ValidateId(legacy); if(!raw.ContainsKey(legacy)) throw new InvalidOperationException($"Legacy v1 key '{legacy}' is missing.");}
        var keys=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var pair in raw){ValidateId(pair.Key); if(pair.Value.Length<32) throw new InvalidOperationException($"Field encryption key '{pair.Key}' must be at least 32 characters."); keys[pair.Key]=Encoding.UTF8.GetBytes(pair.Value);}
        return new FieldEncryptionKeyRing(active,keys,legacy,section.GetValue("AllowLegacyPlaintext",true));
    }

    public static IReadOnlyList<string> ValidateProduction(IConfiguration configuration,string signingKey) =>
        ValidateProduction(configuration, [signingKey]);

    public static IReadOnlyList<string> ValidateProduction(IConfiguration configuration,IReadOnlyCollection<string> signingKeys)
    {
        var errors=new List<string>(); FieldEncryptionKeyRing ring;
        try{ring=Load(configuration);}catch(InvalidOperationException ex){errors.Add(ex.Message);return errors;}
        if(ring.ActiveKeyId==DevelopmentKeyId || ring.Keys.Values.Any(x=>Encoding.UTF8.GetString(x)==DevelopmentKey)) errors.Add("Field encryption uses the repository development key.");
        if(signingKeys.Any(signingKey=>!string.IsNullOrWhiteSpace(signingKey)&&ring.Keys.Values.Any(x=>Encoding.UTF8.GetString(x)==signingKey))) errors.Add("Field encryption key must differ from every JWT signing key.");
        if(string.IsNullOrWhiteSpace(ring.LegacyV1KeyId)) errors.Add("Auth:FieldEncryption:LegacyV1KeyId is required until all v1 ciphertext has been re-encrypted.");
        if(ring.AllowLegacyPlaintext) errors.Add("Auth:FieldEncryption:AllowLegacyPlaintext must be false in Production.");
        return errors;
    }

    private static void ValidateId(string id){if(!KeyIdPattern.IsMatch(id)) throw new InvalidOperationException($"Invalid field-encryption key id '{id}'.");}
}
