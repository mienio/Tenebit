using Tenebit.Domain.Assets;

namespace Tenebit.Application.Reservations;

public sealed record ReservationCatalogResponse(bool HasPersonRecord, IReadOnlyList<ReservationCatalogCategoryResponse> Categories, IReadOnlyList<ReservationCatalogKitResponse> Kits);

public sealed record ReservationCatalogCategoryResponse(Guid Id, string Name, string? Description, string? ImageUrl, string? Icon, ReservationMode ReservationMode, int AvailableCount);

public sealed record ReservationCatalogKitResponse(Guid Id, string Name, string? Description, int AvailableCount, IReadOnlyList<ReservationCatalogKitItemResponse> Items);

public sealed record ReservationCatalogKitItemResponse(Guid CategoryId, string CategoryName, int RequiredQuantity);
