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
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Patient, CreatePatientRequest } from '@core/models/patient.model';
import { Encounter } from '@core/models/encounter.model';
import { Appointment } from '@core/models/appointment.model';
import { Prescription } from '@core/models/prescription.model';
import { LabOrder } from '@core/models/lab-order.model';
import { Invoice } from '@core/models/invoice.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';
@Injectable({
  providedIn: 'root'
})
export class PatientService {
  private readonly baseUrl = stryMutAct_9fa48("1579") ? `` : (stryCov_9fa48("1579"), `${environment.apiUrl}/patients`);
  constructor(private http: HttpClient) {}
  search(query: string, page = 1, pageSize = 20): Observable<PagedResult<Patient>> {
    if (stryMutAct_9fa48("1580")) {
      {}
    } else {
      stryCov_9fa48("1580");
      const params = new HttpParams().set(stryMutAct_9fa48("1581") ? "" : (stryCov_9fa48("1581"), 'q'), query).set(stryMutAct_9fa48("1582") ? "" : (stryCov_9fa48("1582"), 'page'), page.toString()).set(stryMutAct_9fa48("1583") ? "" : (stryCov_9fa48("1583"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Patient>>(stryMutAct_9fa48("1584") ? `` : (stryCov_9fa48("1584"), `${this.baseUrl}/search`), stryMutAct_9fa48("1585") ? {} : (stryCov_9fa48("1585"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getById(id: string): Observable<Patient> {
    if (stryMutAct_9fa48("1586")) {
      {}
    } else {
      stryCov_9fa48("1586");
      return this.http.get<Patient>(stryMutAct_9fa48("1587") ? `` : (stryCov_9fa48("1587"), `${this.baseUrl}/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  create(request: CreatePatientRequest): Observable<Patient> {
    if (stryMutAct_9fa48("1588")) {
      {}
    } else {
      stryCov_9fa48("1588");
      return this.http.post<Patient>(stryMutAct_9fa48("1589") ? `` : (stryCov_9fa48("1589"), `${this.baseUrl}/`), request).pipe(catchError(this.handleError));
    }
  }
  update(id: string, request: CreatePatientRequest): Observable<Patient> {
    if (stryMutAct_9fa48("1590")) {
      {}
    } else {
      stryCov_9fa48("1590");
      return this.http.put<Patient>(stryMutAct_9fa48("1591") ? `` : (stryCov_9fa48("1591"), `${this.baseUrl}/${id}`), request).pipe(catchError(this.handleError));
    }
  }
  deactivate(id: string): Observable<void> {
    if (stryMutAct_9fa48("1592")) {
      {}
    } else {
      stryCov_9fa48("1592");
      return this.http.patch<void>(stryMutAct_9fa48("1593") ? `` : (stryCov_9fa48("1593"), `${this.baseUrl}/${id}/deactivate`), {}).pipe(catchError(this.handleError));
    }
  }
  reactivate(id: string): Observable<void> {
    if (stryMutAct_9fa48("1594")) {
      {}
    } else {
      stryCov_9fa48("1594");
      return this.http.patch<void>(stryMutAct_9fa48("1595") ? `` : (stryCov_9fa48("1595"), `${this.baseUrl}/${id}/reactivate`), {}).pipe(catchError(this.handleError));
    }
  }

  // ── Cross-service convenience methods ──

  getEncounters(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Encounter>> {
    if (stryMutAct_9fa48("1596")) {
      {}
    } else {
      stryCov_9fa48("1596");
      const params = new HttpParams().set(stryMutAct_9fa48("1597") ? "" : (stryCov_9fa48("1597"), 'patientId'), patientId).set(stryMutAct_9fa48("1598") ? "" : (stryCov_9fa48("1598"), 'page'), page.toString()).set(stryMutAct_9fa48("1599") ? "" : (stryCov_9fa48("1599"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Encounter>>(stryMutAct_9fa48("1600") ? `` : (stryCov_9fa48("1600"), `${this.baseUrl}/${patientId}/encounters`), stryMutAct_9fa48("1601") ? {} : (stryCov_9fa48("1601"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getAppointments(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Appointment>> {
    if (stryMutAct_9fa48("1602")) {
      {}
    } else {
      stryCov_9fa48("1602");
      const params = new HttpParams().set(stryMutAct_9fa48("1603") ? "" : (stryCov_9fa48("1603"), 'page'), page.toString()).set(stryMutAct_9fa48("1604") ? "" : (stryCov_9fa48("1604"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Appointment>>(stryMutAct_9fa48("1605") ? `` : (stryCov_9fa48("1605"), `${this.baseUrl}/${patientId}/appointments`), stryMutAct_9fa48("1606") ? {} : (stryCov_9fa48("1606"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getPrescriptions(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Prescription>> {
    if (stryMutAct_9fa48("1607")) {
      {}
    } else {
      stryCov_9fa48("1607");
      const params = new HttpParams().set(stryMutAct_9fa48("1608") ? "" : (stryCov_9fa48("1608"), 'page'), page.toString()).set(stryMutAct_9fa48("1609") ? "" : (stryCov_9fa48("1609"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Prescription>>(stryMutAct_9fa48("1610") ? `` : (stryCov_9fa48("1610"), `${this.baseUrl}/${patientId}/prescriptions`), stryMutAct_9fa48("1611") ? {} : (stryCov_9fa48("1611"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getLabOrders(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<LabOrder>> {
    if (stryMutAct_9fa48("1612")) {
      {}
    } else {
      stryCov_9fa48("1612");
      const params = new HttpParams().set(stryMutAct_9fa48("1613") ? "" : (stryCov_9fa48("1613"), 'page'), page.toString()).set(stryMutAct_9fa48("1614") ? "" : (stryCov_9fa48("1614"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<LabOrder>>(stryMutAct_9fa48("1615") ? `` : (stryCov_9fa48("1615"), `${this.baseUrl}/${patientId}/lab-orders`), stryMutAct_9fa48("1616") ? {} : (stryCov_9fa48("1616"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getInvoices(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Invoice>> {
    if (stryMutAct_9fa48("1617")) {
      {}
    } else {
      stryCov_9fa48("1617");
      const params = new HttpParams().set(stryMutAct_9fa48("1618") ? "" : (stryCov_9fa48("1618"), 'page'), page.toString()).set(stryMutAct_9fa48("1619") ? "" : (stryCov_9fa48("1619"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Invoice>>(stryMutAct_9fa48("1620") ? `` : (stryCov_9fa48("1620"), `${this.baseUrl}/${patientId}/invoices`), stryMutAct_9fa48("1621") ? {} : (stryCov_9fa48("1621"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  private handleError(error: HttpErrorResponse): Observable<never> {
    if (stryMutAct_9fa48("1622")) {
      {}
    } else {
      stryCov_9fa48("1622");
      const errorMessage = error.error instanceof ErrorEvent ? stryMutAct_9fa48("1623") ? `` : (stryCov_9fa48("1623"), `Client error: ${error.error.message}`) : stryMutAct_9fa48("1624") ? `` : (stryCov_9fa48("1624"), `Server error: ${error.status} - ${error.message}`);
      console.error(stryMutAct_9fa48("1625") ? "" : (stryCov_9fa48("1625"), '[PatientService]'), errorMessage);
      return throwError(stryMutAct_9fa48("1626") ? () => undefined : (stryCov_9fa48("1626"), () => error));
    }
  }
}