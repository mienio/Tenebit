using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Tenebit.Application.Common;

/// <summary>
/// Marks an inbound API contract as intentionally covered by the global request-validation pipeline.
/// A reflection regression test requires every *Request type to carry this marker, so adding a new
/// endpoint DTO without an explicit validation decision fails CI instead of silently bypassing validation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ValidatedRequestAttribute : Attribute { }

/// <summary>Rejects collections larger than the declared application-level budget.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MaxItemsAttribute : ValidationAttribute
{
    public MaxItemsAttribute(int maximum)
    {
        if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        Maximum = maximum;
    }

    public int Maximum { get; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success;

        if (value is ICollection collection)
        {
            return collection.Count <= Maximum
                ? ValidationResult.Success
                : new ValidationResult(ErrorMessage ?? $"Pole {validationContext.MemberName} może zawierać maksymalnie {Maximum} elementów.");
        }

        if (value is IEnumerable enumerable)
        {
            var count = 0;
            foreach (var _ in enumerable)
            {
                count++;
                if (count > Maximum)
                {
                    return new ValidationResult(ErrorMessage ?? $"Pole {validationContext.MemberName} może zawierać maksymalnie {Maximum} elementów.");
                }
            }
            return ValidationResult.Success;
        }

        return new ValidationResult(ErrorMessage ?? $"Pole {validationContext.MemberName} nie jest kolekcją.");
    }
}

/// <summary>Rejects Guid.Empty for identifiers that must point at an existing resource.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success;
        return value is Guid guid && guid != Guid.Empty
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage ?? $"Pole {validationContext.MemberName} musi zawierać prawidłowy identyfikator.");
    }
}

public static class RequestObjectValidator
{
    private const int MaxGraphDepth = 12;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> ShouldValidateCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.PropertyInfo[]> TraversablePropertiesCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Reflection.PropertyInfo, System.Reflection.NullabilityState> NullabilityStates = new();

    public static string? Validate(object? value) => Validate(value, 0);

