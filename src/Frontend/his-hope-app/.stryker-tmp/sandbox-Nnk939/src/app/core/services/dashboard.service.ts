// @ts-nocheck
function stryNS_9fa48() {
  var g = typeof globalThis === 'object' && globalThis && globalThis.Math === Math && globalThis || new Function("return this")();
  var ns = g.__stryker__ || (g.__stryker__ = {});
  if (ns.activeMutant === undefined && g.process && g.process.env && g.process.env.__STRYKER_ACTIVE_MUTANT__) {
    ns.activeMutant = g.process.env.__STRYKER_ACTIVE_MUTANT__;
  }
  function retrieveNS() {
    return ns;
  }
  stryNS_9fa48 = retrieveNS;
  return retrieveNS();
}
stryNS_9fa48();
function stryCov_9fa48() {
  var ns = stryNS_9fa48();
  var cov = ns.mutantCoverage || (ns.mutantCoverage = {
    static: {},
    perTest: {}
  });
  function cover() {
    var c = cov.static;
    if (ns.currentTestId) {
      c = cov.perTest[ns.currentTestId] = cov.perTest[ns.currentTestId] || {};
    }
    var a = arguments;
    for (var i = 0; i < a.length; i++) {
      c[a[i]] = (c[a[i]] || 0) + 1;
    }
  }
  stryCov_9fa48 = cover;
  cover.apply(null, arguments);
}
function stryMutAct_9fa48(id) {
  var ns = stryNS_9fa48();
  function isActive(id) {
    if (ns.activeMutant === id) {
      if (ns.hitCount !== void 0 && ++ns.hitCount > ns.hitLimit) {
        throw new Error('Stryker: Hit count limit reached (' + ns.hitCount + ')');
      }
      return true;
    }
    return false;
  }
  stryMutAct_9fa48 = isActive;
  return isActive(id);
}
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Encounter } from '@core/models/encounter.model';
import { Appointment } from '@core/models/appointment.model';
import { environment } from '@env/environment';
export interface DashboardStats {
  totalPatients: number;
  todayAppointments: number;
  activeEncounters: number;
  pendingDiagnoses: number;
  // Augmented fields
  pendingLabs: number;
  outstandingInvoices: number;
  lowStockMedications: number;
  // Trend fields
  newPatientsToday: number;
  appointmentsTomorrow: number;
  recentEncounters: Encounter[];
  upcomingAppointments: Appointment[];
}
@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly baseUrl = stryMutAct_9fa48("1087") ? `` : (stryCov_9fa48("1087"), `${environment.apiUrl}/dashboard`);
  constructor(private http: HttpClient) {}
  getStats(): Observable<DashboardStats> {
    if (stryMutAct_9fa48("1088")) {
      {}
    } else {
      stryCov_9fa48("1088");
      return this.http.get<DashboardStats>(stryMutAct_9fa48("1089") ? `` : (stryCov_9fa48("1089"), `${this.baseUrl}/stats`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  getRecentEncounters(limit: number = 5): Observable<{
    items: Encounter[];
  }> {
    if (stryMutAct_9fa48("1090")) {
      {}
    } else {
      stryCov_9fa48("1090");
      return this.http.get<{
        items: Encounter[];
      }>(stryMutAct_9fa48("1091") ? `` : (stryCov_9fa48("1091"), `${this.baseUrl}/recent-encounters?limit=${limit}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  getUpcomingAppointments(): Observable<{
    items: Appointment[];
  }> {
    if (stryMutAct_9fa48("1092")) {
      {}
    } else {
      stryCov_9fa48("1092");
      return this.http.get<{
        items: Appointment[];
      }>(stryMutAct_9fa48("1093") ? `` : (stryCov_9fa48("1093"), `${this.baseUrl}/upcoming-appointments`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  private handleError(error: HttpErrorResponse): Observable<never> {
    if (stryMutAct_9fa48("1094")) {
      {}
    } else {
      stryCov_9fa48("1094");
      const errorMessage = error.error instanceof ErrorEvent ? stryMutAct_9fa48("1095") ? `` : (stryCov_9fa48("1095"), `Client error: ${error.error.message}`) : stryMutAct_9fa48("1096") ? `` : (stryCov_9fa48("1096"), `Server error: ${error.status} - ${error.message}`);
      console.error(stryMutAct_9fa48("1097") ? "" : (stryCov_9fa48("1097"), '[DashboardService]'), errorMessage);
      return throwError(stryMutAct_9fa48("1098") ? () => undefined : (stryCov_9fa48("1098"), () => error));
    }
  }
}