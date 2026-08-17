using System.ComponentModel.DataAnnotations;
using Tenebit.Application.Assets;
using Tenebit.Application.Identity;

namespace Tenebit.Tests;

// AUD-007 regression: null email login zakończyło się kiedyś NullReferenceException/500
// (OrganizationUserRepository.FindByEmailAsync). Te testy pilnują, że DataAnnotations na DTO
// faktycznie łapią null/pusty/za długi input, zanim ValidationEndpointFilter w Tenebit.Api
// zdąży go odrzucić przed handlerem.
public sealed class RequestValidationTests
{
    private static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), validateAllProperties: true);

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
