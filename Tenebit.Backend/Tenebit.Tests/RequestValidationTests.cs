using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Common;
using Tenebit.Application.Identity;
using Tenebit.Application.JobProfiles;
using Tenebit.Application.Organizations;
using Tenebit.Application.People;
using Tenebit.Application.Offboarding;

namespace Tenebit.Tests;

public sealed class RequestValidationTests
{
    private static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), validateAllProperties: true);

    [Fact]
    public void Every_request_contract_is_explicitly_registered_for_validation()
    {
        var requestTypes = new[] { typeof(LoginRequest).Assembly, typeof(Program).Assembly }
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal) && type.IsClass)
            .OrderBy(type => type.FullName)
            .ToList();

        Assert.NotEmpty(requestTypes);
        Assert.All(requestTypes, type =>
            Assert.True(type.GetCustomAttribute<ValidatedRequestAttribute>() is not null,
                $"{type.FullName} nie ma [ValidatedRequest] i omija obowiązkowy kontrakt walidacji."));
    }

    [Fact]
    public void Convention_validator_rejects_oversized_password()
    {
        var error = RequestObjectValidator.Validate(new LoginRequest("user@example.com", new string('x', RequestLimits.Password + 1)));
        Assert.NotNull(error);
    }

    [Fact]
    public void Convention_validator_rejects_null_non_nullable_property_even_without_DataAnnotations()
    {
        var error = RequestObjectValidator.Validate(new UpdateOrganizationRequest(null!, "PL", "pl", "PLN", "Europe/Warsaw", null));
        Assert.NotNull(error);
    }

    [Fact]
    public void Convention_validator_rejects_oversized_collection()
    {
        var tooManyIds = Enumerable.Range(0, RequestLimits.Collection + 1).Select(_ => Guid.NewGuid()).ToList();
        var error = RequestObjectValidator.Validate(new SaveJobProfileRequest("Developer", null, null, tooManyIds, []));
        Assert.NotNull(error);
    }

    [Fact]
    public void Convention_validator_recurses_into_nested_request_objects()
    {
        var request = new CreateAssignmentRequest(
            Guid.NewGuid(),
            [new AssignmentAssetRequest(Guid.Empty, null)],
            [],
            null,
            null);

        var error = RequestObjectValidator.Validate(request);
        Assert.NotNull(error);
    }

    [Fact]
    public void Convention_validator_recurses_into_public_offboarding_answers()
    {
        var request = new SubmitPublicOffboardingResponseRequest([
            new PublicOffboardingItemAnswer(Guid.NewGuid(), " ", null)
        ]);

        var error = RequestObjectValidator.Validate(request);
        Assert.NotNull(error);
    }


    [Fact]
    public void Convention_validator_rejects_string_longer_than_persisted_column_budget()
    {
        var error = RequestObjectValidator.Validate(new UpdateOrganizationRequest(
            new string('x', 161), "PL", "pl", "PLN", "Europe/Warsaw", null));
        Assert.NotNull(error);
    }

    [Fact]
    public void Convention_validator_rejects_absurd_dates()
    {
        var error = RequestObjectValidator.Validate(new StartOffboardingRequest(
            new DateTimeOffset(2500, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.NotNull(error);
    }

    [Fact]
    public void LoginRequest_with_null_email_is_invalid()
    {
        Assert.False(IsValid(new LoginRequest(null!, "password")));
    }

    [Fact]
    public void LoginRequest_with_malformed_email_is_invalid()
    {
        Assert.False(IsValid(new LoginRequest("not-an-email", "password")));
    }

    [Fact]
    public void LoginRequest_with_valid_data_is_valid()
    {
        Assert.True(IsValid(new LoginRequest("user@example.com", "password")));
    }

    [Fact]
    public void RegisterRequest_with_null_fields_is_invalid()
    {
        Assert.False(IsValid(new RegisterRequest(null!, null!, null!, null!, null!)));
    }

    [Fact]
    public void SaveAssetFieldDefinitionRequest_with_empty_key_is_invalid()
    {
        Assert.False(IsValid(new SaveAssetFieldDefinitionRequest("", "Label", Domain.Assets.AssetFieldType.Text, null, false)));
    }

    [Fact]
    public void SaveAssetFieldDefinitionRequest_with_valid_data_is_valid()
    {
        Assert.True(IsValid(new SaveAssetFieldDefinitionRequest("warranty_months", "Gwarancja (mies.)", Domain.Assets.AssetFieldType.Number, null, false)));
    }
}
