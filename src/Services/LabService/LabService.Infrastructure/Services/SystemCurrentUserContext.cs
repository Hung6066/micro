using His.Hope.LabService.Application.Common.Abstractions;

namespace His.Hope.LabService.Infrastructure.Services;

internal sealed class SystemCurrentUserContext : ICurrentUserContext
{
    public string UserId => "system";
    public string FullName => "System";
    public bool IsAuthenticated => false;
}
