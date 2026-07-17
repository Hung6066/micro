---
id: patient-pattern-01
type: pattern
domain: patient-service
tags: [aggregate, factory-method, domain, dotnet]
severity: info
agent: @dotnet
author: @architect
date: 2026-07-17
related: []
---

# Aggregate Root với Static Factory Method

## Khi nào dùng
Mọi Aggregate Root trong Domain layer **phải** được tạo qua static factory method, không dùng constructor public. Áp dụng cho tất cả service: Patient, Clinical, Appointment, Lab, Billing, Pharmacy, Identity.

## Mẫu chuẩn
```csharp
public class Patient : AggregateRoot<PatientId>
{
    public PersonName Name { get; private set; }
    public DateTime DateOfBirth { get; private set; }
    public bool IsActive { get; private set; }

    // Constructor PRIVATE cho EF Core
    private Patient() { }

    // Factory method — luôn có validation + domain event
    public static Patient Register(
        PersonName name, DateTime dateOfBirth, Gender gender,
        ContactInfo contactInfo, Address address)
    {
        Guard.Against.Null(name, nameof(name));
        Guard.Against.Null(gender, nameof(gender));

        var id = PatientId.New();
        var age = CalculateAge(dateOfBirth);
        Guard.Against.BusinessRule(new PatientMustBeAtLeastZeroYearsOld(age));

        var patient = new Patient
        {
            Id = id,
            Name = name,
            DateOfBirth = dateOfBirth,
            Gender = gender,
            ContactInfo = contactInfo,
            Address = address,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        patient.AddDomainEvent(new PatientRegisteredDomainEvent(id.Value, name.FullName));
        return patient;
    }
}
```

## Lý do
- Đảm bảo domain invariant luôn được kiểm tra khi tạo entity
- Domain event được raise nhất quán
- Constructor private ngăn việc tạo entity không hợp lệ từ bên ngoài

## Tham khảo
- `docs/development/coding-standards.md` section 1.3
- `src/Services/PatientService/PatientService.Domain/Aggregates/Patient.cs`
