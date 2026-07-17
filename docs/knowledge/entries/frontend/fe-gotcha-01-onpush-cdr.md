---
id: fe-gotcha-01
type: gotcha
domain: frontend
tags: [onpush, change-detection, angular, performance]
severity: warning
agent: @angular
author: @architect
date: 2026-07-17
related: []
---

# OnPush component không tự cập nhật nếu thiếu markForCheck()

## Vấn đề
Khi dùng `ChangeDetectionStrategy.OnPush`, Angular chỉ re-render component khi:
- Input reference thay đổi
- Event từ trong component
- Observable pipe với async
- **HOẶC** gọi `ChangeDetectorRef.markForCheck()` thủ công

Nếu quên gọi `markForCheck()` sau khi data thay đổi từ bên ngoài (service, store, subscription), UI sẽ không cập nhật.

## Hậu quả
- UI hiển thị dữ liệu cũ/stale
- Người dùng thấy thông tin sai
- Khó debug vì không có error

## Cách phát hiện
- Kiểm tra component có `OnPush` nhưng không có `markForCheck()` trong subscription callbacks
- Dấu hiệu: data load về từ API nhưng UI không hiển thị

## Cách làm đúng
```typescript
// ✅ ĐÚNG: gọi markForCheck() sau khi data thay đổi
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `...`,
})
export class PatientListComponent implements OnInit {
  patients: Patient[] = [];

  constructor(
    private patientService: PatientService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.patientService.search().subscribe(data => {
      this.patients = data;
      this.cdr.markForCheck();  // ← BẮT BUỘC với OnPush
    });
  }
}

// ❌ SAI: thiếu markForCheck() — UI không cập nhật
this.patientService.search().subscribe(data => {
  this.patients = data;
  // Thiếu: this.cdr.markForCheck();
});

// ✅ ALTERNATIVE: dùng async pipe (tự động markForCheck)
patients$ = this.patientService.search();
// Trong template: *ngFor="let p of patients$ | async"
```

## Đã xảy ra ở đâu
- PatientListComponent: load xong nhưng table trống (tháng 6/2026)
- ClinicalNotesComponent: note mới lưu không hiện ngay (tháng 7/2026)
