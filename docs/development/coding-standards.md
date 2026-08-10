# Tiêu chuẩn Coding — His.Hope

> Quy ước code cho toàn bộ dự án, áp dụng cho .NET backend và Angular frontend

---

## 1. .NET — Clean Architecture

### 1.1 Cấu trúc 4-layer

```
┌──────────────────────────────────────────────────────────────┐
│ API Layer            │ Presentation, REST/gRPC endpoints     │
│ Phụ thuộc: Application                                      │
├──────────────────────────────────────────────────────────────┤
│ Application Layer    │ Orchestration, CQRS, DTOs, Validation │
│ Phụ thuộc: Domain                                           │
├──────────────────────────────────────────────────────────────┤
│ Domain Layer (Pure)  │ Aggregates, Entities, ValueObjects,   │
│                      │ Domain Events, Repository Interfaces  │
│ Phụ thuộc: None                                             │
├──────────────────────────────────────────────────────────────┤
│ Infrastructure Layer │ EF Core, Repos, gRPC Clients, Outbox  │
│ Phụ thuộc: Domain + Application                             │
└──────────────────────────────────────────────────────────────┘
```

**Đường dẫn thực tế trong project** (PatientService):

- `src/Services/PatientService/PatientService.Domain/` — Aggregates, Entities, ValueObjects, Events, Repository interfaces
- `src/Services/PatientService/PatientService.Application/` — Commands, Queries, DTOs, Validators, Behaviours
- `src/Services/PatientService/PatientService.Infrastructure/` — DbContext, Repository implementations, DI
- `src/Services/PatientService/PatientService.Api/` — Program.cs, Endpoints, gRPC Services, Middleware

### 1.2 Dependency Rule

Domain is pure — không reference bất kỳ external library nào ngoài SharedKernel:

```csharp
// ✅ ĐÚNG: Domain không có dependency ngoài SharedKernel
// PatientService.Domain.csproj — chỉ reference:
//   - His.Hope.SharedKernel (shared value objects, base classes)

// ✅ ĐÚNG: Application chỉ reference Domain
// PatientService.Application.csproj — reference:
//   - PatientService.Domain
//   - MediatR
//   - FluentValidation
//   - AutoMapper

// ✅ ĐÚNG: Infrastructure reference Domain + Application
// PatientService.Infrastructure.csproj — reference:
//   - PatientService.Domain
//   - PatientService.Application
//   - Microsoft.EntityFrameworkCore
//   - Npgsql.EntityFrameworkCore.PostgreSQL

// ❌ SAI: Domain reference Infrastructure
// ❌ SAI: Application reference Infrastructure
// ❌ SAI: Domain reference EF Core
```

### 1.3 Domain Layer — Aggregate Root Example

```csharp
// src/Services/PatientService/PatientService.Domain/Aggregates/Patient.cs
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Events;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;

namespace His.Hope.PatientService.Domain.Aggregates;

public class Patient : AggregateRoot<PatientId>
{
    public PersonName Name { get; private set; }
    public DateTime DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public ContactInfo ContactInfo { get; private set; }
    public Address Address { get; private set; }
    public BloodType? BloodType { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Allergy> _allergies = [];
    public IReadOnlyCollection<Allergy> Allergies => _allergies.AsReadOnly();

    // Factory method — luôn bắt đầu bằng validation
    public static Patient Register(
        PersonName name,
        DateTime dateOfBirth,
        Gender gender,
        ContactInfo contactInfo,
        Address address)
    {
        Guard.Against.Null(name, nameof(name));
        Guard.Against.Null(gender, nameof(gender));
        Guard.Against.Null(contactInfo, nameof(contactInfo));
        Guard.Against.Null(address, nameof(address));

        var id = PatientId.New();
        var age = CalculateAge(dateOfBirth);
        Guard.Against.BusinessRule(new PatientMustBeAtLeastZeroYearsOld(age));

        return new Patient(id, name, dateOfBirth, gender, contactInfo, address);
    }

    // Public methods với private setter — encapsulation
    public void UpdatePersonalInfo(
        PersonName name, DateTime? dateOfBirth, Gender? gender,
        ContactInfo? contactInfo, Address? address)
    {
        Name = Guard.Against.Null(name, nameof(name));
        ContactInfo = Guard.Against.Null(contactInfo, nameof(contactInfo));
        Address = Guard.Against.Null(address, nameof(address));

        if (dateOfBirth.HasValue)
        {
            Guard.Against.BusinessRule(new PatientMustBeAtLeastZeroYearsOld(CalculateAge(dateOfBirth.Value)));
            DateOfBirth = dateOfBirth.Value;
        }

        if (gender is not null)
            Gender = gender;

        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new PatientUpdatedDomainEvent(Id.Value, name.FullName, contactInfo.Phone));
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new PatientDeactivatedDomainEvent(Id.Value));
    }

    private Patient() { }  // EF Core cần
}
```