    private static string? Validate(object? value, int depth)
    {
        if (value is null) return null;
        if (depth > MaxGraphDepth) return "Dane wejściowe są zbyt głęboko zagnieżdżone.";
        if (value is string) return null;

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                var itemError = Validate(item, depth + 1);
                if (itemError is not null) return itemError;
            }
            return null;
        }

        var type = value.GetType();
        if (IsSimple(type)) return null;

        var isMarkedRequest = type.GetCustomAttributes(typeof(ValidatedRequestAttribute), false).Length != 0;
        if (ShouldValidateCache.GetOrAdd(type, static t =>
                t.GetCustomAttributes(typeof(ValidatedRequestAttribute), false).Length != 0 ||
                t.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    .Any(p => p.GetCustomAttributes(typeof(ValidationAttribute), true).Length != 0)))
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true))
            {
                return results.FirstOrDefault()?.ErrorMessage ?? "Nieprawidłowe dane wejściowe.";
            }
        }

        if (isMarkedRequest)
        {
            var conventionError = ValidateRequestConventions(value, type);
            if (conventionError is not null) return conventionError;
        }

        if (!IsApplicationContract(type)) return null;

        foreach (var property in TraversablePropertiesCache.GetOrAdd(type, static t =>
                     t.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                         .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !IsSimple(p.PropertyType))
                         .ToArray()))
        {
            var nestedError = Validate(property.GetValue(value), depth + 1);
            if (nestedError is not null) return nestedError;
        }

        return null;
    }

    private static string? ValidateRequestConventions(object request, Type type)
    {
        foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
            var value = property.GetValue(request);
            var propertyType = property.PropertyType;
            var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (value is null)
            {
                var nullability = NullabilityState(property);
                if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) is null)
                    return $"Pole {property.Name} jest wymagane.";
                if (!propertyType.IsValueType && nullability == System.Reflection.NullabilityState.NotNull)
                    return $"Pole {property.Name} jest wymagane.";
                continue;
            }

            if (value is string text)
            {
                var max = StringLimit(type, property.Name);
                if (text.Length > max) return $"Pole {property.Name} może mieć maksymalnie {max} znaków.";
                if (NullabilityState(property) == System.Reflection.NullabilityState.NotNull && string.IsNullOrWhiteSpace(text))
                    return $"Pole {property.Name} nie może być puste.";
                if (property.Name.EndsWith("Email", StringComparison.OrdinalIgnoreCase) && text.Length > 0 && !new EmailAddressAttribute().IsValid(text))
                    return $"Pole {property.Name} nie zawiera prawidłowego adresu e-mail.";
                if (property.Name is "SuccessUrl" or "CancelUrl" or "ReturnUrl")
                {
                    if (!text.StartsWith("/", StringComparison.Ordinal) || text.StartsWith("//", StringComparison.Ordinal))
                        return $"Pole {property.Name} musi być względną ścieżką aplikacji.";
                }
                continue;
            }

            if (underlying == typeof(Guid) && value is Guid guid && guid == Guid.Empty)
                return $"Pole {property.Name} musi zawierać prawidłowy identyfikator.";

            if (underlying.IsEnum && !Enum.IsDefined(underlying, value))
                return $"Pole {property.Name} ma nieprawidłową wartość.";

            if (value is int intValue)
            {
                if (Math.Abs((long)intValue) > 1_000_000)
                    return $"Pole {property.Name} ma wartość poza dozwolonym zakresem.";
                if ((property.Name.Contains("Seats", StringComparison.OrdinalIgnoreCase) && intValue <= 0) ||
                    ((property.Name.Contains("Retention", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("Cooldown", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("SortOrder", StringComparison.OrdinalIgnoreCase)) && intValue < 0))
                    return $"Pole {property.Name} ma nieprawidłową wartość.";
                if (property.Name.Contains("RetentionDays", StringComparison.OrdinalIgnoreCase) && intValue > 36500)
                    return $"Pole {property.Name} przekracza maksymalny okres retencji.";
                if (property.Name.Contains("RetentionMonths", StringComparison.OrdinalIgnoreCase) && intValue > 1200)
                    return $"Pole {property.Name} przekracza maksymalny okres retencji.";
                if (property.Name.Contains("Cooldown", StringComparison.OrdinalIgnoreCase) && intValue > 3650)
                    return $"Pole {property.Name} przekracza maksymalny okres cooldown.";
                if (property.Name.Contains("SortOrder", StringComparison.OrdinalIgnoreCase) && intValue > 10000)
                    return $"Pole {property.Name} ma wartość poza dozwolonym zakresem.";
            }

            if (value is decimal decimalValue)
            {
                if (decimalValue is > 1_000_000_000_000m or < -1_000_000_000_000m)
                    return $"Pole {property.Name} ma wartość poza dozwolonym zakresem.";
                if ((property.Name.Contains("Price", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase)) && decimalValue < 0)
                    return $"Pole {property.Name} nie może być ujemne.";
            }

            if (value is IDictionary dictionary)
            {
                if (dictionary.Count > RequestLimits.Dictionary)
                    return $"Pole {property.Name} może zawierać maksymalnie {RequestLimits.Dictionary} elementów.";
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key && key.Length > RequestLimits.Name)
                        return $"Klucz w polu {property.Name} może mieć maksymalnie {RequestLimits.Name} znaków.";
                    if (entry.Value is string dictionaryValue && dictionaryValue.Length > RequestLimits.Note)
                        return $"Wartość w polu {property.Name} może mieć maksymalnie {RequestLimits.Note} znaków.";
                }
                continue;
            }

            if (value is DateOnly dateOnly && (dateOnly < RequestLimits.MinDate || dateOnly > RequestLimits.MaxDate))
                return $"Pole {property.Name} ma datę poza dozwolonym zakresem.";

            if (value is DateTimeOffset dateTime &&
                (dateTime < RequestLimits.MinDateTime || dateTime > RequestLimits.MaxDateTime))
                return $"Pole {property.Name} ma datę poza dozwolonym zakresem.";

            if (value is ICollection collection)
            {
                var max = CollectionLimit(property.Name);
                if (collection.Count > max)
                    return $"Pole {property.Name} może zawierać maksymalnie {max} elementów.";

                foreach (var item in collection)
                {
                    if (item is Guid itemGuid && itemGuid == Guid.Empty)
                        return $"Pole {property.Name} zawiera pusty identyfikator.";
                    if (item is string itemText && (string.IsNullOrWhiteSpace(itemText) || itemText.Length > RequestLimits.Name))
                        return $"Pole {property.Name} zawiera nieprawidłową wartość tekstową.";
                    if (property.Name.Contains("ThresholdDays", StringComparison.OrdinalIgnoreCase) &&
                        item is int day && (day < 0 || day > 3650))
                        return $"Pole {property.Name} zawiera wartość poza dozwolonym zakresem 0-3650.";
                }
            }
        }
        return null;
    }

    private static System.Reflection.NullabilityState NullabilityState(System.Reflection.PropertyInfo property) =>
        NullabilityStates.GetOrAdd(property, static p => new System.Reflection.NullabilityInfoContext().Create(p).ReadState);

    private static int StringLimit(Type requestType, string propertyName)
    {
        // Limits are intentionally no wider than the corresponding persisted columns. Where one property
        // name is reused by multiple contracts, the request type disambiguates the few wider columns.
        if (propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)) return RequestLimits.Password;
        if (propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)) return RequestLimits.Token;
        if (propertyName.Equals("Code", StringComparison.OrdinalIgnoreCase) || propertyName.EndsWith("Code", StringComparison.OrdinalIgnoreCase)) return RequestLimits.Code;
        if (propertyName.EndsWith("Email", StringComparison.OrdinalIgnoreCase)) return RequestLimits.Email;
        if (propertyName is "LogoUrl" or "PrivacyNoticeUrl") return 600;
        // Podpis to `data:image/png;base64,...`, a nie adres - regula od "Url" ponizej dalaby mu 2048
        // znakow i odrzucala kazdy realny rysunek z canvasu.
        if (propertyName == "SignatureDataUrl") return RequestLimits.SignatureDataUrl;
        if (propertyName.Contains("Url", StringComparison.OrdinalIgnoreCase) || propertyName.Contains("Uri", StringComparison.OrdinalIgnoreCase)) return RequestLimits.Url;
        if (propertyName.Contains("LayoutJson", StringComparison.OrdinalIgnoreCase)) return RequestLimits.Json;

        if (propertyName is "FirstName" or "EmployeeFirstName") return 80;
        if (propertyName is "LastName" or "EmployeeLastName") return 120;
        if (propertyName == "Phone") return 40;
        if (propertyName == "EmployeeNumber") return 80;
        if (propertyName == "RelationType") return 40;
        if (propertyName == "JobTitle") return 120;
        if (propertyName is "Language" or "PreferredLanguage" or "Country" or "HolidayCalendarCountryCode" or "Currency") return 8;
        if (propertyName == "TimeZone") return 80;
        if (propertyName == "DisplayName") return 160;
        if (propertyName == "OrganizationName") return 160;
        if (propertyName == "AssetName") return 180;
        if (propertyName == "AssetTag") return 80;
        if (propertyName == "SerialNumber") return 120;
        if (propertyName is "Manufacturer" or "Model") return 120;
        if (propertyName == "TeamName") return 120;
        if (propertyName == "CategoryName") return 120;
        if (propertyName == "ProcedureTitle") return 180;
        if (propertyName == "Location") return 180;
        if (propertyName is "DestinationLocation" or "ReturnLocation") return 200;
        if (propertyName == "DefaultReturnLocation") return 240;
        if (propertyName == "CostCenter") return 80;
        if (propertyName == "Key") return 80;
        if (propertyName == "Label") return 120;
        if (propertyName is "Color" or "BackgroundColor") return 9;
        if (propertyName == "Icon") return 40;
        if (propertyName == "RoleKey") return 60;
        if (propertyName == "PermissionKey") return 80;
        if (propertyName == "StatusKey") return 60;
        if (propertyName == "PlanKey") return 40;
        if (propertyName == "Version") return 40;
        if (propertyName == "Owner") return 120;
        if (propertyName == "AppliesTo") return 240;
        if (propertyName == "LicenseKey") return 400; // fits the encrypted varchar(600) envelope
        if (propertyName == "Caption") return RequestLimits.Caption;
        if (propertyName is "IssueCondition" or "ReturnCondition") return 400;
        if (propertyName == "ReturnChecklistTemplate") return 2000;
        if (propertyName == "Options") return 1000;
        if (propertyName == "Comment") return 1000;
        if (propertyName is "DamageAssessmentNotes" or "Resolution") return 2000;
        if (propertyName == "Reason") return 1000;
        if (propertyName == "Message") return 1000;

        if (propertyName == "Name")
        {
            if (requestType.Name is "CreateLicenseRequest" or "UpdateLicenseRequest" or "UpdateOrganizationRequest") return 160;
            if (requestType.Name == "SaveJobProfileRequest") return 140;
            return 120;
        }

        if (propertyName == "Vendor") return requestType.Name.Contains("ServiceTicket", StringComparison.Ordinal) ? 200 : 160;
        if (propertyName == "Description")
            return requestType.Name.Contains("ServiceTicket", StringComparison.Ordinal) || requestType.Name.Contains("AssetAuditCampaign", StringComparison.Ordinal) ? 2000 : 800;
        if (propertyName.Contains("Notes", StringComparison.OrdinalIgnoreCase))
            return requestType.Name.Contains("Inspection", StringComparison.Ordinal) || requestType.Name.Contains("Offboarding", StringComparison.Ordinal) ? 2000 : 800;
        if (propertyName.Contains("CustomEmails", StringComparison.OrdinalIgnoreCase)) return 2000;

        return RequestLimits.Name;
    }

    private static int CollectionLimit(string propertyName) =>
        propertyName.Contains("Roles", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Threshold", StringComparison.OrdinalIgnoreCase)
            ? RequestLimits.SmallCollection
            : RequestLimits.Collection;

    private static bool IsApplicationContract(Type type)
    {
        var ns = type.Namespace ?? string.Empty;
        return ns.StartsWith("Tenebit.Application", StringComparison.Ordinal) ||
               ns.StartsWith("Tenebit.Api.Endpoints", StringComparison.Ordinal);
    }

    private static bool IsSimple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsPrimitive || type.IsEnum || type.IsValueType) return true;
        return type == typeof(string) || type == typeof(byte[]) || type == typeof(Uri);
    }
}

public static class RequestLimits
{
    public const int ShortKey = 80;
    public const int Name = 240;
    public const int Email = 240;
    public const int Token = 1024;
    public const int Password = 128;
    public const int Code = 128;
    public const int Url = 2048;

    /// <summary>Podpis odreczny jako PNG w data URL. 200 KB binarnie to ~273 KB w base64; ten sam
    /// prog pilnuje <see cref="Assignments.SignatureDataUrl"/> i domena wydania.</summary>
    public const int SignatureDataUrl = 300_000;
    public const int Caption = 500;
    public const int Note = 2000;
    public const int LongText = 4000;
    public const int Json = 131072;
    public const int Collection = 100;
    public const int SmallCollection = 25;
    public const int Dictionary = 100;
    public static readonly DateOnly MinDate = new(1900, 1, 1);
    public static readonly DateOnly MaxDate = new(2200, 12, 31);
    public static readonly DateTimeOffset MinDateTime = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset MaxDateTime = new(2200, 12, 31, 23, 59, 59, TimeSpan.Zero);
}
