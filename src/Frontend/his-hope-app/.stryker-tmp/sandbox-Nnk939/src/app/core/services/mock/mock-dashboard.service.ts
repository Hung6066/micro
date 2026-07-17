// @ts-nocheck
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';
import { Encounter } from '@core/models/encounter.model';
import { Appointment } from '@core/models/appointment.model';
import {
  mockPatients,
  mockAppointments,
  mockEncounters,
  mockLabOrders,
  mockInvoices,
  mockMedications,
} from './mock-data';

export interface DashboardStats {
  totalPatients: number;
  todayAppointments: number;
  activeEncounters: number;
  pendingDiagnoses: number;
  pendingLabs: number;
  outstandingInvoices: number;
  lowStockMedications: number;
  newPatientsToday: number;
  appointmentsTomorrow: number;
  recentEncounters: Encounter[];
  upcomingAppointments: Appointment[];
}

@Injectable({ providedIn: 'root' })
export class MockDashboardService {
  private delayMs(): number {
    return 300 + Math.floor(Math.random() * 200);
  }

  getStats(): Observable<DashboardStats> {
    const today = new Date().toISOString().slice(0, 10);
    const tomorrow = new Date(Date.now() + 86400000).toISOString().slice(0, 10);

    const todayAppointments = mockAppointments.filter((a) => a.scheduledDate === today).length;
    const appointmentsTomorrow = mockAppointments.filter((a) => a.scheduledDate === tomorrow).length;
    const activeEncounters = mockEncounters.filter(
      (e) => e.status === 'in_progress',
    ).length;
    const pendingLabs = mockLabOrders.filter(
      (lab) => lab.statusCode !== 'completed' && lab.statusCode !== 'cancelled',
    ).length;
    const outstandingInvoices = mockInvoices.filter(
      (inv) => inv.statusCode !== 'paid' && inv.statusCode !== 'voided' && inv.balanceDue > 0,
    ).length;
    const lowStockMedications = mockMedications.filter((m) => !m.isActive).length;
    const newPatientsToday = mockPatients.filter(
      (p) => p.createdAt.slice(0, 10) === today,
    ).length;
    const todayCompletedEncounters = mockEncounters
      .filter((e) => e.encounterDate.slice(0, 10) === today)
      .slice(0, 5);
    const upcomingAppts = mockAppointments
      .filter((a) => a.scheduledDate >= today && a.status === 'scheduled')
      .slice(0, 5);

    const stats: DashboardStats = {
      totalPatients: mockPatients.length,
      todayAppointments,
      activeEncounters,
      pendingDiagnoses: mockEncounters.filter((e) => !e.diagnoses || e.diagnoses.length === 0).length,
      pendingLabs,
      outstandingInvoices,
      lowStockMedications,
      newPatientsToday,
      appointmentsTomorrow,
      recentEncounters: todayCompletedEncounters,
      upcomingAppointments: upcomingAppts,
    };

    return of(stats).pipe(delay(this.delayMs()));
  }

  getRecentEncounters(limit: number = 5): Observable<{ items: Encounter[] }> {
    const sorted = [...mockEncounters]
      .sort((a, b) => new Date(b.encounterDate).getTime() - new Date(a.encounterDate).getTime())
      .slice(0, limit);
    return of({ items: sorted }).pipe(delay(this.delayMs()));
  }

  getUpcomingAppointments(): Observable<{ items: Appointment[] }> {
    const today = new Date().toISOString().slice(0, 10);
    const upcoming = mockAppointments
      .filter((a) => a.scheduledDate >= today && a.status === 'scheduled')
      .sort((a, b) => a.scheduledDate.localeCompare(b.scheduledDate))
      .slice(0, 10);
    return of({ items: upcoming }).pipe(delay(this.delayMs()));
  }
}
