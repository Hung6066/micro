namespace His.Hope.Authorization;

public interface IResourceAuthorizationEvaluator
{
    ValueTask<AuthorizationDecision> EvaluateAsync(
        AuthorizationContext context,
        CancellationToken cancellationToken = default);
}

public interface IAuthorizationDecisionSink
{
    ValueTask WriteAsync(
        AuthorizationDecisionAudit audit,
        CancellationToken cancellationToken = default);
}
