using Microsoft.Extensions.Configuration;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

internal sealed class EmailAvailability : IEmailAvailability
{
    private readonly IConfiguration _configuration;

    public EmailAvailability(IConfiguration configuration) => _configuration = configuration;

    public bool Enabled => _configuration.GetValue("Email:Enabled", false);
}