### 1.4 Repository Interface (Domain Layer)

```csharp
// src/Services/PatientService/PatientService.Domain/Repositories/IPatientRepository.cs
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Repositories;

public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetByIdAsync(PatientId id, CancellationToken cancellationToken = default);
    Task<Patient?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Patient>> GetActivePatientsAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Patient> Items, int TotalCount)> SearchAsync(
        string searchTerm, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Patient> AddAsync(Patient patient, CancellationToken cancellationToken = default);
    Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(PatientId id, CancellationToken cancellationToken = default);
}
```

---

## 2. .NET — CQRS với MediatR

### 2.1 Command (Write)

Command luôn trả về kết quả — có thể là `Result<T>` hoặc chính entity DTO:

```csharp
// src/Services/PatientService/PatientService.Application/UseCases/Patients/Commands/CreatePatientCommand.cs
using His.Hope.PatientService.Application.DTOs;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Commands;

public record CreatePatientCommand(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateTime DateOfBirth,
    string GenderCode,
    string Phone,
    string? Email,
    string Street,
    string District,
    string City,
    string Province,
    string? PostalCode,
    string Country,
    string? InsuranceId,
    string? NationalId) : IRequest<PatientDto>;
```

### 2.2 Command Handler

```csharp
// src/Services/PatientService/PatientService.Application/UseCases/Patients/Commands/CreatePatientCommandHandler.cs
using AutoMapper;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Commands;

public class CreatePatientCommandHandler : IRequestHandler<CreatePatientCommand, PatientDto>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMapper _mapper;

    public CreatePatientCommandHandler(IPatientRepository patientRepository, IMapper mapper)
    {
        _patientRepository = patientRepository;
        _mapper = mapper;
    }

    public async Task<PatientDto> Handle(CreatePatientCommand request,
        CancellationToken cancellationToken)
    {
        var name = new PersonName(request.FirstName, request.LastName, request.MiddleName);
        var gender = Gender.FromCode(request.GenderCode);
        var contactInfo = new ContactInfo(request.Phone, request.Email);
        var address = new Address(request.Street,
            string.IsNullOrWhiteSpace(request.District) ? "-" : request.District!,
            request.City, request.Province,
            request.PostalCode ?? string.Empty, request.Country);

        var patient = Patient.Register(name, request.DateOfBirth, gender, contactInfo, address);

        if (!string.IsNullOrEmpty(request.InsuranceId))
            patient.UpdateInsurance(request.InsuranceId);
        if (!string.IsNullOrEmpty(request.NationalId))
            patient.UpdateNationalId(request.NationalId);

        await _patientRepository.AddAsync(patient, cancellationToken);
        await _patientRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<PatientDto>(patient);
    }
}
```

### 2.3 Query (Read)

Query trả về DTO trực tiếp:

```csharp
// src/Services/PatientService/PatientService.Application/UseCases/Patients/Queries/GetPatientByIdQuery.cs
using His.Hope.PatientService.Application.DTOs;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Queries;

public record GetPatientByIdQuery(Guid Id) : IRequest<PatientDto?>;

public class GetPatientByIdQueryHandler : IRequestHandler<GetPatientByIdQuery, PatientDto?>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMapper _mapper;

    public GetPatientByIdQueryHandler(IPatientRepository repository, IMapper mapper)
    {
        _patientRepository = repository;
        _mapper = mapper;
    }

    public async Task<PatientDto?> Handle(GetPatientByIdQuery request,
        CancellationToken cancellationToken)
    {
        var patient = await _patientRepository.GetByIdAsync(
            new PatientId(request.Id), cancellationToken);
        return patient is null ? null : _mapper.Map<PatientDto>(patient);
    }
}
```

