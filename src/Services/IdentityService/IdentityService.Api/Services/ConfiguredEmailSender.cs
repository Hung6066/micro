using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.ServiceDefaults;

namespace His.Hope.IdentityService.Api.Services;

public sealed class ConfiguredEmailSender(IExternalEmailSender sender) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default) =>
        sender.SendAsync(to, subject, body, ct);
}
