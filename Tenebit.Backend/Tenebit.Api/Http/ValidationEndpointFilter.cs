using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace Tenebit.Api.Http;

// AUD-007: brakowało centralnej walidacji requestów — DTO z typem string nie gwarantował, że klient
// nie wyśle null, co już raz zakończyło się NullReferenceException -> 500 przy logowaniu (potwierdzone
// w logach audytu). Ten filtr działa globalnie na całej grupie /api: dla każdego argumentu handlera,
// którego typ ma choć jeden DataAnnotations atrybut na właściwości, uruchamia walidację i zwraca 400
// zamiast wpuszczać nieprawidłowe dane do Application/Infrastructure. DTO bez atrybutów są pomijane
// (no-op) — pokrycie rośnie przyrostowo, bez wymuszania jednorazowej migracji wszystkich request DTO.
public sealed class ValidationEndpointFilter : IEndpointFilter
{
    private static readonly ConcurrentDictionary<Type, bool> HasValidationAttributesCache = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            var error = Validate(argument);
            if (error is not null)
            {
                return Results.Json(new ErrorResponse(error, "VALIDATION_ERROR"), statusCode: StatusCodes.Status400BadRequest);
            }
        }

        return await next(context);
    }

    private static string? Validate(object? argument)
    {
        if (argument is null) return null;

        // Listy DTO (np. PUT .../fields przyjmujący IReadOnlyList<SaveAssetFieldDefinitionRequest>) —
        // walidujemy każdy element, nie samą kolekcję.
        if (argument is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                var itemError = Validate(item);
                if (itemError is not null) return itemError;
            }
            return null;
        }

        var type = argument.GetType();
        if (!HasValidationAttributesCache.GetOrAdd(type, static t => t.GetProperties().Any(p => p.GetCustomAttributes(typeof(ValidationAttribute), true).Length > 0)))
        {
            return null;
        }

        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(argument, new ValidationContext(argument), results, validateAllProperties: true)
            ? null
            : results.FirstOrDefault()?.ErrorMessage ?? "Nieprawidłowe dane wejściowe.";
    }
}