---

## 3. .NET — Validation với FluentValidation

```csharp
// src/Services/PatientService/PatientService.Application/UseCases/Patients/Commands/CreatePatientCommandValidator.cs
using FluentValidation;

namespace His.Hope.PatientService.Application.UseCases.Patients.Commands;

public class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(v => v.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(v => v.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(v => v.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .LessThan(DateTime.Today).WithMessage("Date of birth must be in the past.");

        RuleFor(v => v.GenderCode)
            .NotEmpty().WithMessage("Gender is required.")
            .Must(g => new[] { "M", "F", "O", "U" }.Contains(g))
            .WithMessage("Invalid gender code. Must be M, F, O, or U.");

        RuleFor(v => v.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[0-9\s\-\(\)]{7,20}$").WithMessage("Invalid phone number format.");

        RuleFor(v => v.Email)
            .EmailAddress().When(v => !string.IsNullOrEmpty(v.Email))
            .WithMessage("Invalid email format.");
    }
}
```

Validation được tự động kích hoạt qua MediatR Pipeline Behavior:

```csharp
// src/Services/PatientService/PatientService.Application/Common/Behaviours/ValidationBehaviour.cs
using FluentValidation;
using MediatR;

namespace His.Hope.PatientService.Application.Common.Behaviours;

public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = (await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count != 0)
                throw new ValidationException(failures);
        }
        return await next();
    }
}
```

---

## 4. .NET — API Layer

### 4.1 Minimal API Endpoints với Permission Check

```csharp
// src/Services/PatientService/PatientService.Api/Program.cs (trích đoạn)
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

var patients = app.MapGroup("/api/v1/patients").RequireAuthorization();

// Query endpoint — chỉ .view permission
patients.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var patient = await mediator.Send(new GetPatientByIdQuery(id), ct);
    return patient is null ? Results.NotFound() : Results.Ok(patient);
}).RequireAuthorization("Permission:patients.view");

// Command endpoint — .create permission
patients.MapPost("/", async (CreatePatientRequest request, IMediator mediator, CancellationToken ct) =>
{
    var command = new CreatePatientCommand(
        request.FirstName, request.LastName, request.MiddleName,
        request.DateOfBirth, request.GenderCode,
        request.Phone, request.Email,
        request.Street, request.District, request.City,
        request.Province, request.PostalCode, request.Country,
        request.InsuranceId, request.NationalId);

    var patient = await mediator.Send(command, ct);
    return Results.Created($"/api/v1/patients/{patient.Id}", patient);
}).RequireAuthorization("Permission:patients.create");

// Deactivate — .delete permission
patients.MapPatch("/{id:guid}/deactivate", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    await mediator.Send(new DeactivatePatientCommand(id), ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:patients.delete");
```

**Nguyên tắc**:
- Không có business logic trong endpoint. Endpoint chỉ parse request → gọi MediatR → map response.
- Mọi endpoint group đều có `.RequireAuthorization()`.
- Mỗi endpoint có permission cụ thể: `RequireAuthorization("Permission:patients.view")`.

### 4.2 Exception Handling Middleware

