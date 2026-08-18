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
        EmploymentStatus = EmploymentStatus.Active;
        IsActive = true;
        Update(firstName, lastName, email, null, null, "Pracownik", null, null, null, null, null);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? EmployeeNumber { get; private set; }
    public string RelationType { get; private set; } = string.Empty;
    public string? JobTitle { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? ManagerId { get; private set; }
    public Guid? LocationId { get; private set; }
    public string? Location { get; private set; }
    public string? CostCenter { get; private set; }
    public EmploymentStatus EmploymentStatus { get; private set; } = EmploymentStatus.Active;
    public DateTimeOffset? EmploymentEndsAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }
    public string? PreferredLanguage { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public void Update(
        string firstName,
        string lastName,
        string email,
        string? phone,
        string? employeeNumber,
        string relationType,
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
        RelationType = relationType.Trim();
        JobTitle = Normalize(jobTitle);
        TeamId = teamId;
        ManagerId = managerId;
        Location = Normalize(location);
        CostCenter = Normalize(costCenter);
    }


    public void SetLocation(Guid? locationId, string? locationPath)
    {
        LocationId = locationId;
        Location = Normalize(locationPath);
    }

    public bool CanReceiveNewObligations => EmploymentStatus == EmploymentStatus.Active;

    public void SetPreferredLanguage(string? preferredLanguage)
    {
        var language = Normalize(preferredLanguage)?.ToLowerInvariant();
        if (language is not null && language is not ("pl" or "en" or "es" or "de"))
        {
            throw new DomainException("Nieobsługiwany preferowany język.");
        }

        PreferredLanguage = language;
    }

    public void StartOffboarding(DateTimeOffset employmentEndsAt)
    {
        if (EmploymentStatus != EmploymentStatus.Active)
        {
            throw new DomainException("Offboarding można rozpocząć tylko dla aktywnej osoby.");
        }

        EmploymentStatus = EmploymentStatus.Offboarding;
        EmploymentEndsAt = employmentEndsAt.ToUniversalTime();
        DeactivatedAt = null;
        IsActive = true;
    }

    public void Deactivate(DateTimeOffset deactivatedAt)
    {
        if (EmploymentStatus == EmploymentStatus.Inactive)
        {
            IsActive = false;
            return;
        }

        EmploymentStatus = EmploymentStatus.Inactive;
        DeactivatedAt = deactivatedAt.ToUniversalTime();
        IsActive = false;
    }

    public void Activate()
    {
        EmploymentStatus = EmploymentStatus.Active;
        EmploymentEndsAt = null;
        DeactivatedAt = null;
        IsActive = true;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
