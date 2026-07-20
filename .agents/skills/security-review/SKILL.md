---
name: security-review
description: Comprehensive security review for His.Hope. Use when adding auth, handling patient data, creating API endpoints, or working with secrets. HIPAA-safe patterns.
metadata:
  origin: ECC (imported)
---

# Security Review Skill

Comprehensive security review for His.Hope hospital information system. Ensures HIPAA compliance, patient data protection, and OWASP Top 10 coverage.

## When to Activate

- Implementing authentication or authorization
- Handling patient data (PII/PHI)
- Creating new API endpoints
- Working with secrets or credentials
- Implementing payment/billing features
- Storing or transmitting sensitive data
- Integrating third-party APIs
- Adding audit logging
- Before ANY production deployment

## Security Checklist

### 1. Secrets Management

#### FAIL: NEVER Do This
```csharp
var apiKey = "sk-proj-xxxxx";  // Hardcoded secret
var dbPassword = "password123"; // In source code
var connectionString = "Server=...;User Id=sa;Password=P@ssw0rd"; // Hardcoded
```

#### PASS: ALWAYS Use Vault
```csharp
// ALWAYS use Vault for secrets
var apiKey = vaultService.GetSecret("external/api-key");
var dbConnection = vaultService.GetSecret("database/connection-string");

// Verify secrets exist
if (string.IsNullOrEmpty(apiKey))
    throw new InvalidOperationException("API key not configured in Vault");
```

#### Verification Steps
- [ ] No hardcoded API keys, tokens, or passwords
- [ ] All secrets in Vault
- [ ] `appsettings.*.local.json` in .gitignore
- [ ] No secrets in git history
- [ ] Vault policies enforce least privilege

### 2. Input Validation

#### Always Validate Input
```csharp
public class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(x => x.Email).EmailAddress();
        RuleFor(x => x.MRN).NotEmpty().Length(8, 12);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DateOfBirth).LessThan(DateTime.UtcNow);
    }
}
```

#### Verification Steps
- [ ] All API inputs validated with FluentValidation
- [ ] File uploads restricted (size, type, extension)
- [ ] No direct use of user input in SQL queries
- [ ] Whitelist validation (not blacklist)
- [ ] Error messages don't leak PII

### 3. SQL Injection Prevention

#### FAIL: NEVER Concatenate SQL
```csharp
// DANGEROUS — SQL Injection vulnerability
var query = $"SELECT * FROM Patients WHERE MRN = '{mrn}'";
await context.Database.ExecuteSqlRawAsync(query);
```

#### PASS: ALWAYS Use Parameterized Queries
```csharp
// Safe — EF Core parameterized query
var patient = await context.Patients
    .FirstOrDefaultAsync(p => p.Mrn == mrn);

// Safe — raw SQL with parameters
await context.Database.ExecuteSqlInterpolatedAsync(
    $"UPDATE Patients SET Status = {status} WHERE MRN = {mrn}");
```

#### Verification Steps
- [ ] All database queries use parameterized queries or EF Core
- [ ] No string concatenation in SQL
- [ ] EF Core used correctly (no raw SQL without parameters)

### 4. Authentication & Authorization

#### JWT Token Handling
```csharp
// CORRECT: JWT via httpOnly cookies
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
```

#### Authorization Checks
```csharp
[Authorize(Roles = "Practitioner")]
[HttpPost("patients/{id}/medications")]
public async Task<IActionResult> OrderMedication(
    Guid id, [FromBody] OrderMedicationCommand command)
{
    // Verify practitioner has rights for this patient
    var practitionerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    if (!await authorizationService.HasAccessToPatient(practitionerId, id))
        return Forbid();
    
    return Ok(await mediator.Send(command));
}
```

#### Verification Steps
- [ ] Tokens stored in httpOnly cookies (not localStorage)
- [ ] Authorization checks before ALL sensitive operations
- [ ] Row-level security for patient data
- [ ] Role-based access control (Practitioner, Nurse, Admin)
- [ ] Audit logging for ALL data access

### 5. HIPAA Compliance

#### Patient Data Protection
```csharp
// ALWAYS redact PII in logs
public class PiiRedactionMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;
        
        // Redact SSN, MRN, DOB, Patient names from logs
        var redacted = piiRedactionService.Redact(logEntry);
        logger.LogInformation(redacted);
    }
}
```

#### Audit Logging
```csharp
// ALWAYS log patient data access
public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var response = await next();
        
        _auditLogger.Log(new AuditEntry
        {
            UserId = _currentUser.Id,
            Action = typeof(TRequest).Name,
            Timestamp = DateTime.UtcNow,
            PatientId = ExtractPatientId(request),
            ResourceType = "PatientRecord"
        });
        
        return response;
    }
}
```