```csharp
// src/Services/PatientService/PatientService.Api/Middleware/ExceptionHandlingMiddleware.cs
using System.Net;
using System.Text.Json;
using FluentValidation;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.PatientService.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex) { await HandleExceptionAsync(context, ex); }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var (statusCode, response) = exception switch
        {
            ValidationException ex => (HttpStatusCode.BadRequest,
                (object)new { error = "Validation failed", details = ex.Errors }),
            DomainException ex => (HttpStatusCode.UnprocessableEntity,
                new { error = ex.Message }),
            NotFoundException ex => (HttpStatusCode.NotFound,
                new { error = ex.Message }),
            _ => (HttpStatusCode.InternalServerError,
                new { error = exception.Message })
        };

        _logger.LogError(exception, "Request failed with {StatusCode}", statusCode);
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

---

## 5. .NET — Naming Conventions

| Thành phần | Convention | Ví dụ |
|------------|-----------|-------|
| Namespace | PascalCase, match folder structure | `His.Hope.PatientService.Domain.Aggregates` |
| Class / Record | PascalCase | `Patient`, `CreatePatientCommand` |
| Interface | PascalCase, prefix `I` | `IPatientRepository` |
| Public method | PascalCase | `Register()`, `GetByIdAsync()` |
| Private field | camelCase, `_` prefix | `_patientRepository`, `_mapper` |
| Method parameter | camelCase | `dateOfBirth`, `cancellationToken` |
| Local variable | camelCase | `var patient = ...` |
| Constant | PascalCase | `MaxPageSize` |
| Async method | Suffix `Async` | `GetByIdAsync()`, `SaveChangesAsync()` |

### Async all the way down

```csharp
// ✅ ĐÚNG
public async Task<PatientDto> Handle(CreatePatientCommand request, CancellationToken ct)
{
    var patient = Patient.Register(...);
    await _patientRepository.AddAsync(patient, ct);
    await _unitOfWork.SaveChangesAsync(ct);
    return _mapper.Map<PatientDto>(patient);
}

// ❌ SAI — không dùng .Result hoặc .Wait()
var patient = _patientRepository.GetByIdAsync(id).Result;  // DEADLOCK RISK
```

---

## 6. EF Core & Database

### 6.1 DbContext

```csharp
// src/Services/PatientService/PatientService.Infrastructure/Persistence/PatientDbContext.cs
using His.Hope.Infrastructure.Outbox;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.PatientService.Infrastructure.Persistence;

public class PatientDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public PatientDbContext(DbContextOptions<PatientDbContext> options, IMediator mediator)
        : base(options) => _mediator = mediator;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Dispatch domain events sau khi save
        var domainEvents = ChangeTracker.Entries<AggregateRoot<PatientId>>()
            .Select(e => e.Entity.DomainEvents)
            .SelectMany(e => e)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
            await _mediator.Publish(domainEvent, cancellationToken);

        return result;
    }
}
```

### 6.2 DI Registration với Retry Strategy

```csharp
// src/Services/PatientService/PatientService.Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddPatientInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PatientDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("PatientDb"),
                b =>
                {
                    b.MigrationsAssembly(typeof(PatientDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .AddInterceptors(new OutboxDomainEventInterceptor()));

        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<DomainEventDispatcher>();

        return services;
    }
}
```

---

## 7. Angular Conventions

### 7.1 Standalone Components & OnPush Detection

Tất cả component mới nên dùng standalone nếu có thể, với `ChangeDetectionStrategy.OnPush`:

```typescript
// src/Frontend/his-hope-app/src/app/features/patients/patient-list/patient-list.component.ts
import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { PatientService } from '@core/services/patient.service';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';

@Component({
  selector: 'app-patient-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="patient-list">
      <div class="header">
        <h1>Patients</h1>
        <button mat-raised-button color="primary" routerLink="/patients/new">
          <mat-icon>add</mat-icon> New Patient
        </button>
      </div>
      <mat-table [dataSource]="patients" class="mat-elevation-z2">
        <ng-container matColumnDef="fullName">
          <mat-header-cell *matHeaderCellDef>Name</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.fullName }}</mat-cell>
        </ng-container>
        <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
        <mat-row *matRowDef="let row; columns: displayedColumns;" (click)="viewPatient(row.id)"></mat-row>
      </mat-table>
    </div>
  `,
})
export class PatientListComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  patients: Patient[] = [];
  searchControl = new FormControl('');
  displayedColumns = ['fullName', 'genderName', 'dateOfBirth', 'age', 'phone', 'actions'];

  constructor(
    private patientService: PatientService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe((term) => {
        this.loadPatients();
        this.cdr.markForCheck();  // Bắt buộc với OnPush
      });
    this.loadPatients();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
```

### 7.2 Smart/Dumb Component Pattern

```
features/patients/
├── patient-list/          # Dumb: chỉ hiển thị, nhận input/output
├── patient-form/          # Dumb: chỉ form, emit submit
├── patient-workspace/     # Smart: orchestrator, call services, manage state
└── patient-detail/        # Dumb: hiển thị chi tiết
```

