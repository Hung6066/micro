using System.Security.Claims;
using FluentAssertions;
using His.Hope.AspNetCore.Authentication;
using Xunit;

namespace His.Hope.AspNetCore.Tests;

public sealed class HisHopeUserClaimsTests
{
    [Fact]
    public void Normalization_copies_name_identifier_to_sub()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")], "Bearer"));

        HisHopeUserClaims.NormalizeSubjectClaims(principal);

        principal.FindFirstValue("sub").Should().Be("user-123");
        principal.GetSubject().Should().Be("user-123");
    }

    [Fact]
    public void Normalization_copies_sub_to_name_identifier()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-456")], "Bearer"));

        HisHopeUserClaims.NormalizeSubjectClaims(principal);

        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("user-456");
        principal.GetSubject().Should().Be("user-456");
    }
}
