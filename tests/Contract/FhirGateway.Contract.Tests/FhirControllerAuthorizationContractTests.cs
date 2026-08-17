using System.Reflection;
using FluentAssertions;
using His.Hope.FhirGateway.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace His.Hope.FhirGateway.Contract.Tests;

public sealed class FhirControllerAuthorizationContractTests
{
    private static readonly Type ControllerType = typeof(FhirController);

    [Fact]
    public void Controller_exposes_the_expected_resource_boundary()
    {
        ControllerType.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        ControllerType.GetCustomAttribute<RouteAttribute>()?.Template.Should().Be("fhir/r4");
    }

    [Fact]
    public void Every_resource_action_has_a_specific_authorization_policy()
    {
        var actions = ControllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<HttpGetAttribute>() is not null)
            .Where(method => method.Name is not nameof(FhirController.GetMetadata))
            .ToArray();

        actions.Should().HaveCount(3);
        foreach (var action in actions)
        {
            var authorize = action.GetCustomAttribute<AuthorizeAttribute>();
            authorize.Should().NotBeNull($"{action.Name} must enforce authorization at the service boundary");
            authorize!.Policy.Should().Be(
                action.Name.Contains("Patient", StringComparison.Ordinal)
                    ? "Fhir.Patient.Read"
                    : "Fhir.Encounter.Read");
        }
    }

    [Fact]
    public void Only_metadata_is_anonymous()
    {
        var metadata = ControllerType.GetMethod(nameof(FhirController.GetMetadata));
        metadata.Should().NotBeNull();
        metadata!.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();

        ControllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<HttpGetAttribute>() is not null)
            .Where(method => method.Name is not nameof(FhirController.GetMetadata))
            .Should()
            .OnlyContain(method => method.GetCustomAttribute<AllowAnonymousAttribute>() == null);
    }
}