```typescript
// Smart component (container)
@Component({
  template: `
    <app-patient-list
      [patients]="patients$ | async"
      (patientSelect)="onSelect($event)">
    </app-patient-list>
  `,
})
export class PatientWorkspaceComponent {
  patients$ = this.store.select(selectAllPatients);

  constructor(private store: Store) {}

  onSelect(id: string): void {
    this.store.dispatch(loadPatient({ id }));
  }
}

// Dumb component (presentational)
@Component({
  selector: 'app-patient-list',
  inputs: ['patients'],
  outputs: ['patientSelect'],
  template: `...`,
})
export class PatientListComponent {
  patients: Patient[] = [];
  patientSelect = new EventEmitter<string>();
}
```

### 7.3 NgRx State Management

```
store/patients/
├── patients.actions.ts    # Action definitions
├── patients.reducer.ts    # State transitions
├── patients.effects.ts    # Side effects (API calls)
└── patients.selectors.ts  # Memoized selectors
```

**Action**:

```typescript
// src/Frontend/his-hope-app/src/app/store/patients/patients.actions.ts
import { createAction, props } from '@ngrx/store';
import { Patient, PagedResult } from '@core/models';

export const loadPatients = createAction('[Patient List] Load', props<{ searchTerm: string; page: number }>());
export const loadPatientsSuccess = createAction('[Patient API] Load Success', props<{ result: PagedResult<Patient> }>());
export const loadPatientsFailure = createAction('[Patient API] Load Failure', props<{ error: string }>());
```

**Effect**:

```typescript
// src/Frontend/his-hope-app/src/app/store/patients/patients.effects.ts
import { Injectable } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, switchMap } from 'rxjs/operators';
import { of } from 'rxjs';
import { PatientService } from '@core/services/patient.service';
import * as PatientActions from './patients.actions';

@Injectable()
export class PatientsEffects {
  loadPatients$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PatientActions.loadPatients),
      switchMap(({ searchTerm, page }) =>
        this.patientService.search(searchTerm, page).pipe(
          map((result) => PatientActions.loadPatientsSuccess({ result })),
          catchError((error) => of(PatientActions.loadPatientsFailure({ error: error.message }))),
        ),
      ),
    ),
  );

  constructor(private actions$: Actions, private patientService: PatientService) {}
}
```

### 7.4 Permission Guard

```typescript
// src/Frontend/his-hope-app/src/app/core/guards/permission.guard.ts
import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, Router, UrlTree } from '@angular/router';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';
import { AuthService } from '@core/services/auth.service';

@Injectable({ providedIn: 'root' })
export class PermissionGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(route: ActivatedRouteSnapshot): Observable<boolean | UrlTree> {
    const required: string[] = route.data?.['permissions'] ?? [];

    if (required.length === 0) {
      return this.authService.isLoggedIn().pipe(
        map((loggedIn) => loggedIn ? true : this.router.parseUrl('/auth/login')),
      );
    }

    return this.authService.currentUser$.pipe(
      take(1),
      map((user) => {
        if (!user) return this.router.parseUrl('/auth/login');
        const userPerms = this.authService.getUserPermissions();
        const hasAll = required.every((p) => userPerms.includes(p));
        return hasAll ? true : this.router.parseUrl('/access-denied');
      }),
    );
  }
}
```

### 7.5 Lazy Loading

```typescript
// src/Frontend/his-hope-app/src/app/app-routing.module.ts
const routes: Routes = [
  {
    path: 'patients',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['patients.view'] },
    loadChildren: () =>
      import('@features/patients/patients.module').then((m) => m.PatientsModule),
  },
  {
    path: 'clinical',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['clinical.view'] },
    loadChildren: () =>
      import('@features/clinical/clinical.module').then((m) => m.ClinicalModule),
  },
  // Tất cả feature modules đều lazy load
];
```

### 7.6 Material Design với Custom Theme

