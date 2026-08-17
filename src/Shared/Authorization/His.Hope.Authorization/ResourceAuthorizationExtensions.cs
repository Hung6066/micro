using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.Authorization;

/// <summary>
/// Loads only trusted resource metadata and evaluates it before a command.
/// The owner service supplies the id predicate and facility selector so the
/// shared package never needs to know domain entity types.
/// </summary>
public static class ResourceAuthorizationExtensions
{
    public static async ValueTask<AuthorizationDecision> EvaluateResourceAsync<TEntity>(
        this IResourceAuthorizationEvaluator evaluator,
        IQueryable<TEntity> source,
        Expression<Func<TEntity, bool>> idPredicate,
        Expression<Func<TEntity, string?>> facilitySelector,
        ClaimsPrincipal principal,
        string action,
        string resourceType,
        string canonicalId,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var facilityId = await source
            .AsNoTracking()
            .Where(idPredicate)
            .Select(facilitySelector)
            .SingleOrDefaultAsync(cancellationToken);

        if (facilityId is null)
        {
            return await evaluator.EvaluateAsync(
                new AuthorizationContext(
                    principal,
                    action,
                    new AuthorizationResource(resourceType, canonicalId),
                    RequireResource: true,
                    ResourceLookupFailed: true),
                cancellationToken);
        }

        return await evaluator.EvaluateAsync(
            new AuthorizationContext(
                principal,
                action,
                new AuthorizationResource(resourceType, canonicalId, FacilityId: facilityId),
                RequireResource: true),
            cancellationToken);
    }
}
