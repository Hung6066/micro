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
import { Appointment, ScheduleAppointmentRequest } from '@core/models/appointment.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';
@Injectable({
  providedIn: 'root'
})
export class AppointmentService {
  private readonly baseUrl = stryMutAct_9fa48("424") ? `` : (stryCov_9fa48("424"), `${environment.apiUrl}/appointments`);
  constructor(private http: HttpClient) {}
  list(page: number = 1, pageSize: number = 20): Observable<PagedResult<Appointment>> {
    if (stryMutAct_9fa48("425")) {
      {}
    } else {
      stryCov_9fa48("425");
      const params = new HttpParams().set(stryMutAct_9fa48("426") ? "" : (stryCov_9fa48("426"), 'page'), page.toString()).set(stryMutAct_9fa48("427") ? "" : (stryCov_9fa48("427"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Appointment>>(stryMutAct_9fa48("428") ? `` : (stryCov_9fa48("428"), `${this.baseUrl}/search`), stryMutAct_9fa48("429") ? {} : (stryCov_9fa48("429"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  search(query: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Appointment>> {
    if (stryMutAct_9fa48("430")) {
      {}
    } else {
      stryCov_9fa48("430");
      const params = new HttpParams().set(stryMutAct_9fa48("431") ? "" : (stryCov_9fa48("431"), 'q'), query).set(stryMutAct_9fa48("432") ? "" : (stryCov_9fa48("432"), 'page'), page.toString()).set(stryMutAct_9fa48("433") ? "" : (stryCov_9fa48("433"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Appointment>>(stryMutAct_9fa48("434") ? `` : (stryCov_9fa48("434"), `${this.baseUrl}/search`), stryMutAct_9fa48("435") ? {} : (stryCov_9fa48("435"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getById(id: string): Observable<Appointment> {
    if (stryMutAct_9fa48("436")) {
      {}
    } else {
      stryCov_9fa48("436");
      return this.http.get<Appointment>(stryMutAct_9fa48("437") ? `` : (stryCov_9fa48("437"), `${this.baseUrl}/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  schedule(request: ScheduleAppointmentRequest): Observable<Appointment> {
    if (stryMutAct_9fa48("438")) {
      {}
    } else {
      stryCov_9fa48("438");
      return this.http.post<Appointment>(stryMutAct_9fa48("439") ? `` : (stryCov_9fa48("439"), `${this.baseUrl}/`), request).pipe(catchError(this.handleError));
    }
  }
  checkIn(id: string): Observable<void> {
    if (stryMutAct_9fa48("440")) {
      {}
    } else {
      stryCov_9fa48("440");
      return this.http.put<void>(stryMutAct_9fa48("441") ? `` : (stryCov_9fa48("441"), `${this.baseUrl}/${id}/checkin`), {}).pipe(catchError(this.handleError));
    }
  }
  checkOut(id: string): Observable<void> {
    if (stryMutAct_9fa48("442")) {
      {}
    } else {
      stryCov_9fa48("442");
      return this.http.put<void>(stryMutAct_9fa48("443") ? `` : (stryCov_9fa48("443"), `${this.baseUrl}/${id}/checkout`), {}).pipe(catchError(this.handleError));
    }
  }
  cancel(id: string, reason?: string): Observable<void> {
    if (stryMutAct_9fa48("444")) {
      {}
    } else {
      stryCov_9fa48("444");
      return this.http.put<void>(stryMutAct_9fa48("445") ? `` : (stryCov_9fa48("445"), `${this.baseUrl}/${id}/cancel`), stryMutAct_9fa48("446") ? {} : (stryCov_9fa48("446"), {
        reason
      })).pipe(catchError(this.handleError));
    }
  }
  private handleError(error: HttpErrorResponse): Observable<never> {
    if (stryMutAct_9fa48("447")) {
      {}
    } else {
      stryCov_9fa48("447");
      const errorMessage = error.error instanceof ErrorEvent ? stryMutAct_9fa48("448") ? `` : (stryCov_9fa48("448"), `Client error: ${error.error.message}`) : stryMutAct_9fa48("449") ? `` : (stryCov_9fa48("449"), `Server error: ${error.status} - ${error.message}`);
      console.error(stryMutAct_9fa48("450") ? "" : (stryCov_9fa48("450"), '[AppointmentService]'), errorMessage);
      return throwError(stryMutAct_9fa48("451") ? () => undefined : (stryCov_9fa48("451"), () => error));
    }
  }
}