```scss
/* src/Frontend/his-hope-app/src/styles/_theme.scss */
@use '@angular/material' as mat;

$his-hope-primary: mat.define-palette(mat.$indigo-palette, 700, 500, 900);
$his-hope-accent: mat.define-palette(mat.$teal-palette, 600);
$his-hope-warn: mat.define-palette(mat.$red-palette);
$his-hope-typography: mat.define-typography-config(
  $font-family: 'Inter, Roboto, sans-serif',
);

$his-hope-theme: mat.define-light-theme((
  color: (primary: $his-hope-primary, accent: $his-hope-accent, warn: $his-hope-warn),
  typography: $his-hope-typography,
));

@include mat.all-component-themes($his-hope-theme);
```

---

## 8. Database Conventions

### 8.1 Schema Rules

| Quy tắc | Ví dụ |
|---------|-------|
| UUID primary key | `PatientId UUID PRIMARY KEY DEFAULT gen_random_uuid()` |
| TIMESTAMPTZ | `CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now()` |
| Index trên FK | `INDEX idx_patients_name (LastName, FirstName)` |
| Index trên query columns | `INDEX idx_patients_active (IsActive)` |
| Migration additive | Chỉ `ALTER TABLE ... ADD COLUMN`, không `DROP` |
| Mỗi service có database riêng | `patientdb`, `identitydb`, `clinicaldb`, ... |

### 8.2 Migration Example

```sql
-- cockroach/migrations/002-patient-service.sql
CREATE TABLE patientdb.Patients (
    PatientId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    FirstName STRING(100) NOT NULL,
    LastName STRING(100) NOT NULL,
    DateOfBirth DATE NOT NULL,
    Gender STRING(10) NOT NULL,
    Phone STRING(20) NOT NULL,
    Email STRING(200),
    Street STRING(200) NOT NULL,
    City STRING(100) NOT NULL,
    Province STRING(100) NOT NULL,
    Country STRING(100) NOT NULL,
    BloodType STRING(10),
    IsActive BOOL NOT NULL DEFAULT true,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    INDEX idx_patients_name (LastName, FirstName),
    INDEX idx_patients_active (IsActive)
);

CREATE TABLE patientdb.Allergies (
    PatientId UUID NOT NULL REFERENCES patientdb.Patients(PatientId) ON DELETE CASCADE,
    AllergyId UUID NOT NULL DEFAULT gen_random_uuid(),
    Allergen STRING(200) NOT NULL,
    Reaction STRING(500),
    Severity STRING(50),
    RecordedDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    IsActive BOOL NOT NULL DEFAULT true,
    PRIMARY KEY (PatientId, AllergyId)
);
```

### 8.3 Row-Level Security (RLS)

Database queries thông qua views, không trực tiếp vào bảng:

```sql
-- cockroach/migrations/011-row-level-security.sql
CREATE VIEW patientdb.vw_Patients_ByFacility AS
SELECT p.*
FROM patientdb.Patients p
INNER JOIN identitydb.FacilityAssignments fa ON p.FacilityId = fa.FacilityId
WHERE fa.UserId = current_setting('app.current_user_id')::UUID;

-- Application query qua view
SELECT * FROM patientdb.vw_Patients_ByFacility WHERE IsActive = true;
```

---

## 9. Security Conventions

### 9.1 Permission Model

```csharp
// Mọi endpoint phải yêu cầu permission
patients.MapGet("/search", ...)
    .RequireAuthorization("Permission:patients.view");       // REST API

[Authorize]                                                   // gRPC
public class PatientGrpcServiceImpl : PatientGrpcService.PatientGrpcServiceBase { }
```

### 9.2 JWT Authentication

```csharp
// src/Services/PatientService/PatientService.Api/Program.cs
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);
// SECURITY: Token được ký bởi IdentityService với RSA private key
// Các service khác chỉ cần RSA public key để validate
```

### 9.3 Secrets Management

```hcl
# vault/policies/patient-service.hcl
path "secret/data/his-hope/patient-service/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/database/patientdb" {
  capabilities = ["read"]
}

path "secret/data/his-hope/rabbitmq" {
  capabilities = ["read"]
}
```

**Nguyên tắc**:
- KHÔNG hardcode connection string, API key, password trong code hay config file
- Secrets từ Vault, inject qua Kubernetes secrets hoặc Vault agent
- Development chỉ dùng giá trị mặc định trong `appsettings.Development.json` (không commit production secrets)

### 9.4 Circuit Breaker cho External Calls

