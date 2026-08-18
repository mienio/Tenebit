using Tenebit.Domain.People;

namespace Tenebit.Application.People;

public sealed record PersonResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    string? EmployeeNumber,
    string RelationType,
    string? JobTitle,
    Guid? TeamId,
    string? TeamName,
    Guid? ManagerId,
    string? Location,
    string? CostCenter,
    bool IsActive,
    EmploymentStatus EmploymentStatus,
    DateTimeOffset? EmploymentEndsAt,
    DateTimeOffset? DeactivatedAt,
    string? PreferredLanguage);

[ValidatedRequest]
public sealed record CreatePersonRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? EmployeeNumber,
    string RelationType,
    string? JobTitle,
    Guid? TeamId,
    Guid? ManagerId,
    string? Location,
    string? CostCenter,
    string? PreferredLanguage = null);

[ValidatedRequest]
public sealed record UpdatePersonRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? EmployeeNumber,
    string RelationType,
    string? JobTitle,
    Guid? TeamId,
    Guid? ManagerId,
    string? Location,
    string? CostCenter,
    bool IsActive,
    string? PreferredLanguage = null);

[ValidatedRequest]
public sealed record StartOffboardingRequest(DateTimeOffset EmploymentEndsAt);

public sealed record TeamResponse(Guid Id, string Name, Guid? ManagerId, string? CostCenter);
[ValidatedRequest]
public sealed record CreateTeamRequest(string Name, Guid? ManagerId, string? CostCenter);
[ValidatedRequest]
public sealed record UpdateTeamRequest(string Name, Guid? ManagerId, string? CostCenter);

public sealed record PersonRelationTypeResponse(Guid Id, string Name);
[ValidatedRequest]
public sealed record CreatePersonRelationTypeRequest(string Name);
[ValidatedRequest]
public sealed record UpdatePersonRelationTypeRequest(string Name);
