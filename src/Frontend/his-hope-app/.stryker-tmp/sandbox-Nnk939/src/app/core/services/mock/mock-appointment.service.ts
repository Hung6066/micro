// @ts-nocheck
import { Injectable } from '@angular/core';
import { Observable, of, BehaviorSubject } from 'rxjs';
import { delay } from 'rxjs/operators';
import { Appointment, ScheduleAppointmentRequest } from '@core/models/appointment.model';
import { PagedResult } from '@core/models/paged-result.model';
import { mockAppointments, mockPatients, mockUsers } from './mock-data';

@Injectable({ providedIn: 'root' })
export class MockAppointmentService {
  private appointmentsSubject = new BehaviorSubject<Appointment[]>([...mockAppointments]);

  private delayMs(): number {
    return 300 + Math.floor(Math.random() * 200);
  }

  list(page = 1, pageSize = 20): Observable<PagedResult<Appointment>> {
    const items = this.appointmentsSubject.value;
    const total = items.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<Appointment> = {
      items: items.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  search(query: string, page = 1, pageSize = 20): Observable<PagedResult<Appointment>> {
    const filtered = this.appointmentsSubject.value.filter(
      (a) =>
        a.reason?.toLowerCase().includes(query.toLowerCase()) ||
        a.type.toLowerCase().includes(query.toLowerCase()) ||
        a.status.toLowerCase().includes(query.toLowerCase()) ||
        a.location?.toLowerCase().includes(query.toLowerCase()),
    );
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<Appointment> = {
      items: filtered.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  getById(id: string): Observable<Appointment> {
    const appointment = this.appointmentsSubject.value.find((a) => a.id === id);
    return of(appointment!).pipe(delay(this.delayMs()));
  }

  schedule(request: ScheduleAppointmentRequest): Observable<Appointment> {
    const patient = mockPatients.find((p) => p.id === request.patientId);
    const provider = mockUsers.find((u) => u.id === request.providerId);
    const endTimeMinutes = this.timeToMinutes(request.startTime) + request.durationMinutes;
    const newAppointment: Appointment = {
      id: `apt-${String(this.appointmentsSubject.value.length + 1).padStart(3, '0')}`,
      patientId: request.patientId,
      providerId: request.providerId,
      scheduledDate: request.scheduledDate,
      startTime: request.startTime,
      endTime: `${String(Math.floor(endTimeMinutes / 60)).padStart(2, '0')}:${String(endTimeMinutes % 60).padStart(2, '0')}`,
      status: 'scheduled',
      statusName: 'Đã lịch',
      type: request.typeCode,
      typeName: request.typeCode === 'consultation' ? 'Khám bệnh' : request.typeCode === 'follow_up' ? 'Tái khám' : 'Khám',
      reason: request.reason,
      location: request.location || (provider?.id === 'usr-002' ? 'Phòng khám số 2 - Tầng 1' : 'Phòng khám số 5 - Tầng 2'),
      createdAt: new Date().toISOString(),
    };
    this.appointmentsSubject.next([...this.appointmentsSubject.value, newAppointment]);
    return of(newAppointment).pipe(delay(this.delayMs()));
  }

  checkIn(id: string): Observable<void> {
    const appointments = this.appointmentsSubject.value;
    const index = appointments.findIndex((a) => a.id === id);
    if (index !== -1) {
      appointments[index] = {
        ...appointments[index],
        status: 'checked_in',
        statusName: 'Đã đến',
        checkedInAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };
      this.appointmentsSubject.next([...appointments]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  checkOut(id: string): Observable<void> {
    const appointments = this.appointmentsSubject.value;
    const index = appointments.findIndex((a) => a.id === id);
    if (index !== -1) {
      appointments[index] = {
        ...appointments[index],
        status: 'completed',
        statusName: 'Hoàn thành',
        checkedOutAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };
      this.appointmentsSubject.next([...appointments]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  cancel(id: string, reason?: string): Observable<void> {
    const appointments = this.appointmentsSubject.value;
    const index = appointments.findIndex((a) => a.id === id);
    if (index !== -1) {
      appointments[index] = {
        ...appointments[index],
        status: 'cancelled',
        statusName: 'Đã hủy',
        cancelledAt: new Date().toISOString(),
        cancellationReason: reason,
        updatedAt: new Date().toISOString(),
      };
      this.appointmentsSubject.next([...appointments]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  private timeToMinutes(time: string): number {
    const [h, m] = time.split(':').map(Number);
    return h * 60 + m;
  }
}