```csharp
// Tất cả gọi external service phải qua Polly circuit breaker
services.AddResiliencePolicies();
// → His.Hope.Infrastructure.Resilience
// → Circuit breaker, retry với jitter, timeout, bulkhead
```

### 9.5 Input Validation

```csharp
// ✅ ĐÚNG: Server-side validation với FluentValidation
public class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(v => v.Phone)
            .NotEmpty()
            .Matches(@"^\+?[0-9\s\-\(\)]{7,20}$");
    }
}

// ❌ SAI: Chỉ validate client-side
// Không trust client input — luôn validate server-side
```

---

## 10. Testing Conventions

### 10.1 Test Structure — Arrange/Act/Assert

```csharp
// tests/Services/PatientService/PatientService.Domain.Tests/PatientTests.cs
[Fact]
public void Register_WithValidParameters_ShouldCreateActivePatient()
{
    // Arrange — không cần setup phức tạp (domain test là pure)
    // ↓ dùng factory method hoặc shared fixture

    // Act
    var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);

    // Assert
    patient.Should().NotBeNull();
    patient.IsActive.Should().BeTrue();
    patient.Allergies.Should().BeEmpty();
}

[Fact]
public void Register_WithNullName_ShouldThrow()
{
    // Act
    var act = () => Patient.Register(null!, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);

    // Assert
    act.Should().Throw<ArgumentNullException>()
        .WithParameterName("name");
}

[Fact]
public void Register_WithFutureDateOfBirth_ShouldThrowDomainException()
{
    // Arrange
    var futureDob = DateTime.Today.AddDays(1);

    // Act
    var act = () => Patient.Register(DefaultName, futureDob, DefaultGender, DefaultContact, DefaultAddress);

    // Assert
    act.Should().Throw<DomainException>()
        .WithMessage("Patient age cannot be negative.");
}
```

### 10.2 Test Naming Convention

```
MethodName_Scenario_ExpectedResult
```

| Ví dụ | Mô tả |
|-------|-------|
| `Register_WithValidParameters_ShouldCreateActivePatient` | Happy path |
| `Register_WithNullName_ShouldThrow` | Error case |
| `Register_WithFutureDateOfBirth_ShouldThrowDomainException` | Edge case |
| `Validate_WithBorderlineFirstNameLength_ShouldNotHaveError` | Boundary |
| `UpdatePersonalInfo_WithAllNullOptionalFields_ShouldPreserveDefaults` | Edge case |

### 10.3 One Assertion Per Test (preferred)

```csharp
// ✅ TỐT: Mỗi test một assertion logic
[Fact]
public void Register_WithValidParameters_ShouldBeActive()
{
    var patient = Patient.Register(...);
    patient.IsActive.Should().BeTrue();
}

[Fact]
public void Register_WithValidParameters_ShouldRaiseDomainEvent()
{
    var patient = Patient.Register(...);
    patient.DomainEvents.Should().ContainSingle()
        .Which.Should().BeOfType<PatientRegisteredDomainEvent>();
}

// ⚠️ Có thể chấp nhận nếu assertions liên quan chặt chẽ
[Fact]
public void Register_WithValidParameters_ShouldCreateActivePatient()
{
    var patient = Patient.Register(...);
    patient.Should().NotBeNull();
    patient.IsActive.Should().BeTrue();
    patient.Allergies.Should().BeEmpty();  // Cùng kiểm tra initial state
}
```

### 10.4 Test Data Builders (tránh hardcode)

```csharp
// Sử dụng helper method thay vì copy-paste hardcoded values
private static readonly PersonName DefaultName = new("John", "Doe", "M");
private static readonly DateTime DefaultDob = new(1990, 1, 15);

private Patient CreateDefaultPatient()
{
    return Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);
}

// Sử dụng FluentValidation.TestHelper cho validator test
[Theory]
[InlineData("X")]
[InlineData("INVALID")]
public void Validate_WithInvalidGenderCode_ShouldHaveError(string invalidCode)
{
    var command = CreateValidCommand() with { GenderCode = invalidCode };
    var result = _validator.TestValidate(command);
    result.ShouldHaveValidationErrorFor(c => c.GenderCode);
}
```
