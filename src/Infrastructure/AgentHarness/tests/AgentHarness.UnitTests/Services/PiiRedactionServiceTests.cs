using FluentAssertions;
using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.UnitTests.Services;

public class PiiRedactionServiceTests
{
    [Fact]
    public void Redact_ShouldRemoveHealthcarePhiContexts()
    {
        var service = new PiiRedactionService();

        var result = service.Redact("Patient name: Jane Doe DOB: 01/02/1980 MRN: ABC-12345 lives at 123 Main Street");

        result.Should().Contain("[REDACTED_NAME]");
        result.Should().Contain("[REDACTED_DOB]");
        result.Should().Contain("[REDACTED_MRN]");
        result.Should().Contain("[REDACTED_ADDRESS]");
        result.Should().NotContain("Jane Doe");
        result.Should().NotContain("01/02/1980");
        result.Should().NotContain("ABC-12345");
        result.Should().NotContain("123 Main Street");
    }
}
