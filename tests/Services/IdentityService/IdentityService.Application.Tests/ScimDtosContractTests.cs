using System.Text.Json;
using FluentAssertions;
using His.Hope.IdentityService.Application.DTOs;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class ScimDtosContractTests
{
    [Fact]
    public void User_request_preserves_scim_wire_names_and_extension_values()
    {
        var request = new ScimUserRequest
        {
            UserName = "scim-user",
            Name = new ScimName { GivenName = "Ada", FamilyName = "Lovelace" },
            Emails = [new ScimEmail { Value = "ada@example.test", Type = "work", Primary = true }],
            PhoneNumbers = [new ScimPhoneNumber { Value = "+1-555-0100", Type = "mobile" }],
            Roles = [new ScimRole { Value = "Clinician", Display = "Clinician" }],
            Entitlements = [new ScimEntitlement { Value = "users.read" }],
            HisHopeExtension = new ScimHisHopeExtension
            {
                LicenseNumber = "LIC-1", Specialty = "Cardiology", FacilityId = "facility-a"
            }
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request));
        var json = document.RootElement;
        json.GetProperty("userName").GetString().Should().Be("scim-user");
        json.GetProperty("emails")[0].GetProperty("primary").GetBoolean().Should().BeTrue();
        json.GetProperty("phoneNumbers")[0].GetProperty("type").GetString().Should().Be("mobile");
        json.GetProperty("roles")[0].GetProperty("display").GetString().Should().Be("Clinician");
        json.GetProperty("entitlements")[0].GetProperty("value").GetString().Should().Be("users.read");
        json.GetProperty("urn:ietf:params:scim:schemas:extension:his-hope:2.0:User")
            .GetProperty("facilityId").GetString().Should().Be("facility-a");
    }

    [Fact]
    public void Response_and_list_defaults_follow_rfc_schemas()
    {
        var response = new ScimUserResponse();
        var list = new ScimListResponse<ScimUserResponse>();
        var group = new ScimGroupResponse();
        var patch = new ScimPatchRequest();
        var error = new ScimError();

        response.Schemas.Should().ContainSingle().Which.Should().Be("urn:ietf:params:scim:schemas:core:2.0:User");
        response.Meta.ResourceType.Should().Be("User");
        list.Schemas.Should().ContainSingle().Which.Should().Be("urn:ietf:params:scim:api:messages:2.0:ListResponse");
        list.Resources.Should().BeEmpty();
        group.Schemas.Should().ContainSingle().Which.Should().Be("urn:ietf:params:scim:schemas:core:2.0:Group");
        group.Meta.ResourceType.Should().Be("Group");
        patch.Operations.Should().BeEmpty();
        error.Schemas.Should().ContainSingle().Which.Should().Be("urn:ietf:params:scim:api:messages:2.0:Error");
    }

    [Fact]
    public void Patch_group_and_query_values_round_trip_boundary_inputs()
    {
        var patch = new ScimPatchRequest
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "active", Value = false }]
        };
        var group = new ScimGroupRequest
        {
            DisplayName = "Clinicians",
            Members = [new ScimGroupMember { Value = "user-1", Display = "Ada" }]
        };
        var query = new ScimQueryParams { Filter = "userName eq \"ada\"", StartIndex = 2, Count = 25 };

        JsonSerializer.Deserialize<ScimPatchRequest>(JsonSerializer.Serialize(patch))!.Operations
            .Should().ContainSingle().Which.Op.Should().Be("replace");
        JsonSerializer.Deserialize<ScimGroupRequest>(JsonSerializer.Serialize(group))!.Members
            .Should().ContainSingle().Which.Value.Should().Be("user-1");
        query.Filter.Should().Contain("ada");
        query.StartIndex.Should().Be(2);
        query.Count.Should().Be(25);
    }
}
