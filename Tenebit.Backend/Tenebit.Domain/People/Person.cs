using Tenebit.Domain.Common;

namespace Tenebit.Domain.People;

public sealed class Person
{
    private Person() { }

    public Person(Guid organizationId, string firstName, string lastName, string email)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CreatedAt = DateTimeOffset.UtcNow;
        IsActive = true;
        Update(firstName, lastName, email, null, null, PersonRelationType.Employee, null, null, null, null, null);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? EmployeeNumber { get; private set; }
    public PersonRelationType RelationType { get; private set; }
    public string? JobTitle { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? ManagerId { get; private set; }
    public string? Location { get; private set; }
    public string? CostCenter { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public void Update(
        string firstName,
        string lastName,
        string email,
        string? phone,
        string? employeeNumber,
        PersonRelationType relationType,
        string? jobTitle,
        Guid? teamId,
        Guid? managerId,
        string? location,
        string? costCenter)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("Imię i nazwisko są wymagane.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainException("Poprawny adres e-mail jest wymagany.");
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Phone = Normalize(phone);
        EmployeeNumber = Normalize(employeeNumber);
        RelationType = relationType;
        JobTitle = Normalize(jobTitle);
        TeamId = teamId;
        ManagerId = managerId;
        Location = Normalize(location);
        CostCenter = Normalize(costCenter);
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
