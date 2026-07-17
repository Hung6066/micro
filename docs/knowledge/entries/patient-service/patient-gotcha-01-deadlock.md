---
id: patient-gotcha-01
type: gotcha
domain: patient-service
tags: [deadlock, async, ef-core, dotnet]
severity: critical
agent: @dotnet
author: @architect
date: 2026-07-17
related: []
---

# Không dùng .Result() hoặc .Wait() với async

## Vấn đề
Gọi `.Result` hoặc `.Wait()` trên Task trong ASP.NET Core gây deadlock do SynchronizationContext bị block. Đã xảy ra 2 lần trong PatientService và AppointmentService.

## Hậu quả
- Request treo vĩnh viễn, thread pool cạn kiệt
- Service cần restart để phục hồi
- Không có exception rõ ràng — chỉ thấy timeout

## Cách phát hiện
```bash
rg "\.Result|\.Wait\(\)" src/Services/ --include="*.cs"
```

## Cách làm đúng
```csharp
// ✅ ĐÚNG: async all the way down
public async Task<PatientDto> Handle(CreatePatientCommand request, CancellationToken ct)
{
    var patient = Patient.Register(...);
    await _patientRepository.AddAsync(patient, ct);
    await _unitOfWork.SaveChangesAsync(ct);
    return _mapper.Map<PatientDto>(patient);
}

// ❌ SAI: block async — DEADLOCK RISK
var patient = _patientRepository.GetByIdAsync(id).Result;
var patient = _patientRepository.GetByIdAsync(id).Wait();

// ✅ Nếu bắt buộc sync (rất hiếm): dùng GetAwaiter().GetResult()
var patient = _patientRepository.GetByIdAsync(id).GetAwaiter().GetResult();
```

## Đã xảy ra ở đâu
- PatientService: `PatientRepository.GetByIdAsync` bị gọi .Result trong controller (đã fix tháng 3/2026)
- AppointmentService: `BookingHandler` (đã fix tháng 5/2026)
