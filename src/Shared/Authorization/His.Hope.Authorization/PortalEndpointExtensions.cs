using System.Security.Claims;
using His.Hope.Authorization.Requirements;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace His.Hope.Authorization;

public static class PortalClassGuard
{
    public static IResult? EnsurePortalClass(ClaimsPrincipal user, params string[] allowedPortalClasses)
    {
        var portalClass = user.FindFirst(PortalClassConstants.Claim)?.Value;
        if (string.IsNullOrWhiteSpace(portalClass))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Missing portal class",
                detail: "The access token must include a portal_class claim.");
        }

        if (allowedPortalClasses.Any(allowed =>
                string.Equals(portalClass, allowed, StringComparison.OrdinalIgnoreCase)))
            return null;

        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Invalid portal class",
            detail: "This route is not available for the current portal class.");
    }

    public static IResult? EnsureEndUserPortal(ClaimsPrincipal user) =>
        EnsurePortalClass(user, PortalClassConstants.EndUser);

    public static IResult? EnsureOperatorPortal(ClaimsPrincipal user) =>
        EnsurePortalClass(user, PortalClassConstants.Operator);

    public static IResult? EnsureNotEndUserPortal(ClaimsPrincipal user)
    {
        var portalClass = user.FindFirst(PortalClassConstants.Claim)?.Value;
        return string.Equals(portalClass, PortalClassConstants.EndUser, StringComparison.OrdinalIgnoreCase)
            ? Results.Forbid()
            : null;
    }
}

public static class PortalEndpointExtensions
{
    public static RouteGroupBuilder RequirePortalClass(this RouteGroupBuilder group, params string[] allowedPortalClasses) =>
        group.AddEndpointFilter((context, next) =>
        {
            var error = PortalClassGuard.EnsurePortalClass(context.HttpContext.User, allowedPortalClasses);
            return error is null ? next(context) : ValueTask.FromResult<object?>(error);
        });

    public static RouteGroupBuilder RequireEndUserPortal(this RouteGroupBuilder group) =>
        group.AddEndpointFilter((context, next) =>
        {
            var error = PortalClassGuard.EnsureEndUserPortal(context.HttpContext.User);
            return error is null ? next(context) : ValueTask.FromResult<object?>(error);
        });

    public static RouteGroupBuilder RequireOperatorPortal(this RouteGroupBuilder group) =>
        group.AddEndpointFilter((context, next) =>
        {
            var error = PortalClassGuard.EnsureOperatorPortal(context.HttpContext.User);
            return error is null ? next(context) : ValueTask.FromResult<object?>(error);
        });

    public static RouteGroupBuilder BlockEndUserPortal(this RouteGroupBuilder group) =>
        group.AddEndpointFilter((context, next) =>
        {
            var error = PortalClassGuard.EnsureNotEndUserPortal(context.HttpContext.User);
            return error is null ? next(context) : ValueTask.FromResult<object?>(error);
        });
}

public static class CommerceAuthorizationPolicies
{
    public const string BuyerRead = "Commerce.Buyer.Read";
    public const string BuyerWrite = "Commerce.Buyer.Write";
    public const string OperatorFulfill = "Commerce.Operator.Fulfill";
}

public static class CommerceAuthorizationPolicyExtensions
{
    public static AuthorizationBuilder AddCommerceAuthorizationPolicies(this AuthorizationBuilder builder)
    {
        builder.AddPolicy(CommerceAuthorizationPolicies.BuyerRead, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(
                new PortalClassRequirement(PortalClassConstants.EndUser),
                new CommerceScopeOrPermissionRequirement(
                    "commerce.read",
                    HisHopePermissions.Commerce.CatalogView)));

        builder.AddPolicy(CommerceAuthorizationPolicies.BuyerWrite, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(
                new PortalClassRequirement(PortalClassConstants.EndUser),
                new CommerceScopeOrPermissionRequirement(
                    "commerce.write",
                    HisHopePermissions.Commerce.OrdersCreate)));

        builder.AddPolicy(CommerceAuthorizationPolicies.OperatorFulfill, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(
                new PortalClassRequirement(PortalClassConstants.Operator),
                new CommerceScopeOrPermissionRequirement(
                    "commerce.write",
                    HisHopePermissions.Commerce.OrdersUpdate)));

        return builder;
    }
}

public static class ContentAuthorizationPolicies
{
    public const string Manage = "Content.Manage";
    public const string InquiriesView = "Content.Inquiries.View";
}

public static class ContentAuthorizationPolicyExtensions
{
    public static AuthorizationBuilder AddContentAuthorizationPolicies(this AuthorizationBuilder builder)
    {
        builder.AddPolicy(ContentAuthorizationPolicies.Manage, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(HisHopePermissions.Content.Manage)));

        builder.AddPolicy(ContentAuthorizationPolicies.InquiriesView, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(HisHopePermissions.Content.InquiriesView)));

        return builder;
    }
}
