using System.Security.Cryptography;
using System.Text;
using His.Hope.Contracts.Manufacturing;
using His.Hope.ManufacturingService.Application.Ports;
using Microsoft.EntityFrameworkCore;

public sealed partial class PostgresManufacturingStore : IManufacturingComplianceStore
{
    public IReadOnlyList<SopArtifactDto> GetSopArtifacts(string tenantKey, string? artifactKey, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.SopArtifacts.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(artifactKey)) query = query.Where(x => x.ArtifactKey == artifactKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderByDescending(x => x.Version).ThenByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).Select(ToDto).ToList();
    }

    public (SopArtifactDto? Artifact, string? Error) CreateSopArtifact(CreateSopArtifactRequest request, string tenantKey, string actor)
    {
        var key = request.ArtifactKey.Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content) || request.Version <= 0)
            return (null, "invalid_sop_artifact");
        if (request.Status is not ("Draft" or "Submitted")) return (null, "invalid_sop_artifact_status");
        using var db = dbFactory.CreateDbContext();
        if (db.SopArtifacts.Any(x => x.TenantKey == tenantKey && x.ArtifactKey == key && x.Version == request.Version)) return (null, "sop_artifact_version_exists");
        var entity = new ManufacturingSopArtifactEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, ArtifactKey = key, Title = request.Title.Trim(), Version = request.Version,
            Content = request.Content, ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "text/markdown" : request.ContentType.Trim(),
            Status = request.Status, Checksum = Checksum(request.Content), EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
            CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? actor : request.CreatedBy.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        db.SopArtifacts.Add(entity);
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public (SopArtifactDto? Artifact, string? Error) ChangeSopArtifactStatus(Guid artifactId, string tenantKey, string targetStatus, SopArtifactLifecycleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Actor)) return (null, "invalid_sop_artifact_actor");
        if (targetStatus is not ("Approved" or "Retired")) return (null, "invalid_sop_artifact_transition");
        using var db = dbFactory.CreateDbContext();
        var entity = db.SopArtifacts.SingleOrDefault(x => x.Id == artifactId && x.TenantKey == tenantKey);
        if (entity is null) return (null, "sop_artifact_not_found");
        if (targetStatus == "Approved" && entity.Status != "Submitted") return (null, "invalid_sop_artifact_transition");
        if (targetStatus == "Retired" && entity.Status != "Approved") return (null, "invalid_sop_artifact_transition");
        entity.Status = targetStatus;
        entity.EffectiveFrom = request.EffectiveFrom ?? entity.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo ?? entity.EffectiveTo;
        if (targetStatus == "Approved") { entity.ApprovedBy = request.Actor.Trim(); entity.ApprovedAt = DateTimeOffset.UtcNow; }
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public (SopArtifactAcknowledgmentDto? Acknowledgment, string? Error) AcknowledgeSopArtifact(Guid artifactId, string tenantKey, string actor, SopArtifactAcknowledgmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(actor)) return (null, "invalid_sop_artifact_actor");
        using var db = dbFactory.CreateDbContext();
        var artifact = db.SopArtifacts.AsNoTracking().SingleOrDefault(x => x.Id == artifactId && x.TenantKey == tenantKey && x.Status == "Approved");
        if (artifact is null) return (null, "sop_artifact_not_found_or_not_approved");
        var normalizedActor = actor.Trim();
        if (db.SopArtifactAcknowledgments.Any(x => x.SopArtifactId == artifactId && x.TenantKey == tenantKey && x.Actor == normalizedActor)) return (null, "sop_artifact_already_acknowledged");
        var entity = new ManufacturingSopArtifactAcknowledgmentEntity { Id = Guid.NewGuid(), SopArtifactId = artifactId, TenantKey = tenantKey, Actor = normalizedActor, Notes = request.Notes?.Trim(), AcknowledgedAt = DateTimeOffset.UtcNow };
        db.SopArtifactAcknowledgments.Add(entity);
        db.SaveChanges();
        return (new SopArtifactAcknowledgmentDto(entity.Id, entity.SopArtifactId, entity.TenantKey, entity.Actor, entity.Notes, entity.AcknowledgedAt), null);
    }

    public (BusinessSignatureDto? Signature, string? Error) CreateBusinessSignature(string tenantKey, string actor, CreateBusinessSignatureRequest request)
    {
        if (request.EntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.EntityType) || string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.Reason))
            return (null, "invalid_business_signature");
        if (request.SignatureMethod is not ("AuthenticatedSession" or "Mfa" or "Passkey")) return (null, "invalid_signature_method");
        using var db = dbFactory.CreateDbContext();
        var normalizedActor = actor.Trim();
        var normalizedType = request.EntityType.Trim();
        var normalizedAction = request.Action.Trim();
        if (db.BusinessSignatures.Any(x => x.TenantKey == tenantKey && x.EntityType == normalizedType && x.EntityId == request.EntityId && x.Action == normalizedAction && x.Actor == normalizedActor))
            return (null, "business_signature_already_exists");
        var signedAt = DateTimeOffset.UtcNow;
        var hash = Checksum($"{tenantKey}|{normalizedType}|{request.EntityId:D}|{normalizedAction}|{normalizedActor}|{signedAt:O}|{request.Reason.Trim()}");
        var entity = new ManufacturingBusinessSignatureEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, EntityType = normalizedType, EntityId = request.EntityId, Action = normalizedAction,
            Reason = request.Reason.Trim(), EvidenceReference = request.EvidenceReference?.Trim(), Actor = normalizedActor,
            SignatureMethod = request.SignatureMethod, SignatureHash = hash, SignedAt = signedAt
        };
        db.BusinessSignatures.Add(entity);
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<BusinessSignatureDto> GetBusinessSignatures(string tenantKey, string? entityType, Guid? entityId, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.BusinessSignatures.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(x => x.EntityType == entityType);
        if (entityId.HasValue) query = query.Where(x => x.EntityId == entityId.Value);
        return query.OrderByDescending(x => x.SignedAt).Take(Math.Clamp(limit, 1, 200)).Select(ToDto).ToList();
    }

    private static string Checksum(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static SopArtifactDto ToDto(ManufacturingSopArtifactEntity x) => new(x.Id, x.TenantKey, x.ArtifactKey, x.Title, x.Version, x.Content, x.ContentType, x.Status, x.Checksum, x.EffectiveFrom, x.EffectiveTo, x.ApprovedBy, x.ApprovedAt, x.CreatedBy, x.CreatedAt);
    private static BusinessSignatureDto ToDto(ManufacturingBusinessSignatureEntity x) => new(x.Id, x.TenantKey, x.EntityType, x.EntityId, x.Action, x.Reason, x.EvidenceReference, x.Actor, x.SignatureMethod, x.SignatureHash, x.SignedAt);
}

public sealed class ManufacturingSopArtifactEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string ArtifactKey { get; set; } = "";
    public string Title { get; set; } = "";
    public int Version { get; set; }
    public string Content { get; set; } = "";
    public string ContentType { get; set; } = "text/markdown";
    public string Status { get; set; } = "Draft";
    public string Checksum { get; set; } = "";
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingBusinessSignatureEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? EvidenceReference { get; set; }
    public string Actor { get; set; } = "";
    public string SignatureMethod { get; set; } = "AuthenticatedSession";
    public string SignatureHash { get; set; } = "";
    public DateTimeOffset SignedAt { get; set; }
}

public sealed class ManufacturingSopArtifactAcknowledgmentEntity
{
    public Guid Id { get; set; }
    public Guid SopArtifactId { get; set; }
    public string TenantKey { get; set; } = "";
    public string Actor { get; set; } = "";
    public string? Notes { get; set; }
    public DateTimeOffset AcknowledgedAt { get; set; }
}
