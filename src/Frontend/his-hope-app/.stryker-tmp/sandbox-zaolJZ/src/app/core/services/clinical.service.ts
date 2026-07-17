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
import { Encounter, StartEncounterRequest, RecordVitalsRequest, AddDiagnosisRequest } from '@core/models/encounter.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';
@Injectable({
  providedIn: 'root'
})
export class ClinicalService {
  private readonly baseUrl = stryMutAct_9fa48("999") ? `` : (stryCov_9fa48("999"), `${environment.apiUrl}/encounters`);
  constructor(private http: HttpClient) {}
  list(page: number = 1, pageSize: number = 20): Observable<PagedResult<Encounter>> {
    if (stryMutAct_9fa48("1000")) {
      {}
    } else {
      stryCov_9fa48("1000");
      const params = new HttpParams().set(stryMutAct_9fa48("1001") ? "" : (stryCov_9fa48("1001"), 'page'), page.toString()).set(stryMutAct_9fa48("1002") ? "" : (stryCov_9fa48("1002"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Encounter>>(stryMutAct_9fa48("1003") ? `` : (stryCov_9fa48("1003"), `${this.baseUrl}/search`), stryMutAct_9fa48("1004") ? {} : (stryCov_9fa48("1004"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  search(query: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Encounter>> {
    if (stryMutAct_9fa48("1005")) {
      {}
    } else {
      stryCov_9fa48("1005");
      const params = new HttpParams().set(stryMutAct_9fa48("1006") ? "" : (stryCov_9fa48("1006"), 'q'), query).set(stryMutAct_9fa48("1007") ? "" : (stryCov_9fa48("1007"), 'page'), page.toString()).set(stryMutAct_9fa48("1008") ? "" : (stryCov_9fa48("1008"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Encounter>>(stryMutAct_9fa48("1009") ? `` : (stryCov_9fa48("1009"), `${this.baseUrl}/search`), stryMutAct_9fa48("1010") ? {} : (stryCov_9fa48("1010"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getById(id: string): Observable<Encounter> {
    if (stryMutAct_9fa48("1011")) {
      {}
    } else {
      stryCov_9fa48("1011");
      return this.http.get<Encounter>(stryMutAct_9fa48("1012") ? `` : (stryCov_9fa48("1012"), `${this.baseUrl}/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  start(request: StartEncounterRequest): Observable<Encounter> {
    if (stryMutAct_9fa48("1013")) {
      {}
    } else {
      stryCov_9fa48("1013");
      return this.http.post<Encounter>(stryMutAct_9fa48("1014") ? `` : (stryCov_9fa48("1014"), `${this.baseUrl}/`), request).pipe(catchError(this.handleError));
    }
  }
  recordVitals(id: string, request: RecordVitalsRequest): Observable<void> {
    if (stryMutAct_9fa48("1015")) {
      {}
    } else {
      stryCov_9fa48("1015");
      return this.http.post<void>(stryMutAct_9fa48("1016") ? `` : (stryCov_9fa48("1016"), `${this.baseUrl}/${id}/vitals`), request).pipe(catchError(this.handleError));
    }
  }
  addDiagnosis(id: string, request: AddDiagnosisRequest): Observable<void> {
    if (stryMutAct_9fa48("1017")) {
      {}
    } else {
      stryCov_9fa48("1017");
      return this.http.post<void>(stryMutAct_9fa48("1018") ? `` : (stryCov_9fa48("1018"), `${this.baseUrl}/${id}/diagnosis`), request).pipe(catchError(this.handleError));
    }
  }
  complete(id: string): Observable<void> {
    if (stryMutAct_9fa48("1019")) {
      {}
    } else {
      stryCov_9fa48("1019");
      return this.http.put<void>(stryMutAct_9fa48("1020") ? `` : (stryCov_9fa48("1020"), `${this.baseUrl}/${id}/complete`), {}).pipe(catchError(this.handleError));
    }
  }
  private handleError(error: HttpErrorResponse): Observable<never> {
    if (stryMutAct_9fa48("1021")) {
      {}
    } else {
      stryCov_9fa48("1021");
      const errorMessage = error.error instanceof ErrorEvent ? stryMutAct_9fa48("1022") ? `` : (stryCov_9fa48("1022"), `Client error: ${error.error.message}`) : stryMutAct_9fa48("1023") ? `` : (stryCov_9fa48("1023"), `Server error: ${error.status} - ${error.message}`);
      console.error(stryMutAct_9fa48("1024") ? "" : (stryCov_9fa48("1024"), '[ClinicalService]'), errorMessage);
      return throwError(stryMutAct_9fa48("1025") ? () => undefined : (stryCov_9fa48("1025"), () => error));
    }
  }
}