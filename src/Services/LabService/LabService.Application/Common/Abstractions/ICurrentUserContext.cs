namespace His.Hope.LabService.Application.Common.Abstractions;

public interface ICurrentUserContext
{
    string UserId { get; }
    string FullName { get; }
    bool IsAuthenticated { get; }
}