#### Verification Steps
- [ ] All PII redacted in logs (SSN, MRN, DOB, names, addresses)
- [ ] Audit logging for all patient data access
- [ ] Encryption at rest for patient data
- [ ] Encryption in transit (mTLS via Linkerd)
- [ ] Data access logged per-patient

### 6. XSS Prevention

```csharp
// Angular's built-in sanitization handles most XSS
// But NEVER use bypassSecurityTrustHtml on user content

// CORRECT: Use Angular's DomSanitizer
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

export class PatientNotesComponent {
  safeNotes: SafeHtml;
  
  constructor(private sanitizer: DomSanitizer) {
    // Sanitize user-provided content
    this.safeNotes = this.sanitizer.sanitize(
      SecurityContext.HTML, 
      this.rawNotes
    );
  }
}
```

#### Verification Steps
- [ ] User-provided HTML sanitized (if allowed)
- [ ] CSP headers configured in API gateway
- [ ] Angular's built-in XSS protection used
- [ ] No `bypassSecurityTrust` calls on user content

### 7. Rate Limiting

```csharp
// Rate limiting on all API endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Api", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
    });
    
    // Stricter for auth endpoints
    options.AddFixedWindowLimiter("Auth", config =>
    {
        config.PermitLimit = 10;
        config.Window = TimeSpan.FromMinutes(1);
    });
});
```

#### Verification Steps
- [ ] Rate limiting on ALL API endpoints
- [ ] Stricter limits on authentication endpoints
- [ ] Patient search endpoints have rate limits
- [ ] IP-based + user-based rate limiting

### 8. Sensitive Data Exposure

#### Logging
```csharp
// FAIL: WRONG — Logging PII
logger.LogInformation("Patient login: {Email}, {MRN}", email, mrn);

// PASS: CORRECT — Redact sensitive data
logger.LogInformation("Patient login: {UserId}", userId);
```

#### API Responses
```csharp
// FAIL: WRONG — Exposing SSN
public record PatientResponse(string Name, string SSN, string Mrn);

// PASS: CORRECT — Never expose SSN
public record PatientResponse(
    Guid Id, 
    string Name, 
    string Mrn, 
    string DateOfBirth  // Only last 4 digits of SSN if needed
);
```

#### Verification Steps
- [ ] SSN never exposed in API responses
- [ ] No PII in logs
- [ ] Error messages generic for users; detailed only in server logs
- [ ] No stack traces exposed to API consumers
- [ ] Patient data never in query strings

### 9. Dependency Security

```bash
# Check for vulnerabilities
dotnet list package --vulnerable

# Update dependencies
dotnet update package

# Check for outdated packages
dotnet list package --outdated
```

#### Verification Steps
- [ ] Dependencies up to date
- [ ] No known vulnerabilities
- [ ] Lock files committed
- [ ] Dependabot enabled on GitHub
- [ ] Regular security updates scheduled

## Security Testing

### Automated Security Tests
```csharp
[Fact]
public async Task Unauthenticated_Request_Returns_401()
{
    var response = await _client.GetAsync("/api/patients");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task Unauthorized_User_Cannot_Access_Other_Patient_Data()
{
    // Arrange
    var token = _tokenService.GetToken("user1");
    _client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", token);
    
    // Act — try to access user2's patient data
    var response = await _client.GetAsync($"/api/patients/{_otherPatientId}");
    
    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

## Pre-Deployment Security Checklist

Before ANY production deployment:

- [ ] **Secrets**: No hardcoded secrets, all in Vault
- [ ] **Input Validation**: All inputs validated via FluentValidation
- [ ] **SQL Injection**: All queries parameterized / EF Core
- [ ] **HIPAA**: PII redacted in logs, audit logging enabled
- [ ] **XSS**: User content sanitized (Angular handles this)
- [ ] **CSRF**: Anti-forgery tokens on state-changing operations
- [ ] **Authentication**: JWT properly configured, tokens in cookies
- [ ] **Authorization**: Role checks on ALL protected endpoints
- [ ] **Rate Limiting**: Enabled on all endpoints
- [ ] **HTTPS**: Enforced (Linkerd mTLS)
- [ ] **Security Headers**: Configured in gateway
- [ ] **Error Handling**: No sensitive data in error responses
- [ ] **Logging**: No PII logged, audit trail for patient access
- [ ] **Dependencies**: No known vulnerabilities
- [ ] **CORS**: Properly configured for Angular SPA
- [ ] **File Uploads**: Validated (size, type) if applicable
- [ ] **CockroachDB**: Row-level security configured

## Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [HIPAA Security Rule](https://www.hhs.gov/hipaa/for-professionals/security/index.html)
- His.Hope Security docs: `docs/security/`
- His.Hope Vault setup: `vault/`
- His.Hope Network policies: `k8s/network-policies/`

---

**Remember**: In a hospital system, security failures can affect patient safety. One vulnerability can compromise the entire platform. When in doubt, err on the side of caution — and always involve @security.
