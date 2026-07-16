import { Injectable } from '@angular/core';
import { Observable, of, BehaviorSubject } from 'rxjs';
import { delay } from 'rxjs/operators';
import { LabOrder, CreateLabOrderRequest, RecordLabResultRequest, LabOrderSearchParams } from '@core/models/lab-order.model';
import { PagedResult } from '@core/models/paged-result.model';
import { mockLabOrders, mockPatients, mockUsers } from './mock-data';

@Injectable({ providedIn: 'root' })
export class MockLabService {
  private labOrdersSubject = new BehaviorSubject<LabOrder[]>([...mockLabOrders]);

  private delayMs(): number {
    return 300 + Math.floor(Math.random() * 200);
  }

  searchLabOrders(params?: LabOrderSearchParams): Observable<PagedResult<LabOrder>> {
    let filtered = this.labOrdersSubject.value;
    if (params?.searchTerm) {
      const q = params.searchTerm.toLowerCase();
      filtered = filtered.filter(
        (lab) =>
          (lab.patientName && lab.patientName.toLowerCase().includes(q)) ||
          (lab.providerName && lab.providerName.toLowerCase().includes(q)) ||
          lab.tests.some((t) => t.testName.toLowerCase().includes(q) || t.testCode.toLowerCase().includes(q)),
      );
    }
    if (params?.patientId) {
      filtered = filtered.filter((lab) => lab.patientId === params.patientId);
    }
    if (params?.statusCode) {
      filtered = filtered.filter((lab) => lab.statusCode === params.statusCode);
    }
    const page = params?.page || 1;
    const pageSize = params?.pageSize || 20;
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<LabOrder> = {
      items: filtered.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  getLabOrder(id: string): Observable<LabOrder> {
    const lab = this.labOrdersSubject.value.find((l) => l.id === id);
    return of(lab!).pipe(delay(this.delayMs()));
  }

  createLabOrder(data: CreateLabOrderRequest): Observable<LabOrder> {
    const patient = mockPatients.find((p) => p.id === data.patientId);
    const provider = mockUsers.find((u) => u.id === data.providerId);
    const now = new Date().toISOString();
    const newLabOrder: LabOrder = {
      id: `lab-${String(this.labOrdersSubject.value.length + 1).padStart(3, '0')}`,
      patientId: data.patientId,
      providerId: data.providerId,
      encounterId: data.encounterId,
      orderDate: now,
      statusCode: 'ordered',
      statusName: 'Đã yêu cầu',
      priorityCode: data.priorityCode,
      priorityName: data.priorityCode === 'urgent' ? 'Khẩn' : 'Thường quy',
      notes: data.notes,
      tests: data.tests.map((t, i) => ({
        id: `lt-${String(this.labOrdersSubject.value.length * 10 + i + 1).padStart(3, '0')}`,
        testCode: t.testCode,
        testName: t.testName,
        specimenType: t.specimenType,
        statusCode: 'ordered',
        statusName: 'Đã yêu cầu',
        orderedAt: now,
      })),
      patientName: patient?.fullName,
      providerName: provider?.fullName,
    };
    this.labOrdersSubject.next([...this.labOrdersSubject.value, newLabOrder]);
    return of(newLabOrder).pipe(delay(this.delayMs()));
  }

  submitLabOrder(id: string): Observable<void> {
    const labs = this.labOrdersSubject.value;
    const index = labs.findIndex((l) => l.id === id);
    if (index !== -1) {
      labs[index] = {
        ...labs[index],
        statusCode: 'in_progress',
        statusName: 'Đang xử lý',
      };
      this.labOrdersSubject.next([...labs]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  collectSpecimen(id: string): Observable<void> {
    const labs = this.labOrdersSubject.value;
    const index = labs.findIndex((l) => l.id === id);
    if (index !== -1) {
      const now = new Date().toISOString();
      const updatedTests = labs[index].tests.map((t) => ({
        ...t,
        statusCode: t.statusCode === 'ordered' ? 'specimen_collected' : t.statusCode,
        statusName: t.statusCode === 'ordered' ? 'Đã lấy mẫu' : t.statusName,
        collectedAt: t.statusCode === 'ordered' ? now : t.collectedAt,
      }));
      labs[index] = {
        ...labs[index],
        statusCode: 'specimen_collected',
        statusName: 'Đã lấy mẫu',
        tests: updatedTests,
      };
      this.labOrdersSubject.next([...labs]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  recordResult(id: string, data: RecordLabResultRequest): Observable<void> {
    const labs = this.labOrdersSubject.value;
    const index = labs.findIndex((l) => l.id === id);
    if (index !== -1) {
      const now = new Date().toISOString();
      const firstPendingTest = labs[index].tests.find((t) => t.statusCode !== 'completed');
      const updatedTests = labs[index].tests.map((t) => {
        if (t.statusCode !== 'completed' && t === firstPendingTest) {
          return {
            ...t,
            statusCode: 'completed',
            statusName: 'Hoàn thành',
            completedAt: now,
            result: {
              labResultId: `lr-${Date.now()}`,
              value: data.value,
              unit: data.unit,
              referenceRange: data.referenceRange,
              abnormalFlagCode: data.abnormalFlagCode,
              abnormalFlagName:
                data.abnormalFlagCode === 'normal'
                  ? 'Bình thường'
                  : data.abnormalFlagCode === 'high'
                    ? 'Cao'
                    : data.abnormalFlagCode === 'low'
                      ? 'Thấp'
                      : 'Bất thường',
              resultStatusCode: 'final',
              resultStatusName: 'Kết quả cuối',
              resultedAt: now,
              performedBy: 'KTV Xét nghiệm',
              notes: data.notes,
            },
          };
        }
        return t;
      });
      const allCompleted = updatedTests.every((t) => t.statusCode === 'completed');
      labs[index] = {
        ...labs[index],
        tests: updatedTests,
        statusCode: allCompleted ? 'completed' : labs[index].statusCode,
        statusName: allCompleted ? 'Hoàn thành' : labs[index].statusName,
      };
      this.labOrdersSubject.next([...labs]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  cancelLabOrder(id: string): Observable<void> {
    const labs = this.labOrdersSubject.value;
    const index = labs.findIndex((l) => l.id === id);
    if (index !== -1) {
      labs[index] = {
        ...labs[index],
        statusCode: 'cancelled',
        statusName: 'Đã hủy',
      };
      this.labOrdersSubject.next([...labs]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  getPatientLabOrders(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<LabOrder>> {
    const filtered = this.labOrdersSubject.value.filter((lab) => lab.patientId === patientId);
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<LabOrder> = {
      items: filtered.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  /** Extra mock method: returns count of lab orders that are not completed/cancelled */
  getPendingCount(): Observable<number> {
    const count = this.labOrdersSubject.value.filter(
      (lab) => lab.statusCode !== 'completed' && lab.statusCode !== 'cancelled',
    ).length;
    return of(count).pipe(delay(this.delayMs()));
  }
}
