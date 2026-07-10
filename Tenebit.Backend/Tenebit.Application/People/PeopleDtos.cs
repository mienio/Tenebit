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
    PersonRelationType RelationType,
    string? JobTitle,
    Guid? TeamId,
    string? TeamName,
    Guid? ManagerId,
    string? Location,
    string? CostCenter,
    bool IsActive);

public sealed record CreatePersonRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? EmployeeNumber,
    PersonRelationType RelationType,
    string? JobTitle,
    Guid? TeamId,
    Guid? ManagerId,
    string? Location,
    string? CostCenter);

public sealed record UpdatePersonRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? EmployeeNumber,
    PersonRelationType RelationType,
    string? JobTitle,
    Guid? TeamId,
    Guid? ManagerId,
    string? Location,
    string? CostCenter,
    bool IsActive);

public sealed record TeamResponse(Guid Id, string Name, Guid? ManagerId, string? CostCenter);
public sealed record CreateTeamRequest(string Name, Guid? ManagerId, string? CostCenter);
