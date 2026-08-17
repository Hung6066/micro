using System.Reflection;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Domain.Entities;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class DevicePostureAssessmentContractTests
{
    [Fact]
    public void AssessmentResponse_ExposesOnlyHashPrefixAndMetadata()
    {
        var mapper = typeof(DevicePostureEndpoints).GetMethod("ToAssessmentResponse", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mapper);

        var assessment = new DevicePostureAssessment
        {
            EvidenceHash = new string('a', 64),
            SignalsJson = "{\"managed\":true}",
            Provider = "advanced-compliance",
            DeviceId = "device-1",
            Decision = "observe",
            PolicyVersion = "1",
            CorrelationId = "trace-1",
            ObservedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        var response = mapper!.Invoke(null, [assessment, DateTime.UtcNow]);
        Assert.NotNull(response);
        var properties = response!.GetType().GetProperties().Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("evidenceHashPrefix", properties);
        Assert.DoesNotContain("EvidenceHash", properties);
        Assert.DoesNotContain("SignalsJson", properties);
        var prefix = (string)response.GetType().GetProperty("evidenceHashPrefix")!.GetValue(response)!;
        Assert.Equal(12, prefix.Length);
    }
}
