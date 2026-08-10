using System.Text.Json.Serialization;

namespace His.Hope.IdentityService.Application.DTOs;

// ─── SCIM v2 Core Schemas ───

public class ScimUserRequest
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:schemas:core:2.0:User" };

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public ScimName? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("emails")]
    public List<ScimEmail>? Emails { get; set; }

    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    [JsonPropertyName("phoneNumbers")]
    public List<ScimPhoneNumber>? PhoneNumbers { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("roles")]
    public List<ScimRole>? Roles { get; set; }

    [JsonPropertyName("entitlements")]
    public List<ScimEntitlement>? Entitlements { get; set; }

    // His.Hope extension
    [JsonPropertyName("urn:ietf:params:scim:schemas:extension:his-hope:2.0:User")]
    public ScimHisHopeExtension? HisHopeExtension { get; set; }
}

public class ScimName
{
    [JsonPropertyName("formatted")]
    public string? Formatted { get; set; }
    [JsonPropertyName("familyName")]
    public string? FamilyName { get; set; }
    [JsonPropertyName("givenName")]
    public string? GivenName { get; set; }
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
}

public class ScimEmail
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("primary")]
    public bool Primary { get; set; }
}

public class ScimPhoneNumber
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class ScimRole
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    [JsonPropertyName("display")]
    public string? Display { get; set; }
}

public class ScimEntitlement
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class ScimHisHopeExtension
{
    [JsonPropertyName("licenseNumber")]
    public string? LicenseNumber { get; set; }
    [JsonPropertyName("specialty")]
    public string? Specialty { get; set; }
    [JsonPropertyName("facilityId")]
    public string? FacilityId { get; set; }
}

// ─── SCIM v2 Responses ───

public class ScimUserResponse
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:schemas:core:2.0:User" };

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public ScimName? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("emails")]
    public List<ScimEmail>? Emails { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("meta")]
    public ScimMeta Meta { get; set; } = new();

    [JsonPropertyName("urn:ietf:params:scim:schemas:extension:his-hope:2.0:User")]
    public ScimHisHopeExtension? HisHopeExtension { get; set; }
}

public class ScimMeta
{
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = "User";
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }
    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; }
    [JsonPropertyName("location")]
    public string? Location { get; set; }
}

public class ScimListResponse<T>
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:api:messages:2.0:ListResponse" };

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    [JsonPropertyName("itemsPerPage")]
    public int ItemsPerPage { get; set; }

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; }

    [JsonPropertyName("Resources")]
    public List<T> Resources { get; set; } = new();
}

public class ScimError
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:api:messages:2.0:Error" };

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
}

// ─── SCIM Patch Operation (RFC 7644 §3.5.2) ───

public class ScimPatchRequest
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:api:messages:2.0:PatchOp" };

    [JsonPropertyName("Operations")]
    public List<ScimPatchOperation> Operations { get; set; } = new();
}

public class ScimPatchOperation
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

// ─── SCIM Group ───

public class ScimGroupRequest
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:schemas:core:2.0:Group" };

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<ScimGroupMember>? Members { get; set; }
}

public class ScimGroupMember
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    [JsonPropertyName("display")]
    public string? Display { get; set; }
}

public class ScimGroupResponse
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:schemas:core:2.0:Group" };

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<ScimGroupMember>? Members { get; set; }

    [JsonPropertyName("meta")]
    public ScimMeta Meta { get; set; } = new();
}

// ─── SCIM Query ───

public class ScimQueryParams
{
    public string? Filter { get; set; }
    public int StartIndex { get; set; } = 1;
    public int Count { get; set; } = 100;
}
