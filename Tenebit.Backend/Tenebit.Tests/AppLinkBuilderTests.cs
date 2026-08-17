using Microsoft.Extensions.Configuration;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class AppLinkBuilderTests
{
    private static AppLinkBuilder CreateBuilder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:PublicUrl"] = "https://app.tenebit.test" })
            .Build();
        return new AppLinkBuilder(configuration);
    }

    [Fact]
    public void BuildAppUrl_JoinsRelativePathToConfiguredOrigin()
    {
        var builder = CreateBuilder();

        var url = builder.BuildAppUrl("/dashboard?checkout=success");

        Assert.Equal("https://app.tenebit.test/dashboard?checkout=success", url);
    }

    [Theory]
    [InlineData("https://evil.test/phish")]
    [InlineData("//evil.test/phish")]
    [InlineData("not-a-path")]
    public void BuildAppUrl_RejectsNonRelativePath_AndFallsBackToDashboard(string maliciousPath)
    {
        var builder = CreateBuilder();

        var url = builder.BuildAppUrl(maliciousPath);

        Assert.Equal("https://app.tenebit.test/dashboard", url);
    }
}
